import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ChatResponseContent } from './ChatResponseContent'
import type { ChatResponse, ChatResponseType } from './types'

const responseTypes: ChatResponseType[] = ['sales_summary', 'sales_comparison', 'top_products', 'forecast', 'knowledge', 'mixed', 'unsupported']

const structuredDataByType: Record<ChatResponseType, unknown> = {
  sales_summary: { netRevenue: 1000, grossProfit: 300, grossMarginPercent: 30, orderCount: 2, quantitySold: 4, averageOrderValue: 500 },
  sales_comparison: { currentWeek: { netRevenue: 1000 }, previousWeek: { netRevenue: 900 }, netRevenueChange: 100, netRevenueChangePercentage: 11.1, direction: 'Increased' },
  top_products: { products: [{ productCode: 'FIN-1', productName: 'Ledger Pro', quantitySold: 2, netRevenue: 1000, grossProfit: 300 }] },
  forecast: { forecasts: [{ year: 2026, conservativeNetRevenue: 900, expectedNetRevenue: 1000, optimisticNetRevenue: 1100 }] },
  knowledge: {},
  mixed: {},
  unsupported: null,
}

describe('ChatResponseContent', () => {
  it.each(responseTypes)('renders %s responses without an error', (responseType) => {
    const response: ChatResponse = {
      conversationId: 'conversation-1', answer: `${responseType} answer`, agentNames: ['CfoOrchestratorAgent'], responseType,
      structuredData: structuredDataByType[responseType], sources: [], assumptions: [], warnings: [], dataPeriod: { label: 'Demo period' }, model: { provider: 'Ollama', name: 'llama3.2:3b' },
    }

    render(<ChatResponseContent response={response} />)

    expect(screen.getByText(`${responseType} answer`)).toBeInTheDocument()
  })
})
