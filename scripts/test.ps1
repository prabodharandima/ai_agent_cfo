$ErrorActionPreference = 'Stop'

dotnet test CfoAgent.sln

Push-Location src/cfo-agent-ui
try {
  npm test -- --run
}
finally {
  Pop-Location
}
