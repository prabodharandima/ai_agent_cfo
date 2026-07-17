import type { ReactNode } from 'react'
import type { ChatResponse } from './types'

type RecordValue = Record<string, unknown>

function isRecord(value: unknown): value is RecordValue {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function read(record: RecordValue | undefined, key: string): unknown {
  if (!record) return undefined
  const matchedKey = Object.keys(record).find((candidate) => candidate.toLowerCase() === key.toLowerCase())
  return matchedKey ? record[matchedKey] : undefined
}

function asNumber(value: unknown): number | undefined {
  return typeof value === 'number' ? value : undefined
}

function asString(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined
}

function asRecords(value: unknown): RecordValue[] {
  return Array.isArray(value) ? value.filter(isRecord) : []
}

function formatCurrency(value: unknown): string {
  const amount = asNumber(value)
  return amount === undefined
    ? 'Not available'
    : new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(amount)
}

function formatNumber(value: unknown): string {
  const number = asNumber(value)
  return number === undefined ? 'Not available' : new Intl.NumberFormat('en-US').format(number)
}

function formatPercent(value: unknown): string {
  const percent = asNumber(value)
  return percent === undefined ? 'Not comparable' : `${percent.toFixed(1)}%`
}

function KpiCard({ label, value }: { label: string; value: string }) {
  return <div className="kpi-card"><span>{label}</span><strong>{value}</strong></div>
}

function SummaryCards({ summary }: { summary: RecordValue }) {
  return (
    <section className="kpi-grid" aria-label="Sales summary metrics">
      <KpiCard label="Net revenue" value={formatCurrency(read(summary, 'netRevenue'))} />
      <KpiCard label="Gross profit" value={formatCurrency(read(summary, 'grossProfit'))} />
      <KpiCard label="Gross margin" value={formatPercent(read(summary, 'grossMarginPercent'))} />
      <KpiCard label="Orders" value={formatNumber(read(summary, 'orderCount'))} />
      <KpiCard label="Units sold" value={formatNumber(read(summary, 'quantitySold'))} />
      <KpiCard label="Average order" value={formatCurrency(read(summary, 'averageOrderValue'))} />
    </section>
  )
}

function Comparison({ data }: { data: RecordValue }) {
  const currentValue = read(data, 'currentWeek')
  const previousValue = read(data, 'previousWeek')
  const current = isRecord(currentValue) ? currentValue : undefined
  const previous = isRecord(previousValue) ? previousValue : undefined
  const direction = asString(read(data, 'direction')) ?? 'unchanged'
  return (
    <section className="comparison" aria-label="Week-over-week comparison">
      <div><span>Current week</span><strong>{formatCurrency(read(current, 'netRevenue'))}</strong></div>
      <div><span>Previous week</span><strong>{formatCurrency(read(previous, 'netRevenue'))}</strong></div>
      <div className={`trend trend-${direction.toLowerCase()}`}><span>Revenue change</span><strong>{formatCurrency(read(data, 'netRevenueChange'))} ({formatPercent(read(data, 'netRevenueChangePercentage'))})</strong></div>
    </section>
  )
}

function ProductsTable({ data }: { data: RecordValue }) {
  const products = asRecords(read(data, 'products'))
  return (
    <table aria-label="Top products">
      <thead><tr><th>Product</th><th>Units</th><th>Revenue</th><th>Gross profit</th></tr></thead>
      <tbody>{products.map((product) => <tr key={asString(read(product, 'productCode')) ?? asString(read(product, 'productName'))}><td><strong>{asString(read(product, 'productName')) ?? 'Product'}</strong><small>{asString(read(product, 'productCode'))}</small></td><td>{formatNumber(read(product, 'quantitySold'))}</td><td>{formatCurrency(read(product, 'netRevenue'))}</td><td>{formatCurrency(read(product, 'grossProfit'))}</td></tr>)}</tbody>
    </table>
  )
}

function Forecast({ data }: { data: RecordValue }) {
  const forecasts = asRecords(read(data, 'forecasts'))
  const largestExpected = Math.max(...forecasts.map((forecast) => asNumber(read(forecast, 'expectedNetRevenue')) ?? 0), 1)
  return (
    <section className="forecast" aria-label="Five-year sales forecast">
      <div className="forecast-chart" aria-label="Expected net revenue forecast chart">
        {forecasts.map((forecast) => {
          const expected = asNumber(read(forecast, 'expectedNetRevenue')) ?? 0
          return <div className="forecast-bar" key={asNumber(read(forecast, 'year'))}><span style={{ height: `${Math.max((expected / largestExpected) * 100, 5)}%` }} /><small>{asNumber(read(forecast, 'year'))}</small></div>
        })}
      </div>
      <table aria-label="Five-year sales forecast">
        <thead><tr><th>Year</th><th>Conservative</th><th>Expected</th><th>Optimistic</th></tr></thead>
        <tbody>{forecasts.map((forecast) => <tr key={asNumber(read(forecast, 'year'))}><td>{formatNumber(read(forecast, 'year'))}</td><td>{formatCurrency(read(forecast, 'conservativeNetRevenue'))}</td><td>{formatCurrency(read(forecast, 'expectedNetRevenue'))}</td><td>{formatCurrency(read(forecast, 'optimisticNetRevenue'))}</td></tr>)}</tbody>
      </table>
    </section>
  )
}

function SupportingInformation({ response }: { response: ChatResponse }) {
  const blocks: ReactNode[] = []
  if (response.assumptions.length > 0) blocks.push(<section key="assumptions"><h3>Assumptions</h3><ul>{response.assumptions.map((item) => <li key={item}>{item}</li>)}</ul></section>)
  if (response.warnings.length > 0) blocks.push(<section className="warnings" key="warnings"><h3>Warnings</h3><ul>{response.warnings.map((item) => <li key={item}>{item}</li>)}</ul></section>)
  if (response.sources.length > 0) blocks.push(<section key="sources"><h3>Sources</h3><ul className="sources">{response.sources.map((source) => <li key={`${source.documentId}-${source.section}`}><strong>{source.documentName}</strong><span>{source.section}{source.period ? ` | ${source.period}` : ''}</span></li>)}</ul></section>)
  return blocks.length > 0 ? <div className="supporting-information">{blocks}</div> : null
}

export function ChatResponseContent({ response }: { response: ChatResponse }) {
  const data = isRecord(response.structuredData) ? response.structuredData : undefined
  let visual: ReactNode = null
  if (data && response.responseType === 'sales_summary') visual = <SummaryCards summary={data} />
  if (data && response.responseType === 'sales_comparison') visual = <Comparison data={data} />
  if (data && response.responseType === 'top_products') visual = <ProductsTable data={data} />
  if (data && response.responseType === 'forecast') visual = <Forecast data={data} />

  return (
    <div className="assistant-response">
      <p className="assistant-answer">{response.answer}</p>
      {visual}
      <div className="response-meta"><span>Agents: {response.agentNames.join(', ')}</span>{response.dataPeriod?.label && <span>Period: {response.dataPeriod.label}</span>}</div>
      <SupportingInformation response={response} />
    </div>
  )
}
