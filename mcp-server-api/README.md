# linnworks-api MCP server (public-safe, API-only)

A deliberately narrow MCP server: only `search_api`, `get_endpoint`, `get_model`,
`list_controllers`. No macro conventions, no golden examples, no internal
standards - this is the one safe to hand a URL to someone outside the team,
because there's nothing in it about how you write macros, only what Linnworks'
API looks like.

Standalone from `mcp-server/` (the full internal server) on purpose - sharing code
between them would risk something internal leaking into the public one later.

## Local use (stdio)

```
pip install -r requirements.txt
python server.py
```

Same client config shape as the internal server - point an MCP client's `command`
at `python` and `args` at this `server.py`.

## Running as a hosted service (HTTP)

```
python server.py --http --host 0.0.0.0 --port 8787
```

This serves MCP over `streamable-http` at `http://<host>:<port>/mcp` - the modern
MCP transport, supported by Claude, Cursor, and most current MCP clients (check
your specific client's docs for how it expects a remote/HTTP MCP server
configured - it's usually just a URL instead of a command/args pair).

**This has no authentication built in.** Anyone who can reach the port can call
these tools. Since the tools are read-only lookups over public-ish API
documentation, the actual exposure is low, but you should still put it behind
something before sharing the URL widely - see the main platform's hosting notes
for options (a shared token checked in each request is the minimum bar).

## TODO

- [ ] Add shared-token auth before wider distribution (deferred 2026-08-14 - okay
      for initial small-scale testing without it, revisit before real rollout)

## Hosting for testing (Cloudflare Tunnel from a PC you keep running)

See the walkthrough in the main project conversation / ask for it again - short
version: `python server.py --http --port 8787` in one terminal,
`cloudflared tunnel --url http://localhost:8787` in another, share the printed
`https://*.trycloudflare.com` URL. That URL changes every time the tunnel
restarts; for a stable URL, set up a named Cloudflare Tunnel against a domain
instead of the quick/free tunnel.
