# Golden Examples

Real, approved macros from the team, supplied 2026-08-13. These are the actual basis
for `../macro_conventions.md` — that file's rules were derived by comparing these
three against each other, not invented. None of these are perfect; each is annotated
below with what to copy and what to fix if you're using it as a template.

## 01_ShopifyPaymentMethodMapping.cs

Order-triggered macro. Maps a Shopify payment gateway (read from order XML) to a
Linnworks payment method, with idempotency via an order identifier tag.

**Copy this:**
- Start/end logging in `try`/`finally` (`"...macro started."` / `"...macro finished."`)
- Idempotency: checks for a processed-identifier tag before doing any work, so
  re-running the macro on an already-processed order is a safe no-op
- Restores state in a `finally` block (re-parks the order if it unparked it, even
  if the update itself throws)

**Fix if reusing:**
- **No rate-limit handling at all** — no pacing, no 429 retry. Every `Api.*` call
  here is a direct, unwrapped call. If this macro processes more than a handful of
  orders, it will eventually hit 429 with no recovery. See 02 for the pattern to add.
- **Logs raw GUIDs**: `Logger.WriteError($"Order not found. OrderId: {orderId}")` and
  `$"Error processing Order {orderId}: {ex.Message}"` both log the raw `Guid`
  instead of `NumOrderId`. Every other log line in this file correctly uses
  `order.NumOrderId` — these two are the exception, not the pattern. Use `NumOrderId`
  in both.

## 02_ContainerEtaFolderSync.cs

Scheduled macro. Syncs order folders to purchase-order ETAs, processing orders in
batches with paged reads.

**Copy this — it's the reference implementation for rate-limit handling:**
- `ExecuteApi<T>` wraps every single `Api.*` call: proactive pacing
  (`PaceApiCall()`, a lock-protected minimum spacing between *any* two API calls,
  not just within one method) plus reactive exponential-backoff retry on HTTP 429
  (`IsHttp429` walks the full exception chain, not just the top-level exception)
- Batches mutations (100 at a time) instead of one call per order
- Verifies the result of every folder mutation by re-reading, not just trusting the
  response
- Explicit `finally` block that restores original lock/park state even if the main
  operation throws partway through

**Fix if reusing:**
- Also logs a raw GUID in a couple of places: `$"Order {order.NumOrderId} ({order.OrderId})..."`
  and `$"...for order {change.OrderNumber} ({change.OrderId})..."`. `NumOrderId`
  alone is enough context; drop the `({...OrderId})` suffix.
- `IsHttp429`'s string-matching fallback (`Contains("(429)")`) predates the
  `LinnworksAPI/Core/Factory.cs` fix (2026-08-13) that changed v1 error messages to
  `"Linnworks API error 429 calling X: ..."`. The primary check (walking to
  `WebException`/`HttpWebResponse.StatusCode`) still works after that fix since the
  original `WebException` is preserved as `InnerException` — but the string fallback
  should also check for `"error 429"` to match the new message format.

## 03_PickListMonitoring.cs

Scheduled macro. Parks an "original" order once its linked "consolidated" order's
pick list has printed, using extended-property markers for correlation and
idempotency.

**Copy this — it's the reference implementation for logging discipline:**
- Every single log line uses `NumOrderId`. No raw `Guid` is logged anywhere in this
  file. This is the standard to hold every other macro to.
- Re-reads both correlated orders immediately before mutating ("re-read authoritative
  state right before you act on it, not the snapshot from minutes ago")
- Verified, execution-race-safe marker creation (`TryCreateVerifiedMarker`): checks
  for an existing marker, writes, re-reads to confirm, and detects if another
  concurrent execution created it first

**Fix if reusing:**
- Rate-limit handling here is retry-only (`ApiCall<T>`, exponential backoff on 429)
  with **no proactive pacing** between calls — it relies on a flat
  `Thread.Sleep(ThrottleDelayMs)` between top-level candidates only, not around every
  individual `Api.*` call the way 02 does. For a macro that makes many calls per
  candidate (this one does — multiple reads/writes per order pair), add 02's
  `PaceApiCall()` pattern too, not just the retry.
- **Real bug, confirmed live 2026-08-14**: `FetchOpenOrderIds` does
  `LocationId = locationId ?? Guid.Empty` when no location is specified - but its
  own doc comment says `location` - *"Leave empty to scan all locations."* It
  doesn't. `Guid.Empty` is the real `StockLocationId` of the account's "Default"
  location, not a wildcard - on a 30-location test account this returned 1,871
  orders instead of the true total of 23,520 (8% of the actual data). See
  `../macro_conventions.md` section 0.1. **Do not copy this pattern** - if "all
  locations" is genuinely intended, fetch `Inventory.GetStockLocations()` once and
  loop, issuing one scoped call per location.
