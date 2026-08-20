# Macro Conventions

Derived from `golden_examples/` (real approved macros) by comparing them against
each other — every rule below cites which example demonstrates it and which
example(s) violate it. Read `golden_examples/README.md` for the full per-file
breakdown; this file is the checklist to write new macros against.

This file lives on the MCP/server side, not inside any one agent's prompt -
Claude (via `mcp-server`), Antigravity, and ChatGPT (via `chatgpt-action-macro`,
which wraps the same `get_macro_conventions`/`check_against_standards` tools) all
read the identical rules from here. Anything **mechanically checkable** (a regex
over the code - see `check_against_standards`) belongs here, because it then
applies to every agent automatically and gets enforced, not just suggested.
Anything **behavioral** (ask the user when a requirement is ambiguous, follow a
particular order of steps, never present unverified code) can only be enforced by
each agent's own instructions/system prompt - an MCP tool response is data an
agent chooses to act on, it can't compel a calling order. `chatgpt-action-macro/README.md`'s
GPT instructions text is where that half lives for ChatGPT specifically.

## Process every agent should follow, in order

1. **Fully understand the requirement first** - trigger type (see 1.1), scope,
   what "done" looks like, what should happen on partial failure. If anything
   here is genuinely ambiguous, ask before writing code (see 1.1's guidance on
   asking vs guessing) - guessing wrong here means rewriting logic later.
2. **Find the best-fit API and filter for that scope** (rule 5) - server-side
   filtering/paging over "fetch everything, filter in code", and a batch/bulk
   endpoint over one-call-per-record (rule 7). Check the actual request/response
   shape with `get_model`/`get_endpoint` before writing the call (rule 9) - don't
   assume a field name or required-ness from memory.
3. **Design the full logic, including edge cases**, before it's considered done:
   empty/zero-length working sets, a record that fails mid-batch (per-order
   isolation - rule 1.1/rule_macro.md), idempotency (rule 6), and the specific
   `Guid.Empty`-is-not-"all" trap (section 0.1).
4. **Verify before presenting anything** - `check_against_standards` then
   `check_macro_compiles`, fix what either flags, repeat until clean.
