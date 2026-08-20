---
title: Verifying a mutation actually persisted, not just that the call didn't throw
added_by: platform
date: 2026-08-20
---

## Problem

A successful API response (no exception thrown) isn't proof a write actually
took effect the way the macro expects - especially under two overlapping macro
executions racing on the same record, or a partial failure the API swallows
without erroring. `03_PickListMonitoring.cs`'s `TryCreateVerifiedMarker` does
this correctly: it doesn't just call the write and move on, it re-reads the
record afterward and checks the specific thing it just wrote is actually there.
This pattern generalizes that shape for reuse beyond markers specifically.

## Which API / helper

Whichever write has a corresponding read for the same entity - extended
properties, notes, folder assignment, a status/field update. Not every write has
a cheap read counterpart; use judgment about whether the record is important
enough (a mutation another run's idempotency check depends on, a customer-facing
change) to justify the extra call, per rule 7's budget.

## Gotchas

- **Re-read the specific field/record you changed, not "did the call throw".**
  A 200-response write can still not have applied the way you expect if two
  executions raced, or if the API accepted a partial update.
- **This matters most exactly where idempotency (rule 6) matters most** - if a
  marker write is what a later run checks to decide "have I already done this",
  and that write silently didn't persist, the later run will duplicate the work
  the marker was supposed to prevent. Verifying the write closes that gap.
- **Don't verify everything** - re-reading after every single write burns API
  budget (rule 7) for marginal benefit on low-stakes writes. Reserve this for
  writes another run's correctness actually depends on.
- **Log what verification found**, not just that it ran - if the re-read shows
  the write didn't persist, that's worth a log line of its own (with the
  record's human-readable ID, per rule 3), since it means the mutation needs a
  retry or an escalation, not silent acceptance.

## Example

```csharp
private bool TryCreateVerifiedMarker(Guid orderId, string markerTag)
{
    ExecuteApi("AddExtendedProperties", () => Api.Orders.AddExtendedProperties(
        orderId, new List<ExtendedProperty> { new() { Name = markerTag, Value = "true" } }));

    // Re-read - don't trust the write call's lack-of-exception as proof.
    var reloaded = ExecuteApi("GetExtendedProperties", () => Api.Orders.GetExtendedProperties(orderId));
    var persisted = reloaded.Any(p => p.Name == markerTag);

    if (!persisted)
        Logger.WriteError($"Marker '{markerTag}' did not persist for order {orderId}."); // orderId here should be NumOrderId - see rule 3

    return persisted;
}
```
