param()

$env:CFO_AGENT_RUN_OLLAMA_TESTS = "true"
dotnet test CfoAgent.sln --no-build --maxcpucount:1 --filter "Category=LiveOllama"
exit $LASTEXITCODE
