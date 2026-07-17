import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const forecastResponse = {
  conversationId: 'conversation-1', answer: 'Mock forecast explanation based only on verified values.', agentNames: ['CfoOrchestratorAgent', 'ForecastingAgent'], responseType: 'forecast' as const,
  structuredData: { forecasts: [{ year: 2026, conservativeNetRevenue: 90_000, expectedNetRevenue: 100_000, optimisticNetRevenue: 110_000 }] },
  sources: [{ documentId: 'forecast', documentName: 'Forecast Assumptions', section: 'Planning', sourcePath: 'data/knowledge/forecast.md', period: '2026-2030' }], assumptions: ['Planning estimate only.'], warnings: [], dataPeriod: { label: '2026-2030' }, model: { provider: 'Mock', name: 'DeterministicMock' },
}

afterEach(() => vi.restoreAllMocks())

describe('App', () => {
  it('renders the Mock LLM marker, example prompts, and empty conversation state', () => {
    render(<App />)
    expect(screen.getByRole('heading', { name: 'CFO AI Agent' })).toBeInTheDocument()
    expect(screen.getByText('Mock LLM')).toBeInTheDocument()
    expect(screen.getByText('Your finance briefing starts here')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /sales summary|compare|top five|forecast|annual sales target/i })).toHaveLength(5)
  })

  it('submits a prompt, preserves the returned conversation id, and renders forecast visuals', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify(forecastResponse), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: /forecast for the next five years/i }))
    expect(screen.getByRole('status')).toHaveTextContent('Preparing a verified CFO response')
    await screen.findByText('Mock forecast explanation based only on verified values.')
    expect(screen.getByRole('table', { name: 'Five-year sales forecast' })).toBeInTheDocument()
    expect(screen.getByText('Forecast Assumptions')).toBeInTheDocument()
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('http://localhost:5260/api/chat', expect.objectContaining({ method: 'POST' })))
  })

  it('shows a useful error when the API fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('', { status: 503 }))
    render(<App />)
    const input = screen.getByLabelText('Ask the CFO AI Agent')
    fireEvent.change(input, { target: { value: 'Give me the sales summary of this week.' } })
    fireEvent.submit(input.closest('form')!)
    expect(await screen.findByRole('alert')).toHaveTextContent('could not complete that request')
  })
})
