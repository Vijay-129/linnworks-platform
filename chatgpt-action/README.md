# ChatGPT Custom GPT Action

REST wrapper over `mcp-shared/reference_tools.py`, for ChatGPT plans where a
Custom GPT's Actions is the only integration path (no native remote-MCP connector
support - that's Team/Enterprise only, and even there support varies). Custom GPT
Actions call a plain REST API described by an OpenAPI schema; they don't speak
MCP's JSON-RPC directly, which is the whole reason this wrapper exists instead of
just pointing a GPT at the MCP servers.

No business logic lives here - every route is a one-line call into
`mcp-shared/reference_tools.py`, the same code both MCP servers use. This can
never answer differently than they do for the same query.

## 1. Start the tunnel first (you need the URL before starting the server)

ChatGPT Actions require a real HTTPS URL - same approach as the MCP servers (see
`../mcp-server/README.md`):

```
cloudflared tunnel --url http://localhost:8790
```
(or `ngrok http 8790`.) That prints a `https://...` URL - copy it, you need it
for the next step.

## 2. Run the server, with that URL set

```
pip install -r requirements.txt
PUBLIC_BASE_URL=https://<your-tunnel-url> python server.py
```

**`PUBLIC_BASE_URL` is required** - without it, `/openapi.json` has no `servers`
entry and ChatGPT's Action importer rejects the schema with *"Could not find a
valid URL in `servers`"* (a real error hit and fixed 2026-08-20). The server
still starts without it, just prints a warning - if the GPT Builder rejects the
schema, check this first.

Serves on `http://0.0.0.0:8790`. Visit `http://localhost:8790/docs` to see/try
the endpoints locally, or `/openapi.json` for the raw schema - confirm
`"servers"` is actually populated before importing it. Your OpenAPI schema is
at `<your-tunnel-url>/openapi.json` - that's what you give the GPT Builder.

## 3. Create the Custom GPT

1. In ChatGPT: **Explore GPTs → Create** (or **My GPTs → Create a GPT**)
2. Go to the **Configure** tab
3. Give it a name (e.g. "Linnworks API"), and instructions along these lines:
   > You help with the Linnworks API. Before answering anything about a specific
   > endpoint, model, or controller, call the appropriate action
   > (`search_api`/`get_endpoint`/`get_model`/`list_controllers`) rather than
   > answering from general knowledge - these reflect Linnworks' actual published
   > API, which general knowledge may get wrong or have out of date.
4. Scroll to **Actions → Create new action**
5. Click **Import from URL**, paste `https://<your-tunnel-url>/openapi.json`
6. **Authentication**: set to **None** for now (this API has no auth yet -
   see the TODO in `../mcp-server-api/README.md`, same caveat applies here)
7. Save. Test it in the GPT's preview pane with a real question (e.g. "what
   endpoint do I use to get an order's fulfillment status in v2?") and confirm
   it actually calls an action (visible in the chat as "Talking to
   linnworks-api-lookup...") rather than answering from memory.

## Notes

- **This is a separate URL/tunnel from the MCP servers** - `mcp-server` and
  `mcp-server-api` serve MCP protocol on their own ports; this serves plain REST
  on its own port (8790 by default). Run all three if you want IDE clients (via
  MCP) and a Custom GPT (via this) working at the same time.
- Only 4 read-only endpoints exist here (matching `mcp-server-api`'s scope, not
  the full internal server's macro/golden-example tools) - deliberately, so a
  Custom GPT built from this can be shared the same way `mcp-server-api` can.
- No auth yet, same as both MCP servers - fine for testing, revisit before wide
  distribution.
