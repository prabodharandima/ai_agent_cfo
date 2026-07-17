$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repositoryRoot
$env:ASPNETCORE_ENVIRONMENT = 'Development'

docker compose up -d

$deadline = (Get-Date).AddMinutes(2)
do {
  try {
    Invoke-WebRequest -UseBasicParsing http://127.0.0.1:8000/api/v2/heartbeat | Out-Null
    $chromaReady = $true
  }
  catch {
    Start-Sleep -Seconds 2
  }
} while (-not $chromaReady -and (Get-Date) -lt $deadline)

if (-not $chromaReady) {
  throw 'ChromaDB did not become ready within two minutes.'
}

dotnet build CfoAgent.sln --configuration Debug --maxcpucount:1
dotnet run --project src/CfoAgent.Api --no-build -- --seed
dotnet run --project src/CfoAgent.Api --no-build -- --ingest-rag

$env:ASPNETCORE_URLS = 'http://localhost:5260'
$env:Mcp__Finance__Enabled = 'true'
$env:Mcp__Finance__ServerProjectPath = Join-Path $repositoryRoot 'tools/CfoAgent.FinanceMcpServer'
$env:Mcp__KnowledgeFiles__Enabled = 'true'
$env:Mcp__KnowledgeFiles__RootPath = Join-Path $repositoryRoot 'data/knowledge'

dotnet run --project src/CfoAgent.Api --no-build --no-launch-profile
