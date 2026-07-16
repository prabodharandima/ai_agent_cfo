$ErrorActionPreference = 'Stop'

dotnet build CfoAgent.sln

Push-Location src/cfo-agent-ui
try {
  npm run build
}
finally {
  Pop-Location
}
