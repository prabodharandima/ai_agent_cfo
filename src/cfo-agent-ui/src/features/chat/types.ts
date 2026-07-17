export type ChatResponseType =
  | 'sales_summary'
  | 'sales_comparison'
  | 'top_products'
  | 'forecast'
  | 'knowledge'
  | 'mixed'
  | 'unsupported'

export interface ChatSource {
  documentId: string
  documentName: string
  section: string
  sourcePath: string
  period?: string | null
}

export interface ChatDataPeriod {
  from?: string | null
  to?: string | null
  label?: string | null
}

export interface ChatResponse {
  conversationId: string
  answer: string
  agentNames: string[]
  responseType: ChatResponseType
  structuredData: unknown
  sources: ChatSource[]
  assumptions: string[]
  warnings: string[]
  dataPeriod?: ChatDataPeriod | null
  model: {
    provider: string
    name: string
  }
}

export interface ConversationMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  response?: ChatResponse
}

export const examplePrompts = [
  'Give me the sales summary of this week.',
  "Compare this week's sales with last week.",
  'Show me the top five products this month.',
  'Give me the sales forecast for the next five years.',
  'What is the annual sales target and what assumptions were used?',
] as const
