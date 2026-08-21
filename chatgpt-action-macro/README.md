# ChatGPT Custom GPT Action — Macro Assistant

Same idea as `../chatgpt-action/`, but for writing macros, not just looking up API
docs. Wraps `mcp-server`'s full macro toolset (conventions, golden examples,
scaffolding, and **real `dotnet build` compile verification**) as REST + OpenAPI,
since Custom GPT Actions can't speak MCP directly.

**This is a bigger exposure than `../chatgpt-action/`**: golden-example macro
source and internal conventions become reachable through this URL. That's not a
new decision made here - it's the same one already made for hosting `mcp-server`
itself publicly (2026-08-14), just applied to this REST surface too.

## 1. Start the tunnel first (you need the URL before starting the server)

```
cloudflared tunnel --url http://localhost:8791
```
(or `ngrok http 8791` if you're using ngrok instead - either works.) Copy the
printed `https://...` URL - you need it for the next step.

## 2. Run the server, with that URL set

```
pip install -r requirements.txt
PUBLIC_BASE_URL=https://<your-tunnel-url> python server.py
```

**`PUBLIC_BASE_URL` is required for ChatGPT's Action importer to accept the
schema** - without it, `/openapi.json` has no `servers` entry and the importer
rejects it with *"Could not find a valid URL in `servers`"* (a real error hit
and fixed 2026-08-20 - see `server.py`'s comment for why). The server prints a
warning to stderr if you forget it, but it will still start - so if the GPT
Builder rejects the schema, check this first.

Serves on `http://0.0.0.0:8791`. `/docs` to try it locally, `/openapi.json` for
the schema (confirm `"servers"` is actually populated in it before importing).
Requires the `.NET SDK` on PATH for `check_macro_compiles` to work (same
requirement as `mcp-server`'s tool of the same name) - it degrades to a plain
error message if missing, doesn't crash the server.

Give the GPT Builder `<your-tunnel-url>/openapi.json`.

## 3. Create the Custom GPT

Same steps as `../chatgpt-action/README.md` section 3, but use these instructions
instead - this is the part that actually makes the GPT behave the way you want
(always the same structure, best-fit API choice, verified before it's shown to
you, asks when it's genuinely unsure rather than guessing):

> You write Linnworks macros. Follow this process for every request, in order -
> don't skip steps or present code you haven't verified:
>
> 1. **Primary Entry Point: Call `find_relevant_operations` first.**
>    Pass the user's task goal as `{"goal": "..."}`. This tool returns:
>    - The best matching workflow (if any) and relevant domain concepts.
>    - Candidate API operations (`Controller.Method`) and reasons.
>    - Critical gotchas with verified source provenance (e.g. `Guid.Empty` location rules, `ViewId` requirements, locking rules).
>    - `needs_more_information`: check if any ambiguities are marked `blocking: true` (e.g. ambiguous write mutation). If blocking, **ask the user directly** to clarify before generating code.
> 2. **Understand the trigger type.** A macro is either Rule-triggered (fires from
>    the Rules Engine on matching orders) or Scheduled (runs on a timer and finds
>    its own working set) - never both. If unclear, ask the user to clarify.
> 3. **Deep-dive on concepts and workflows when needed:**
>    - Call `get_linnworks_workflow(name)` if a matching workflow was identified.
>    - Call `get_linnworks_concept(name)` to inspect domain lifecycle, identifiers, and models.
>    - Call `search_golden_examples` to find real reference macro implementations.
> 4. **Read `get_macro_conventions`** before writing code. Adhere to all rules:
>    logging `NumOrderId` instead of raw GUIDs, wrapping API calls in rate-limit handlers,
>    one documented config parameter per scalar value (no blobs), and idempotency guards.
> 5. **Look up exact API signatures and verify usage:**
>    - Call `get_endpoint(controller)` and `get_model(model)` for exact method signatures and field tables.
>    - Call `verify_api_usage(controller, method, model, fields)` BEFORE generating code to confirm the planned controller, method, and field names exist.
> 6. **Call `scaffold_macro`** with the macro name, trigger type, and `config_params`
>    (`"name:description"` pairs separated by `|`). This generates the mandatory
>    scaffolding (logging, try/catch, rate-limit wrapper, XML doc comments).
> 7. **Fill in the business logic** into the scaffold, following the workflow steps and conventions.
>    Keep it compact, use batch endpoints where available, and respect execution time budgets.
> 8. **Verify before showing the user anything:**
>    - Run `check_against_standards` on the code and fix anything flagged.
>    - Run `check_macro_compiles` (real dotnet build against `net10.0` / C# latest). Repeat until it compiles with 0 errors.
> 9. **Present the final code:**
>    - State the trigger type, chosen APIs, and any verified assumptions.
>    - Suggest concrete test scenarios (normal case, empty match, and high-risk edge cases).
> If at any point a requirement is genuinely ambiguous and guessing wrong would
> mean rewriting significant logic later, stop and ask instead of proceeding on
> an assumption.

Then: **Actions → Create new action → Import from URL** → paste
`https://<your-tunnel-url>/openapi.json` → **Authentication: None** (no auth yet,
same TODO as both MCP servers) → Save.

Test it with a real, slightly underspecified request (e.g. "write me a macro that
parks orders when their pick list is printed") and confirm it actually asks a
clarifying question about trigger type or scope rather than guessing - that's the
behavior this whole setup exists to produce.

## Notes

- This is a **third** separate tunnel/process from `../chatgpt-action/` and the
  two MCP servers - run whichever combination you actually need at once.
- `check_macro_compiles` takes a few real seconds per call (it invokes the actual
  compiler) and is lock-protected against concurrent calls on this process, so
  under concurrent GPT users it serializes rather than corrupting - just slower,
  not unsafe.
- No auth yet. Fine for testing with people you trust; revisit before wider
  distribution, same as everything else hosted so far.
