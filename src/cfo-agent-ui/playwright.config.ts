import { defineConfig, devices } from '@playwright/test'
import { resolve } from 'node:path'

const repositoryRoot = resolve(import.meta.dirname, '../..')

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: `powershell -NoProfile -ExecutionPolicy Bypass -File "${resolve(repositoryRoot, 'scripts/start-phase-5-e2e-api.ps1')}"`,
      cwd: repositoryRoot,
      url: 'http://localhost:5260/health/live',
      timeout: 180_000,
      reuseExistingServer: false,
    },
    {
      command: 'npm run dev -- --host localhost',
      cwd: resolve(repositoryRoot, 'src/cfo-agent-ui'),
      url: 'http://localhost:5173',
      timeout: 60_000,
      reuseExistingServer: false,
    },
  ],
})