5. **Suggest concrete test scenarios to the user** when you hand over the
   finished macro - not just "let me know if it works." At minimum: the normal
   case, an empty/no-match case, and whichever edge case from step 3 is riskiest
   to get wrong for this specific macro (e.g. "test with an order at a
   non-Default location" for anything location-scoped).

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

Don't apply the SDK's C# 7.3 constraint to macro code - it doesn't apply.

**Confirmed 2026-08-19** against a real account (a 16-feature probe macro, run to
completion as a Scheduled macro - see below for why Scheduled and not Rule -
every one of these logged successfully):

- `record` / `record struct` declarations
- `required` members
- Collection expressions (`[1, 2, 3]`)
- Raw string literals (`"""..."""`)
- Pattern matching as a group: switch expressions, list patterns
  (`[first, .., last]`), relational patterns (`>= 90 and <= 100`)
- Target-typed `new()`
- Null-coalescing assignment (`??=`)
- Index/range operators (`^1`, `1..3`)
- `using` declarations (`using var x = ...`)
- `params` collections (`params IEnumerable<T>`, not just `params T[]`)
- `System.Threading.Lock` (C# 13) - **actionable**: rule 4's rate-limit pattern
  below still uses a plain `object` + `lock(...)`; `Lock` can replace it if/when
  that pattern is next touched, not required retroactively.
- `field`-backed properties (the `field` keyword, C# 14)
- Null-conditional assignment (`obj?.Prop = value`, C# 14)
- Extension members (`extension(...)` blocks, C# 14 / .NET 10's headline feature)

Combined with the nullable reference types + `init`-only setters already
confirmed above, the macro engine's real target is effectively **C# 14 / .NET
10**, not just "newer than C# 7.3." `compile_check/CompileCheck.csproj`
(`net10.0`, `LangVersion latest`, `Nullable enable`) matches this - it was a
guess as of the previous version of this note, it is now confirmed.

**Not safe to use**: `file`-scoped types (the `file` modifier). Confirmed
2026-08-19 - a `file static class` produced `CS9068: File-local type '...' must
be declared in a file with a unique path. Path '' is used in multiple files.`
Whatever compiles pasted macro source appears to treat it as a pathless/anonymous
unit, and `file`'s visibility model depends on a unique file path to key off of.
Use `internal` instead - same practical effect for macro-internal helper types,
no dependency on file-path uniqueness.

**Open question, not resolved**: a Rule macro (`Execute(Guid[] OrderIds)`,
attached to a real Rule condition that matched a live order) produced **zero**
log output and no execution-history record at all - not even a failure entry -
both for the full probe and for a maximally trivial one-line sanity-check
version. The identical macro body run as a **Scheduled** macro (`Execute(string
someParam = "")`) logged correctly on its first cycle. This does not mean Rule
macros are broken - the cause wasn't isolated (could be the specific rule
condition/attachment in this account, a delay, or something else entirely) - but
it does mean **don't assume a Rule macro is executing just because it saved
without error**. Verify a new Rule macro actually produces log output against a
real matching order before relying on it; if it doesn't, don't debug the macro's
C# first - check the rule's condition/attachment.

**Also observed, not a hard rule**: during this testing, Linnworks' macro editor
required the exact namespace/class shape `namespace LinnworksMacro { public
class LinnworksMacro : LinnworksMacroHelpers.LinnworksMacroBase { ... } }`
(fully-qualified base class, no `using LinnworksMacroHelpers;`) for the specific
macro being edited at the time - but this is very likely per-macro (matching
whatever name that macro was given), not a fixed platform-wide name every macro
must use. `golden_examples/01` (`LinnworksMacro._2349` /
`Shopify_PaymentMethod_Mapping_MacroGraphQL`) and `02`
(`Rishvi.ContainerEtaFolderSyncMacro`) use their own distinct namespace/class
names and are real approved macros - don't force every future macro into the
literal `LinnworksMacro`/`LinnworksMacro` name based on this test.

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

**This is not a location-only rule.** The same failure shape applies to every
reference-entity ID a macro might treat as a filter: vendor (`Guid.Empty` is
whichever vendor is named "Default", not "any vendor"), category, shipping
method, and any other `*Id`-typed parameter that looks optional. Before writing
`someId ?? Guid.Empty` for *any* of these, resolve the real set of values first
(`Inventory.GetStockLocations()`, the vendor list endpoint, the category tree,
etc.) the same way as the location case above - don't assume the pattern is safe
just because it's a different entity type than the one that was live-tested.
`check_against_standards` flags any `?? Guid.Empty` occurrence for this reason -
treat a flag on it as a real bug to investigate, not noise.

**Independently reconfirmed 2026-08-20**, different account, different endpoint:
probing `Picking.GetItemBinracks` for a real batch-tracked item found its bin
data sitting at the location literally named `"Default"`, whose
`StockLocationId` is `00000000-0000-0000-0000-000000000000`. Same conclusion,
reproduced from scratch - see `references/macro/patterns/picking_get_item_binracks.md`
for the full test.

**This rule applies identically no matter which agent is writing the macro** -
Claude, Antigravity, or ChatGPT. It's documented here (not only in any one
agent's prompt) precisely so it can't be forgotten by switching tools.

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
- **Compact code, not padded code.** Don't insert a blank line between every
  statement or put a brace on its own line where the golden examples don't -
  match the density they already use (Allman braces, one blank line to separate
  logical groups, not one after every line). Compactness is in service of the
  ~5-minute-budget mindset above (less to read while debugging a run that's about
  to time out), not a hard line-count target - never compact by dropping the
  mandatory structure (rule 1), logging (rule 2), or error handling.

## 8. Execute parameters: one documented scalar per value - never a blob

Every parameter on `Execute` needs an XML `/// <param name="x">description</param>`
doc comment directly above it. This isn't cosmetic: Linnworks' macro settings UI
renders that description next to the field when someone configures the macro (see
`03_PickListMonitoring.cs`'s `viewName`/`location` doc comments - "Optional
Linnworks open-order view. Leave empty to scan all open orders." is exactly what
a non-developer configuring the macro sees). A parameter with no description is a
blank, unexplained text box in that UI.

**The rule is "one parameter per logical setting", not "one parameter per scalar
value".** Don't combine several *unrelated* settings into one JSON/CSV-encoded
blob (`Execute(string configJson)` parsed with `JsonConvert.DeserializeObject`
inside, jamming a location name and a folder prefix and a flag into one string)
to avoid declaring several parameters - Linnworks' settings UI edits one text
field per `Execute` parameter, so a blob like that isn't editable there in any
useful way, it just moves the problem from "no config UI" to "a config UI that
shows one cryptic text box."

A delimited (CSV) value **is** allowed - and often the better choice - when the
parameter genuinely represents **one setting that can hold more than one value**:
locations, folders, sources/sub-sources, SKUs, channels, or any other selector a
user might reasonably want to scope to two or three of, not necessarily one or
all. Forcing that into "one or ALL" is worse UX than a documented CSV field, not
better. See `references/macro/patterns/multi_value_selector.md` for the standard
way to parse and resolve one of these - it defines the trim/case/dedupe/ALL
rules once so every macro handles a multi-select parameter the same way, instead
of each macro re-inventing (and probably getting slightly wrong) its own
location-list parsing.

Whichever shape a parameter takes, it must still be one `string` (Linnworks'
settings UI only edits text - parse/validate inside `Execute`, don't expect the
engine to coerce it), and its `<param>` doc comment must say what it is,
including - if it's a multi-value selector - the delimiter and what a blank
value means (see `multi_value_selector.md`; never leave "what does blank mean"
unstated, and never let blank silently resolve via `Guid.Empty`, per section 0.1).

`scaffold_macro`'s `config_params` argument takes `"name:description"` pairs
(separated by `|`) for exactly this reason, and refuses to generate a parameter
with no description - use it rather than hand-writing the signature, so this
rule can't be silently skipped. It generates a single `string` parameter either
way; whether that parameter's value happens to be delimited is a documentation
and parsing concern (write the delimiter semantics into the description you
pass), not a different code-generation path.

## 9. Verify the request/response shape before writing an API call

A macro calling an API with a malformed or incomplete request object fails at
**runtime** with a generic "Bad Request" - not at compile time, and not with a
message that points at which field was wrong. Before writing a call that
constructs a request object (`Search_PurchaseOrder2Request`, `AddOrderNote`
parameters, anything with more than one or two fields), call `get_model` (or
`get_endpoint` for the controller) to check the real field names, which are
required vs optional, and their actual types - don't recall the shape from
memory or infer it from a similarly-named type. This is especially easy to get
wrong for request types that look like their response counterpart but aren't
(different required fields, different casing) - checking first is one tool call;
debugging a live "Bad Request" with no field-level detail is not.

## 10. Every `Api.*` call goes through the rate-limit wrapper - no exceptions

Rule 4 says this is mandatory, and `check_against_standards` now actually checks
it: any line calling `Api.<Controller>.<Method>(` that isn't also wrapped in
`ExecuteApi(...)` (the golden pattern is always `ExecuteApi("Name", () =>
Api.X.Y(...))` on one line) gets flagged. This closes a real gap - the rule
existed in prose since this file's first version, but nothing verified it before
now, so a single unwrapped call could slip through unnoticed until it caused a
real 429. Read the flag as "add the wrapper", not "this call is somehow exempt" -
there is no exemption in normal macro code.

## 11. Runtime-budget red flags `check_against_standards` now catches

Rule 7 already covers the ~5-minute budget in prose; these are the specific
shapes of getting it wrong that are now mechanically flagged, because they've
shown up in real macros:

- **`int.MaxValue` passed into a fetch/page-size argument** - a request for "no
  limit" against a growing order book will eventually exceed the budget outright,
  independent of how fast each individual call is. Page it instead (rule 7).
- **A large literal `Thread.Sleep`** (a hardcoded pause of several seconds or
  more, not the mandated `ExecuteApi` backoff array) - a 60-second sleep between
  every batch adds up fast against a 5-minute ceiling. If a deliberate pause is
  actually needed, make the duration small enough that the budget still holds
  even in the worst case, and say why in a comment.
- **A declared cap (`MaxOrdersPerRun`, `MaxRecords`, etc.) that's never
  referenced again after its own declaration** - a variable that exists only to
  document an intended limit isn't a limit; it has to actually gate a loop
  (`if (processed >= MaxOrdersPerRun) break;` or equivalent) to do anything.

**Not flagged, deliberately**: an `Api.*` call nested inside two loops. Detecting
that reliably needs real parsing (loop nesting, not a regex), and a linter that
tries anyway will be wrong often enough to train people to ignore its output.
Watch for it by reading the code instead - "does this call happen once per
combination of two things I'm iterating over" is usually visible on inspection.

## 12. Destructive operations need a visible ownership/idempotency guard

A call that deletes, unassigns, cancels, or wholesale-overwrites something
(`DeleteAssignedStock`, `Unassign...`, `Cancel...`, replacing an entity's full
property set instead of adding to it) is a lot more expensive to get wrong than
a read - a duplicate read is wasted API budget, a duplicate delete/cancel is
data loss. `check_against_standards` warns (not fails) when it sees one of these
method names with nothing that looks like an ownership/idempotency check (a
marker-tag/processed-flag lookup, per rule 6) nearby in the same method - this
is a nudge to double check, not proof either way, since "nearby in the same
method" is a rough heuristic that can both miss a guard written differently and
flag one that's genuinely not needed (e.g. a guard enforced by the caller,
documented as such). Treat the warning as "look at this again", not "this is
wrong."
