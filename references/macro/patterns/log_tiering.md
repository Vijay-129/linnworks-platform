---
title: Log tiering - run/order/record decisions at Info, per-candidate noise at Debug
added_by: platform
date: 2026-08-20
---

## Problem

Two failure modes pull in opposite directions if a macro only has one log level
in practice:

- **Too sparse**: "macro finished" with no detail forces anyone debugging a run
  to guess what actually happened to a specific order/SKU.
- **Too noisy**: logging every candidate a macro considered and rejected (every
  bin checked, every wrong-type bin skipped) at the same level as real decisions
  buries them. A run processing 100 orders x 10 SKUs x several bin candidates
  each can produce thousands of `WriteInfo` lines this way, which defeats the
  point of logging - nobody reads thousands of lines to find the one that
  mattered.

The fix isn't "log more" or "log less" - it's putting different *kinds* of
information at different levels on purpose.

## Which API / helper

`Logger` (from `LinnworksMacroBase`) implements
`LinnworksMacroHelpers.Interfaces.ILogger`, which declares **four** methods, not
two: `WriteInfo`, `WriteWarning`, `WriteError` - and `WriteDebug`. No golden
example in this repo uses `WriteDebug` yet, but it's declared on the identical
interface as the other three (all of which are confirmed working live), so it's
real and available - just unused so far. It's the natural home for the
"diagnostic" tier below rather than inventing a verbosity flag/parameter.

## The three decision levels (all at `WriteInfo`/`WriteWarning`/`WriteError`)

1. **Run-level** - one block at start, one at end (rule 2's mandatory pair).
   State the effective configuration at start (so a re-run's log is
   self-explanatory without cross-referencing the macro's settings screen), and
   a full outcome summary at end (see `operational_outcome.md` for what belongs
   in it).
2. **Order-level** (or whatever the top-level record is) - one line per record
   stating what was decided and why, not just that it was "processed."
3. **SKU/allocation-level** (or whatever the leaf-level decision is) - for a
   record with multiple sub-decisions (e.g. one order with several SKUs to
   allocate), one line per sub-decision: what was required, what was available,
   what was actually selected, pass/fail and why.

Every one of these three explains a **decision**, not just an outcome - "Result=FAIL
Reason='Insufficient E-com pick stock'" tells a reader what to fix; "processed 1
item" doesn't.

## The diagnostic tier (`WriteDebug`)

Individual rejected/skipped candidates (a bin considered and passed over,
`BatchInventoryId`s inspected, a raw API response that looked unusual) belong at
`WriteDebug`, not `WriteInfo`. This is the information that's valuable when
actively troubleshooting one specific run, and noise every other time. Route it
through `WriteDebug` rather than an `if (verbose)` flag around `WriteInfo` calls -
it's a real, distinct log level the engine already supports, so use it as one.

**Not mechanically checked.** Telling "a per-candidate line inside a loop" apart
from "a legitimate per-record Info line" isn't reliably regex-detectable, for
the same reason rule 11 doesn't attempt to detect an API call nested in a loop -
judge it by reading, same as that case.

## Example

Adapted from a real working macro's approach, reshaped into the three levels above:

```
START Sage200PickingAreaAllocator
Source='Sage 200'  Locations='ALL'  EcomMarker='999'  MaxOrders=100

[run] Locations scanned: 3
[run] Candidates found: 52
...
[order] Order 123456: Source='Sage 200' SubSource='WEB999' Location='Main Warehouse' Result='Allocated'
[sku]   Order 123456 | SKU ABC001: Required=5 Available=8 Selected=[ECOM-A01 qty=3, ECOM-A02 qty=2] Result=PASS
[sku]   Order 123457 | SKU XYZ001: Required=10 Available=4 Result=FAIL Reason='Insufficient E-com pick stock'
...
Processed: 52  Allocated: 38  AwaitingReplenishment: 11  ReviewRequired: 3  ApiRetries: 2  Elapsed: 41.7s
END Sage200PickingAreaAllocator
```

```csharp
Logger.WriteInfo($"{MacroName} started. Source='{source}' Locations='{locationsParam}' EcomMarker='{ecomMarker}' MaxOrders={maxOrders}");

// per order:
Logger.WriteInfo($"Order {order.NumOrderId}: Source='{source}' SubSource='{subSource}' Location='{location}' Result='{result}'");

// per SKU/allocation - the decision-explaining line:
Logger.WriteInfo(
    result == "PASS"
        ? $"Order {order.NumOrderId} | SKU {sku}: Required={required} Available={available} Selected=[{string.Join(", ", selected)}] Result=PASS"
        : $"Order {order.NumOrderId} | SKU {sku}: Required={required} Available={available} Result=FAIL Reason='{reason}'");

// per rejected candidate bin - diagnostic, not Info:
Logger.WriteDebug($"Order {order.NumOrderId} | SKU {sku}: candidate bin {binRack.BinRack} (BatchInventoryId={binRack.BatchInventoryId}) rejected - {rejectReason}");
```
