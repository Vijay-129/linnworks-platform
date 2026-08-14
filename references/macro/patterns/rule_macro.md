---
title: Writing a Rule (order-triggered) macro
added_by: platform
date: 2026-08-13
---

## Problem

A macro that Linnworks' Rules Engine calls with a specific set of matching order IDs
- e.g. "when an order's Source = Shopify, run this macro" - as opposed to a
Scheduled macro that runs on a timer and has to find its own working set.

## Which API / helper

Same `Api.*` surface as any macro (`references/macro/linnworks-api/overview.md`).
The distinguishing thing about a Rule macro isn't the API it calls, it's its
`Execute` signature and per-order processing shape.

## A macro is Rule *or* Scheduled - never both

**A macro has exactly one `Execute` method.** Don't write two `Execute` overloads
(one taking `Guid[] OrderIds` for rule-trigger, another taking scalar config
params for schedule-trigger) in the same macro class expecting Linnworks to pick
the right one depending on how it's invoked - that's not how macro resolution
works; a macro is registered as one trigger type with one signature, not both.
(Confirmed by the team 2026-08-14, after a submitted macro,
`StaleShippingLabelGuardian.cs`, did exactly this - see
`references/standards/macro_conventions.md` section 1.1.)

If a requirement could plausibly go either way (a user describes both "run it on
a schedule" and "trigger it from a rule" language), pick one deliberately based
on the actual trigger semantics the requirement describes, state which one and
why, and write a single macro for it. If genuinely ambiguous, ask which one
before writing code - don't hedge by writing both into one file.

## Gotchas

- **Entry point signature**: the Rules Engine passes matched orders as
  `Guid[] OrderIds`, always as the *first* parameter. This is the dominant pattern
  across every Rule macro in the corpus (`source\repos\macros\LinnworksMacro`) -
  `MargeOrderMacro`, `ParkOrderMacro`, `LowStockEmail`, `FolderAssignmentAndOrderItemAddition`,
  and the golden example `01_ShopifyPaymentMethodMapping.cs` all start with
  `Execute(Guid[] OrderIds, ...)`. Don't invent a different parameter shape for
  "the orders this macro acts on" - use `Guid[] OrderIds`.
- **A rule can fire more than once on the same order** — if the rule's condition
  stays true (e.g. it re-evaluates on a schedule, or another rule/macro touches the
  order again), your macro can be called again for an order it already processed.
  Idempotency is not optional here: use a durable marker (identifier tag or
  extended property) checked *before* mutating, the same way `01`'s
  `EnsureProcessedIdentifierExists`/`OrderAlreadyHasProcessedIdentifier` do. See
  `references/standards/macro_conventions.md` rule 6.
- **Per-order try/catch, not one try/catch for the whole batch** — if `OrderIds`
  has 50 orders and order #30 throws, orders #31-50 should still get processed.
  `01`'s structure (`Execute` loops and delegates to `ProcessOrder`, which has its
  own try/catch) is the pattern - a single top-level try/catch around the whole
  loop means one bad order silently stops everything after it.
- **Batch size is generally small** (a rule firing on a handful of newly-matching
  orders), so the aggressive pacing/batching machinery in
  `references/standards/macro_conventions.md` rule 4 matters less here than in a
  Scheduled macro - but still wrap every `Api.*` call through the same
  `ExecuteApi`/pacing+backoff helper. A rule macro that normally sees 3 orders per
  run can still get hit with 200 if someone bulk-re-triggers the rule.
- **Restore state you changed, even on failure** — if the macro unparks/unlocks an
  order to make a change, put the re-park/re-lock in a `finally`, not just at the
  end of the happy path (see `01`'s `wasUnparkedForUpdate` handling).

## Example

Minimal shape (see `references/standards/golden_examples/01_ShopifyPaymentMethodMapping.cs`
for the full real version):

```csharp
public class MyRuleMacro : LinnworksMacroBase
{
    public void Execute(Guid[] OrderIds, string SomeConfigParam)
    {
        try
        {
            Logger.WriteInfo("MyRuleMacro started.");

            if (OrderIds == null || OrderIds.Length == 0)
            {
                Logger.WriteInfo("No OrderIds supplied. Macro exiting.");
                return;
            }

            foreach (var orderId in OrderIds.Distinct())
            {
                ProcessOrder(orderId, SomeConfigParam);
            }
        }
        catch (Exception ex)
        {
            Logger.WriteError($"Unhandled macro error: {ex.Message}");
        }
        finally
        {
            Logger.WriteInfo("MyRuleMacro finished.");
        }
    }

    private void ProcessOrder(Guid orderId, string someConfigParam)
    {
        try
        {
            var order = Api.Orders.GetOrderById(orderId);
            if (order == null)
            {
                Logger.WriteError($"Order not found. NumOrderId lookup unavailable for {orderId}.");
                return;
            }

            // idempotency check before any mutation - see macro_conventions.md rule 6

            // ... do the work, using order.NumOrderId in every log line, never orderId ...
        }
        catch (Exception ex)
        {
            Logger.WriteError($"Error processing order: {ex.Message}");
        }
    }
}
```
