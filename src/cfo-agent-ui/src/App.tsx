import { useState } from 'react'
import type { FormEvent, KeyboardEvent } from 'react'
import './App.css'
import { postChat, ChatApiError } from './features/chat/chatApi'
import { ChatResponseContent } from './features/chat/ChatResponseContent'
import { examplePrompts, type ConversationMessage } from './features/chat/types'

function App() {
  const [conversationId, setConversationId] = useState<string>()
  const [messages, setMessages] = useState<ConversationMessage[]>([])
  const [prompt, setPrompt] = useState('')
  const [error, setError] = useState<string>()
  const [isLoading, setIsLoading] = useState(false)

  async function sendMessage(message: string) {
    const trimmedMessage = message.trim()
    if (!trimmedMessage || isLoading) return

    setError(undefined)
    setPrompt('')
    setMessages((current) => [...current, { id: crypto.randomUUID(), role: 'user', content: trimmedMessage }])
    setIsLoading(true)

    try {
      const response = await postChat(trimmedMessage, conversationId)
      setConversationId(response.conversationId)
      setMessages((current) => [...current, { id: crypto.randomUUID(), role: 'assistant', content: response.answer, response }])
    } catch (exception) {
      setPrompt(trimmedMessage)
      setError(exception instanceof ChatApiError ? exception.message : 'The CFO assistant could not complete that request. Please try again.')
    } finally {
      setIsLoading(false)
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    void sendMessage(prompt)
  }

  function handlePromptKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      void sendMessage(prompt)
    }
  }

  return (
    <main className="chat-page">
      <header className="app-header">
        <div>
          <p className="eyebrow">Executive finance workspace</p>
          <h1>CFO AI Agent</h1>
        </div>
        <span className="mock-badge">Mock LLM</span>
      </header>
      <section className="prompt-library" aria-label="Example CFO prompts">
        <h2>Start with a CFO question</h2>
        <div>
          {examplePrompts.map((example) => <button className="prompt-button" key={example} type="button" disabled={isLoading} onClick={() => void sendMessage(example)}>{example}</button>)}
        </div>
      </section>
      <section className="conversation" aria-label="Conversation" aria-live="polite">
        {messages.length === 0 && !isLoading && <div className="empty-state"><h2>Your finance briefing starts here</h2><p>Ask about weekly sales, comparisons, products, forecasts, or indexed planning knowledge.</p></div>}
        {messages.map((message) => <article className={`message message-${message.role}`} key={message.id}><span className="message-label">{message.role === 'user' ? 'You' : 'CFO AI Agent'}</span>{message.response ? <ChatResponseContent response={message.response} /> : <p>{message.content}</p>}</article>)}
        {isLoading && <div className="loading-state" role="status">Preparing a verified CFO response...</div>}
        {error && <div className="error-state" role="alert">{error}</div>}
      </section>
      <form className="composer" onSubmit={handleSubmit}>
        <label htmlFor="cfo-prompt">Ask the CFO AI Agent</label>
        <textarea id="cfo-prompt" value={prompt} onChange={(event) => setPrompt(event.target.value)} onKeyDown={handlePromptKeyDown} placeholder="Ask a finance question..." rows={3} disabled={isLoading} />
        <div>
          <span>Press Enter to send. Shift+Enter adds a line.</span>
          <button type="submit" disabled={isLoading || !prompt.trim()}>{isLoading ? 'Working...' : 'Send'}</button>
        </div>
      </form>
    </main>
  )
}

export default App
