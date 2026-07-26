# TASK-MAF-002 Result - Agent Middleware

## Previous Changes Detected

- `TASK-MAF-000-RESULT.md` and `TASK-MAF-001-RESULT.md` were present before this task.
- The working tree already contained unrelated changes to `CfoAgent.sln`, `IMPLEMENTATION-PLAN.md`, `src/CfoAgent.Api/AI/Ollama/OllamaChatClient.cs`, and `src/cfo-agent-ui/Web.config`. They were preserved.

## Files Changed

- `src/CfoAgent.Api/CfoAgent.Api.csproj`
- `src/CfoAgent.Api/Configuration/AgentMiddlewareOptions.cs`
- `src/CfoAgent.Api/AI/AgentChatMiddleware.cs`
- `src/CfoAgent.Api/AI/PromptInjectionRiskException.cs`
- `src/CfoAgent.Api/Observability/ApiExceptionHandler.cs`
- `src/CfoAgent.Api/Program.cs`
- `src/CfoAgent.Api/appsettings.json`
- `tests/CfoAgent.Api.Tests/AI/AgentChatMiddlewareTests.cs`
- `tests/CfoAgent.Api.Tests/AI/AiProviderRegistrationTests.cs`
- `AGENT.md`
- `APPLICATION_ARCHITECTURE.md`
- `USER-GUIDE.md`

## Middleware Behavior

- Added `Microsoft.Agents.AI` 1.13.0 and wrapped the composition-root `IChatClient` with the Microsoft Agent Framework-compatible `IChatClient` middleware pipeline.
- `AgentChatMiddleware` adds non-streaming execution timing, correlation-aware safe structured logging, configurable deterministic prompt-risk checks, and sensitive-output redaction.
- Prompt checks use `AgentMiddleware:PromptInjectionCheckEnabled` and `AgentMiddleware:SuspiciousPromptPhrases`. The defaults are in `appsettings.json`; invalid empty, blank, or duplicate configuration fails at startup.
- The middleware neither routes business requests nor selects MCP servers/tools. It also does not replace the global exception handler.
- `PromptInjectionRiskException` is centrally mapped to sanitized HTTP 400 Problem Details. Caller cancellation is rethrown unchanged. Provider failures still follow the existing error path.

## Tests And Results

- Focused middleware and registration tests: **18 passed, 0 failed**.
- Full solution test suite after Docker Desktop was started: **209 passed, 0 failed, 8 intentionally skipped**. The skips are existing container-script or opt-in Ollama tests.

## Documentation Updated

- `AGENT.md` documents the cross-cutting middleware scope and configuration.
- `APPLICATION_ARCHITECTURE.md` documents the middleware position, responsibilities, safe error handling, and configuration keys.
- `USER-GUIDE.md` documents direct-development configuration names and the deployed defaults.

## Warnings

- Git reported existing LF-to-CRLF working-tree warnings for edited source files.

## Blockers

- None after the solution website entry was removed, Docker Desktop was started, and the shared-fixture seed assertion was made order-independent.

## Completion Status

Implementation and validation are complete. `dotnet build CfoAgent.sln --no-restore --maxcpucount:1` passed with zero warnings/errors, and the Docker-enabled solution test gate passed.
