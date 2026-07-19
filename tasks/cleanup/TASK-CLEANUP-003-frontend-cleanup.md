# TASK-CLEANUP-003 — Frontend Cleanup

## Objective

Remove proven unused React, TypeScript, CSS, assets, dependencies, and test helpers while preserving the current UI.

## Scope

Clean:

- React components
- TypeScript types/helpers
- CSS classes/files
- static assets
- npm dependencies/devDependencies
- obsolete frontend tests/utilities
- empty folders

## Requirements

- Verify usage through imports, dynamic references, CSS class names, tests, Vite configuration, Playwright, and public assets.
- Remove only proven unused files/symbols.
- Remove unused npm packages only after checking build/test tooling.
- Preserve all response renderers, loading/error/empty states, accessibility behavior, example prompts, API client behavior, environment variables, and Playwright selectors.
- Do not redesign styling.
- Do not mass-format unrelated files.
- Update the cleanup inventory.

## Acceptance criteria

- Frontend build passes.
- Frontend unit tests pass.
- Existing E2E behavior is preserved.
- No visible functionality changes.

## Validation

```bash
npm --prefix src/cfo-agent-ui ci
npm --prefix src/cfo-agent-ui test
npm --prefix src/cfo-agent-ui run build
npm --prefix src/cfo-agent-ui ls --depth=0
git diff --check
```

Run Playwright if the repository provides a stable command.

Stop after this task.
