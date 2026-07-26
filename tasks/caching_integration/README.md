# CFO AI Agent Caching Integration

Execute in order, one Codex session per task:

1. TASK-CACHE-001-foundation-and-finance.md
2. TASK-CACHE-002-rag-retrieval.md
3. TASK-CACHE-003-embeddings.md
4. TASK-CACHE-004-mcp-tool-discovery.md
5. TASK-CACHE-005-llm-classification.md

Design rule: internal application code depends on a provider-neutral cache abstraction. HybridCache is the cache API and Redis is the distributed backing store. Replacing Redis must require composition/configuration changes only.

Keep tests focused. Do not cache failures, timeouts, cancellation, secrets, raw prompts, or final chat responses.
