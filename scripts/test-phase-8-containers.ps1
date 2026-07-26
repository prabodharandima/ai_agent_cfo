[CmdletBinding()]
param(
  [ValidatePattern('^[a-z0-9][a-z0-9_-]+$')]
  [string]$ProjectName = 'cfo-p8-integration',
  [ValidateRange(1024, 65535)]
  [int]$ApiPort = 5261,
  [ValidateRange(30, 600)]
  [int]$TimeoutSeconds = 180,
  [switch]$SkipBuild,
  [switch]$KeepResources
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot 'docker-compose.yml'
$integrationFile = Join-Path $repositoryRoot 'docker-compose.integration.yml'
$composeArguments = @('-p', $ProjectName, '-f', $composeFile, '-f', $integrationFile)
$previousApiPort = [Environment]::GetEnvironmentVariable('CFO_API_PORT', 'Process')
$script:httpClient = $null

function Assert-Condition {
  param([bool]$Condition, [string]$Message)

  if (-not $Condition) {
    throw $Message
  }
}

function Invoke-Compose {
  param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = 'Continue'
    & docker compose @composeArguments @Arguments
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  if ($exitCode -ne 0) {
    throw "docker compose $($Arguments -join ' ') failed with exit code $exitCode."
  }
}

function Get-ServiceContainerId {
  param([string]$Service)

  $id = & docker compose @composeArguments ps --all -q $Service
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($id)) {
    throw "The $Service container does not exist."
  }

  return ($id | Select-Object -First 1).Trim()
}

function Wait-ServiceHealthy {
  param([string]$Service)

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  do {
    $containerId = Get-ServiceContainerId $Service
    $status = (& docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $containerId).Trim()
    if ($LASTEXITCODE -eq 0 -and $status -eq 'healthy') {
      return
    }

    Start-Sleep -Seconds 2
  } while ((Get-Date) -lt $deadline)

  throw "$Service did not become healthy within $TimeoutSeconds seconds."
}

function Wait-ServiceCompleted {
  param([string]$Service)

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  do {
    $containerId = Get-ServiceContainerId $Service
    $state = (& docker inspect --format '{{.State.Status}}|{{.State.ExitCode}}' $containerId).Trim()
    if ($LASTEXITCODE -eq 0 -and $state -eq 'exited|0') {
      return
    }

    if ($state -match '^exited\|(?<code>\d+)$' -and $Matches.code -ne '0') {
      throw "$Service exited with code $($Matches.code)."
    }

    Start-Sleep -Seconds 2
  } while ((Get-Date) -lt $deadline)

  throw "$Service did not complete within $TimeoutSeconds seconds."
}

function Invoke-Chat {
  param([string]$Message)

  $json = @{ message = $Message } | ConvertTo-Json -Compress
  $content = New-Object System.Net.Http.StringContent($json, [System.Text.Encoding]::UTF8, 'application/json')
  try {
    $response = $script:httpClient.PostAsync('api/chat', $content).GetAwaiter().GetResult()
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    return [pscustomobject]@{
      StatusCode = [int]$response.StatusCode
      ContentType = $response.Content.Headers.ContentType.MediaType
      Body = $body
      Json = $body | ConvertFrom-Json
    }
  }
  finally {
    $content.Dispose()
  }
}

function Wait-ReadinessStatus {
  param([int]$ExpectedStatusCode)

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  do {
    $response = $null
    try {
      $response = $script:httpClient.GetAsync('health/ready').GetAwaiter().GetResult()
      if ([int]$response.StatusCode -eq $ExpectedStatusCode) {
        return
      }
    }
    catch [System.Net.Http.HttpRequestException] {
      # The API may briefly refuse connections while its container restarts.
    }
    finally {
      if ($null -ne $response) {
        $response.Dispose()
      }
    }

    Start-Sleep -Seconds 2
  } while ((Get-Date) -lt $deadline)

  throw "API readiness did not return HTTP $ExpectedStatusCode within $TimeoutSeconds seconds."
}

function Assert-SanitizedDependencyFailure {
  param([pscustomobject]$Response, [string]$Dependency)

  Assert-Condition ($Response.StatusCode -eq 503) "$Dependency outage returned HTTP $($Response.StatusCode), expected 503."
  Assert-Condition ($Response.ContentType -eq 'application/problem+json') "$Dependency outage did not return Problem Details."
  Assert-Condition ($Response.Json.status -eq 503) "$Dependency outage Problem Details did not contain status 503."

  $normalized = $Response.Body.ToLowerInvariant()
  foreach ($forbidden in @('stacktrace', 'connectionstrings', 'password=', 'postgres:', '/knowledge', 'npgsql')) {
    Assert-Condition (-not $normalized.Contains($forbidden)) "$Dependency outage response exposed forbidden detail '$forbidden'."
  }
}

