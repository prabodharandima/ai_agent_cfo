import type { ChatResponse } from './types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5260'

export class ChatApiError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'ChatApiError'
  }
}

export async function postChat(message: string, conversationId?: string): Promise<ChatResponse> {
  let response: Response

  try {
    response = await fetch(`${apiBaseUrl}/api/chat`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message, conversationId }),
    })
  } catch {
    throw new ChatApiError('The CFO API could not be reached. Check that the local API is running.')
  }

  if (!response.ok) {
    throw new ChatApiError('The CFO assistant could not complete that request. Please try again.')
  }

  return (await response.json()) as ChatResponse
}
