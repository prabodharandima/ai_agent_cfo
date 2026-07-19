import { expect, test } from '@playwright/test'

async function ask(page: import('@playwright/test').Page, prompt: string) {
  await page.getByRole('button', { name: prompt, exact: true }).click()
  await expect(page.getByText('Preparing a verified CFO response...')).toBeHidden()
}

test.beforeEach(async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'CFO AI Agent' })).toBeVisible()
  await expect(page.getByText('Mock LLM')).toHaveCount(0)
})

test('shows the deterministic weekly sales summary', async ({ page }) => {
  await ask(page, 'Give me the sales summary of this week.')
  await expect(page.getByLabel('Sales summary metrics')).toBeVisible()
  await expect(page.getByText('Net revenue')).toBeVisible()
  await expect(page.getByText(/Agents: CfoOrchestratorAgent, SalesAnalysisAgent/)).toBeVisible()
})

test('shows the week-over-week comparison', async ({ page }) => {
  await ask(page, "Compare this week's sales with last week.")
  const comparison = page.getByLabel('Week-over-week comparison')
  await expect(comparison).toBeVisible()
  await expect(comparison.getByText('Current week', { exact: true })).toBeVisible()
  await expect(page.getByText(/Agents: CfoOrchestratorAgent, SalesAnalysisAgent/)).toBeVisible()
})

test('shows the current-month top products table', async ({ page }) => {
  await ask(page, 'Show me the top five products this month.')
  await expect(page.getByRole('table', { name: 'Top products' })).toBeVisible()
  await expect(page.getByText(/Agents: CfoOrchestratorAgent, SalesAnalysisAgent/)).toBeVisible()
})

test('shows the deterministic five-year forecast table and chart', async ({ page }) => {
  await ask(page, 'Give me the sales forecast for the next five years.')
  await expect(page.getByLabel('Expected net revenue forecast chart')).toBeVisible()
  await expect(page.getByRole('table', { name: 'Five-year sales forecast' })).toBeVisible()
  await expect(page.getByText(/Agents: CfoOrchestratorAgent, ForecastingAgent/)).toBeVisible()
})

test('shows annual-target assumptions with RAG sources', async ({ page }) => {
  await ask(page, 'What is the annual sales target and what assumptions were used?')
  await expect(page.getByRole('heading', { name: 'Sources' })).toBeVisible()
  await expect(page.getByText(/Agents: CfoOrchestratorAgent, FinancialKnowledgeAgent/)).toBeVisible()
  await expect(page.locator('.sources li').first()).toBeVisible()
})

test('keeps an empty prompt from being submitted', async ({ page }) => {
  await expect(page.getByRole('button', { name: 'Send' })).toBeDisabled()
  await expect(page.getByText('Your finance briefing starts here')).toBeVisible()
})

test('shows a useful dependency failure message', async ({ page }) => {
  await page.route('**/api/chat', async (route) => {
    await route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{"title":"CFO assistant is temporarily unavailable."}' })
  })
  await page.getByRole('button', { name: 'Give me the sales summary of this week.', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('could not complete that request')
})