function Assert-ContainerBoundaries {
  $services = @('postgres', 'finance-mcp', 'knowledge-mcp', 'chromadb', 'api')
  foreach ($service in $services) {
    $container = (docker inspect (Get-ServiceContainerId $service) | ConvertFrom-Json)[0]
    $published = @($container.NetworkSettings.Ports.PSObject.Properties | Where-Object { $null -ne $_.Value })
    if ($service -eq 'api') {
      Assert-Condition ($published.Count -eq 1) 'The API must be the only service with one published port.'
      Assert-Condition ($published[0].Value[0].HostPort -eq $ApiPort.ToString()) "The API is not published on host port $ApiPort."
    }
    else {
      Assert-Condition ($published.Count -eq 0) "$service unexpectedly publishes a host port."
    }
  }

  $api = (docker inspect (Get-ServiceContainerId 'api') | ConvertFrom-Json)[0]
  $apiEnvironment = @($api.Config.Env)
  Assert-Condition (-not ($apiEnvironment -match '^ConnectionStrings__')) 'The API contains a database connection string.'
  Assert-Condition (-not ($apiEnvironment -match 'postgres')) 'The API contains a PostgreSQL endpoint.'
  Assert-Condition ($apiEnvironment -contains 'Mcp__KnowledgeFiles__UseLocalFallback=false') 'Knowledge local fallback is not disabled.'
  Assert-Condition ($apiEnvironment -contains 'AI__Ollama__BaseUrl=http://host.docker.internal:11434') 'Ollama is not routed through host.docker.internal.'

  $knowledge = (docker inspect (Get-ServiceContainerId 'knowledge-mcp') | ConvertFrom-Json)[0]
  $knowledgeMount = @($knowledge.Mounts | Where-Object { $_.Destination -eq '/knowledge' })
  Assert-Condition ($knowledgeMount.Count -eq 1) 'The Knowledge MCP knowledge mount is missing.'
  Assert-Condition (-not $knowledgeMount[0].RW) 'The Knowledge MCP knowledge mount is writable.'
  Assert-Condition $knowledge.HostConfig.ReadonlyRootfs 'The Knowledge MCP root filesystem is not read-only.'

  $backend = (docker network inspect "${ProjectName}_backend" | ConvertFrom-Json)[0]
  Assert-Condition $backend.Internal 'The backend Docker network is not internal.'
}

function Assert-DatabaseInitialized {
  Wait-ServiceCompleted 'finance-db-init'
  $postgresId = Get-ServiceContainerId 'postgres'
  $query = 'SELECT (SELECT COUNT(*) FROM "Products"), (SELECT COUNT(*) FROM "Sales"), (SELECT COUNT(*) FROM "BudgetTargets");'
  $counts = $query | & docker exec -i $postgresId psql -U cfo_agent -d cfo_agent -tA
  if ($LASTEXITCODE -ne 0) {
    throw 'PostgreSQL seed verification failed.'
  }

  Assert-Condition (($counts | Select-Object -First 1).Trim() -eq '8|1104|18') "Unexpected deterministic seed counts: $counts"
}

function Restart-ApiAfterDependency {
  param([string]$Service)

  Invoke-Compose -Arguments @('start', $Service)
  Wait-ServiceHealthy $Service
  Invoke-Compose -Arguments @('restart', 'api')
  Wait-ServiceHealthy 'api'
}

Set-Location $repositoryRoot
[Environment]::SetEnvironmentVariable('CFO_API_PORT', $ApiPort.ToString(), 'Process')

try {
  Add-Type -AssemblyName System.Net.Http

  Write-Host "Resetting only isolated Compose project '$ProjectName'."
  Invoke-Compose -Arguments @('down', '--volumes', '--remove-orphans')
  Invoke-Compose -Arguments @('config', '--quiet')

  if (-not $SkipBuild) {
    dotnet build tests/CfoAgent.Api.Tests/CfoAgent.Api.Tests.csproj --configuration Release --maxcpucount:1
    if ($LASTEXITCODE -ne 0) {
      throw "The Release container-test assembly build failed with exit code $LASTEXITCODE."
    }

    Invoke-Compose -Arguments @('build')
  }

  Invoke-Compose -Arguments @('up', '-d')
  Wait-ServiceHealthy 'postgres'
  Wait-ServiceCompleted 'finance-db-init'
  Wait-ServiceHealthy 'finance-mcp'
  Wait-ServiceHealthy 'knowledge-mcp'
  Wait-ServiceHealthy 'chromadb'
  Wait-ServiceCompleted 'rag-init'
  Wait-ServiceHealthy 'api'

  $script:httpClient = New-Object System.Net.Http.HttpClient
  $script:httpClient.BaseAddress = [Uri]"http://127.0.0.1:$ApiPort/"
  $script:httpClient.Timeout = [TimeSpan]::FromSeconds(30)

  Assert-ContainerBoundaries
  Assert-DatabaseInitialized

  Write-Host 'Running real container-to-container API and MCP tests.'
  Invoke-Compose -Arguments @('--profile', 'integration', 'run', '--rm', '--no-deps', 'container-tests')

  Write-Host 'Stopping Knowledge MCP and verifying the API readiness boundary.'
  Invoke-Compose -Arguments @('stop', 'knowledge-mcp')
  Invoke-Compose -Arguments @('restart', 'api')
  Wait-ReadinessStatus 503
  Restart-ApiAfterDependency 'knowledge-mcp'

  Write-Host 'Stopping Finance MCP and verifying sanitized 503 without a database fallback.'
  Invoke-Compose -Arguments @('stop', 'finance-mcp')
  $financeFailure = Invoke-Chat 'Give me the sales summary of this week.'
  Assert-SanitizedDependencyFailure $financeFailure 'Finance MCP'
  Restart-ApiAfterDependency 'finance-mcp'

  $ready = $script:httpClient.GetAsync('health/ready').GetAwaiter().GetResult()
  Assert-Condition ([int]$ready.StatusCode -eq 200) 'The API did not recover to a healthy state after dependency outage tests.'

  Invoke-Compose -Arguments @('ps', '--all')
  Write-Host 'Phase 8 container integration and resilience gate passed.'
}
catch {
  Write-Host 'Container gate failed. Capturing isolated Compose logs.' -ForegroundColor Red
  & docker compose @composeArguments logs --no-color
  throw
}
finally {
  if ($null -ne $script:httpClient) {
    $script:httpClient.Dispose()
  }

  if (-not $KeepResources) {
    & docker compose @composeArguments down --volumes --remove-orphans
  }

  [Environment]::SetEnvironmentVariable('CFO_API_PORT', $previousApiPort, 'Process')
}
