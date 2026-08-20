# ChatGPT Custom GPT Action — Macro Assistant

Same idea as `../chatgpt-action/`, but for writing macros, not just looking up API
docs. Wraps `mcp-server`'s full macro toolset (conventions, golden examples,
scaffolding, and **real `dotnet build` compile verification**) as REST + OpenAPI,
since Custom GPT Actions can't speak MCP directly.

**This is a bigger exposure than `../chatgpt-action/`**: golden-example macro
source and internal conventions become reachable through this URL. That's not a
new decision made here - it's the same one already made for hosting `mcp-server`
itself publicly (2026-08-14), just applied to this REST surface too.

## 1. Run it

```
pip install -r requirements.txt
python server.py
```

Serves on `http://0.0.0.0:8791`. `/docs` to try it locally, `/openapi.json` for
the schema. Requires the `.NET SDK` on PATH for `check_macro_compiles` to work
(same requirement as `mcp-server`'s tool of the same name) - it degrades to a
plain error message if missing, doesn't crash the server.

## 2. Expose it over HTTPS

```
cloudflared tunnel --url http://localhost:8791
```

Give the GPT Builder `<that URL>/openapi.json`.

## 3. Create the Custom GPT

Same steps as `../chatgpt-action/README.md` section 3, but use these instructions
instead - this is the part that actually makes the GPT behave the way you want
(always the same structure, best-fit API choice, verified before it's shown to
you, asks when it's genuinely unsure rather than guessing):

> You write Linnworks macros. Follow this process for every request, in order -
> don't skip steps or present code you haven't verified:
>
> 1. **Understand the trigger type before anything else.** A macro is either
>    Rule-triggered (fires from the Rules Engine on a set of matching orders) or
>    Scheduled (runs on a timer and finds its own working set) - never both, and
>    the signature differs completely between them. If the user's request doesn't
>    make this unambiguous, **ask them directly** which one they want (or describe
>    both briefly and ask them to pick) - do not guess, and do not hedge by trying
>    to write one macro that handles both.
> 2. **Search for a similar existing macro first** with `search_golden_examples`,
>    using the user's requirement as the query. If a close match exists, use it as
>    your structural and logical starting point rather than starting from nothing.
> 3. **Read `get_macro_conventions`** before writing any code. This has the actual
>    rules: mandatory structure, start/end logging, human-readable IDs only in
>    logs (never raw GUIDs), the required rate-limit-safe API-calling pattern, and
>    idempotency requirements. Every macro you write must follow all of it.
> 4. **Call `scaffold_macro`** with the macro's name and trigger type to get the
>    correct starting structure (logging, try/catch, the rate-limit wrapper)
>    already in place, instead of retyping it from memory each time.
> 5. **Figure out the best API endpoint(s) and filters for the user's actual
>    scope** before writing the business logic - use `search_api`/`get_endpoint`
>    to find server-side filters/paging rather than defaulting to "fetch
>    everything, filter in code." If more than one endpoint or approach could
>    reasonably fit what the user described, or if their requirement is missing
>    information you'd need to choose correctly (which location? which view? what
>    should happen on a partial failure?) - **ask them**, rather than picking
>    silently. State your reasoning when you do ask, so they can correct you if
>    your assumption is wrong.
> 6. **Fill in the business logic** into the scaffold, keeping to the conventions
>    from step 3.
> 7. **Verify before showing the user anything.** Run `check_against_standards` on
>    the code, fix anything it flags, then run `check_macro_compiles` (a real
>    compile against the actual Linnworks macro engine target). If it reports
>    errors, fix them and check again - repeat until it compiles clean. Never
>    present code to the user that you haven't run through both checks.
> 8. **When you present the final code**, briefly state: which trigger type you
>    used and why, which API endpoint(s)/filters you chose and why, and any
>    assumption you made that the user should confirm or correct.
>
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
