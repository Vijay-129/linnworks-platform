# Macro Conventions

Derived from `golden_examples/` (3 real approved macros, supplied 2026-08-13) by
comparing them against each other — every rule below cites which example
demonstrates it and which example(s) violate it. Read `golden_examples/README.md`
for the full per-file breakdown; this file is the checklist to write new macros
against.

## 1. Structure

Every macro is a `public class X : LinnworksMacroBase` with `Execute(...)` as the
entry point (this is what the Linnworks macro engine calls; see
`references/macro/linnworks-api/overview.md` for what `LinnworksMacroBase` provides -
`Api`, `Logger`, `RunTime`, `Configuration`, `SettingsHelper`, all pre-injected).

```csharp
public void Execute(/* parameters */)
{
    Logger.WriteInfo("<MacroName> started.");   // see rule 2

    try
    {
        // main logic
    }
    catch (Exception ex)
    {
        Logger.WriteError($"<MacroName> failed: {ex}");
    }
    finally
    {
        Logger.WriteInfo("<MacroName> finished.");  // see rule 2
    }
}
```

All three golden examples follow this shape. Deviating from it (e.g. no top-level
try/catch, logic scattered across the entry point instead of delegated to private
methods) is a red flag when reviewing or generating a macro.

## 2. Two logs are mandatory: start and end

Every macro must log once at the very start of `Execute` and once at the very end
(in a `finally`, so it logs even on failure). All three examples do this:

- `01`: `"Shopify payment method mapping macro started."` / `"...finished."`
- `02`: `"ContainerEtaFolderSync started."` / `"...finished in {elapsed} second(s)."`
- `03`: `"Macro started. Version: {version}."` / a summary-counts line + `"Macro finished."`

`03`'s pattern (log a summary of outcomes — parked/skipped/failed counts — right
before the final "finished" line) is worth copying for any macro that processes a
batch of records: it turns the log into an audit trail, not just a heartbeat.

## 3. Logging: human-readable IDs only, never raw GUIDs

Log `order.NumOrderId` (int), not `order.OrderId` (Guid). For inventory, log SKU,
not `StockItemId`. A GUID in a log line is useless to a human debugging a run - the
NumOrderId/SKU is what someone can actually search the Linnworks UI for.

- **`03` is the reference implementation**: every log line uses `NumOrderId`, with
  zero exceptions, across a genuinely complex macro (correlation, validation,
  idempotent markers).
- **`01` and `02` both violate this in a couple of places** — see
  `golden_examples/README.md` for the exact lines. This is the single most common
  slip across the examples we have; check for it specifically when reviewing a
  macro or generating one.

## 4. Rate-limit safety is mandatory, not optional

**Every `Api.*` call must go through a wrapper that does both of these:**

1. **Proactive pacing** — a minimum spacing between *any* two API calls (not just
   within one loop), so the macro doesn't burst past the rate limit in the first
   place. `02`'s `PaceApiCall()` (lock-protected, ~550ms minimum spacing) is the
   reference pattern.
2. **Reactive retry on HTTP 429** with exponential backoff, walking the *full*
   exception chain (not just the top-level exception — Linnworks SDK errors can be
   wrapped). `02`'s `IsHttp429` checks both `WebException`/`HttpWebResponse.StatusCode`
   and a string-match fallback on the message.

`01` has **no rate-limit handling at all** — copy `02`'s `ExecuteApi<T>` wrapper
(pacing + backoff combined) for any new macro, don't start from `01`'s pattern.
`03` has retry but not proactive pacing - if a new macro makes several calls per
record (most do), add both, not just retry.

**Detection detail post-2026-08-13**: `LinnworksAPI/Core/Factory.cs` was fixed so v1
errors now surface as `"Linnworks API error {code} calling {path}: {message}"`
instead of a generic .NET exception message, with the original `WebException`
preserved as `InnerException`. A 429-detection helper should check for `"error 429"`
in the message text (not the old `"(429)"` pattern) *and* walk to the inner
`WebException`/`HttpWebResponse.StatusCode` - do both, since either the wrapper text
or the original exception could be what you see depending on which layer threw.

Reference pattern (adapt `MinimumApiSpacingMilliseconds` to the macro's actual call
volume):

```csharp
private static readonly object RateLimitLock = new();
private static DateTime _lastApiCallUtc = DateTime.MinValue;
private const int MinimumApiSpacingMilliseconds = 550;

private static void PaceApiCall()
{
    lock (RateLimitLock)
    {
        var elapsed = (DateTime.UtcNow - _lastApiCallUtc).TotalMilliseconds;
        if (elapsed < MinimumApiSpacingMilliseconds)
            Thread.Sleep(MinimumApiSpacingMilliseconds - (int)elapsed);
        _lastApiCallUtc = DateTime.UtcNow;
    }
}

private T ExecuteApi<T>(string operationName, Func<T> operation)
{
    const int maxAttempts = 5;
    var retryDelaysSeconds = new[] { 5, 15, 30, 60 };
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        PaceApiCall();
        try { return operation(); }
        catch (Exception ex) when (IsHttp429(ex) && attempt < maxAttempts)
        {
            Thread.Sleep(TimeSpan.FromSeconds(retryDelaysSeconds[attempt - 1]));
        }
    }
    throw new InvalidOperationException($"{operationName} failed without returning a result.");
}

private static bool IsHttp429(Exception exception)
{
    for (var current = exception; current != null; current = current.InnerException)
    {
        if (current is WebException webEx && webEx.Response is HttpWebResponse http && (int)http.StatusCode == 429)
            return true;
        if (current.Message.Contains("error 429", StringComparison.OrdinalIgnoreCase) ||
            current.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}
```

## 5. Choosing an endpoint and filter: server-side first

Before writing a macro that fetches records, check whether the API can filter/page
server-side rather than fetching everything and filtering in C#:

- `02` fetches purchase orders with `Search_PurchaseOrder2Request { Status =
  PurchaseOrderStatus.OPEN, ... }` — filters by status server-side, then pages
  (`PageNumber`/`EntriesPerPage`) instead of pulling the whole PO history.
- `03` resolves a named view to a `ViewId` first (`GetViewStats`), then calls
  `OpenOrders.GetOpenOrderIds` scoped to that view + location, instead of pulling
  every open order and filtering client-side.

When helping someone write a new macro: identify their actual scope (a date range?
a location? a status? a specific view?) and recommend the narrowest server-side
filter/endpoint that covers it, the same way `02` and `03` do — don't default to "get
everything, filter in memory" when a scoped/paged call exists. Check `search_api`/
`get_endpoint` (MCP) or `references/api/v1/<Controller>.md` for what filters an
endpoint actually supports before assuming one doesn't exist.

## 6. Idempotency for anything that mutates state

Both `01` and `03` use a durable marker (an order identifier tag, or an extended
property) to detect "have I already done this" before mutating, rather than relying
on in-memory state that resets between runs. `03` goes further: it re-reads and
verifies the marker was actually persisted before proceeding, to handle two
overlapping executions racing on the same order. Any scheduled macro that mutates
orders needs an idempotency check — a scheduled macro that reprocesses the same
record every run because it has no memory of prior runs is a bug waiting to
duplicate a note, an email, or a state change.
