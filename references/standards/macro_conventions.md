# Macro Conventions

Derived from `golden_examples/` (real approved macros) by comparing them against
each other — every rule below cites which example demonstrates it and which
example(s) violate it. Read `golden_examples/README.md` for the full per-file
breakdown; this file is the checklist to write new macros against.

## 0. Language features: macros compile separately from LinnworksAPI itself

`LinnworksAPI/LinnworksAPI.csproj` targets netstandard2.0/C# 7.3 (no nullable
reference types, no `init`, etc.) - but that constraint is about the **SDK
library**, not about what a macro file itself can use. Linnworks' own macro
engine compiles macro source separately, and real approved macros already use
features far newer than C# 7.3:

- **Nullable reference types** (`#nullable enable`, `string?`) - used throughout
  `02_ContainerEtaFolderSync.cs` and `03_PickListMonitoring.cs`.
- **`init`-only setters** (C# 9) - used throughout `02`'s internal record-like
  classes (`PurchaseOrderEtaSnapshot`, `FolderChangePlan`, etc).

Don't apply the SDK's C# 7.3 constraint to macro code - it doesn't apply. What
*hasn't* been confirmed yet (no real example uses them): `record`/`record struct`
declarations, `required` members, collection expressions (`[1, 2, 3]`), raw
string literals. If you want to use one of these in a real macro, the safe move
is a small standalone test macro exercising just that feature, run once against
a real account, before relying on it in production code - don't assume based on
the C# 7.3 SDK constraint, and don't assume based on the confirmed features above
either (a feature working doesn't mean every newer feature does).

## 0.1 GUIDs default to `Guid.Empty` - and `Guid.Empty` is a REAL location, not "all"

**Do not write `locationId ?? Guid.Empty` (or any equivalent) expecting that to mean
"no location filter" / "all locations".** Live-tested 2026-08-14 against a real
30-location account:

| Call | `TotalEntries` |
|---|---|
| `GetOpenOrders(LocationId = Guid.Empty)` | **1,871** |
| `GetOpenOrders(LocationId = <the account's "Default" location's real ID>)` | **1,871** (identical) |
| Sum of `GetOpenOrders` called once per real location (30 locations) | **23,520** |

`Guid.Empty` is not a wildcard - it happens to be the literal `StockLocationId` of
whatever location is named "Default" in this account (confirmed separately via
`Locations.GetLocation(Guid.Empty)`, which returns that specific location's
record, not an error or a merged view). Passing it as a filter silently limits
you to that one location - **92% of this account's open orders were invisible**
to a macro that did this.

This is not hypothetical: **`golden_examples/03_PickListMonitoring.cs` has this
exact bug.** Its own doc comment says `location` - *"Leave empty to scan all
locations"* - but its implementation
(`LocationId = locationId ?? Guid.Empty`, in `FetchOpenOrderIds`) does the
opposite. See `golden_examples/README.md` for the annotation.

**The correct pattern**: if "all locations" is the actual intent, call
`Inventory.GetStockLocations()` once and loop, issuing one scoped call per real
location (or per relevant subset) - never rely on an empty/default Guid to mean
"unfiltered." This generalizes beyond `GetOpenOrders` - treat any location-typed
(or similarly-typed) filter parameter with the same suspicion until checked; the
API doesn't reliably use "empty means unset" semantics.

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

## 1.1 One `Execute` method - a macro is Rule *or* Scheduled, never both

**A macro has exactly one `Execute` method.** Linnworks resolves a macro as one
trigger type with one signature - it does not pick between overloads depending on
how the macro was invoked. Don't write both
`Execute(Guid[] OrderIds, ...)` (rule-triggered) and
`Execute(string someParam = "", ...)` (scheduled) as overloads on the same class
expecting Linnworks to route to the right one.

Confirmed by the team 2026-08-14: a submitted macro (`StaleShippingLabelGuardian.cs`)
had exactly this shape - two `Execute` overloads, one per trigger type - which is
invalid. It compiles as valid C# (overloading is a language feature), but that
doesn't mean Linnworks' macro engine treats it as a dual-mode macro; it isn't one.

**When a requirement doesn't specify Rule vs Scheduled, decide - don't hedge by
writing both.** Read the actual trigger semantics being described (a condition on
individual orders as they arrive → Rule; "check this recurring state periodically"
→ Scheduled) and pick one. State which one you picked and why before writing code
(see the platform's general "announce your plan first" convention). If the
requirement is genuinely ambiguous even after reading it carefully, ask which one
instead of guessing or writing both. See `references/macro/patterns/rule_macro.md`
and `scheduled_macro.md` for the two shapes.

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

## 7. A macro run is a ~5 minute budget - design for it, don't just hope

A macro isn't a long-lived service; it runs, stops, and gets invoked again later
(on a schedule, or on the next rule trigger). Everything below follows from
treating that window as a hard constraint, not an afterthought:

- **Fetch rarely-changing reference data once, at the top of the run, not per
  record.** Countries, payment methods, shipping methods, views, the location
  list - anything that isn't going to change mid-run. A macro that calls
  `Api.Orders.GetPaymentMethods()` (or similar) inside a per-order loop is
  spending API calls and wall-clock time on the same answer, repeatedly, inside a
  budget that's already tight. Fetch once before the loop, pass the result in.
- **Use a batch/bulk endpoint instead of one-call-per-record wherever the SDK has
  one.** `Api.Orders.GetOrdersById(List<Guid>)` instead of looping
  `GetOrderById` per order; `AssignToFolder`/`UnassignToFolder` given a batch of
  order IDs instead of one order at a time - `02`'s folder-change logic groups by
  target folder and applies each group in one batched call rather than looping
  per order. Check `search_api`/`get_endpoint` (MCP) for a `...ByIds`/batch
  variant before writing a per-record loop against a single-record endpoint.
- **Prefer `List<T>`/`Dictionary<K,V>` over a bespoke class/enum/struct for
  macro-internal data shapes.** `LinnworksAPI` already provides the domain types
  (`OrderDetails`, `OpenOrder`, etc.) - a macro is a short-run script working with
  those, not a long-lived application that benefits from its own rich domain
  model. Introducing a new `enum`/`struct`/class to represent something a
  `Dictionary<string, string>` or a tuple could hold adds a maintenance surface
  for no benefit in a script that runs for a few minutes and exits. This is a
  judgment call, not an absolute - a real state machine with named outcomes (e.g.
  `03`'s `ProcessingOutcome` enum, used for control flow and logging) earns its
  keep; a one-off snapshot struct usually doesn't.
