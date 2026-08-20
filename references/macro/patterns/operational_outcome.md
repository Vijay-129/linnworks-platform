---
title: Recording a record's outcome as structured state, not just a log line
added_by: platform
date: 2026-08-20
---

## Problem

"Logged success/failure" is enough to debug a single run, but not enough to
answer "what happened to record X across every run this week" or to let a
non-developer see a record's status without reading logs. `03_PickListMonitoring.cs`
does better: it combines a marker (extended property), a human-readable note on
the order, and a summary-counts log line into one coherent outcome model instead
of just a pass/fail log entry. This pattern names that shape so future macros
default to it rather than to bare logging.

## Which API / helper

Whichever combination of `AddExtendedProperties`/`AddOrderNote`/folder-assignment
fits the entity - the point isn't a specific API, it's using *some* durable,
inspectable field (not just a log line) to hold the outcome.

## Shape

An outcome, wherever it's recorded, should carry:

- **A human-readable status** - not a bare boolean. `"Parked"`, `"SkippedAlreadyProcessed"`,
  `"FailedValidation"` reads meaningfully in the Linnworks UI; `true`/`false` doesn't.
- **A reason**, when the status isn't the happy path - why it was skipped or
  failed, specific enough that someone reading it later doesn't have to go find
  the log for that run.
- **A timestamp** - when this outcome was recorded, so staleness is visible
  without cross-referencing a log.
- **A classification**, if the macro has more than one kind of outcome worth
  distinguishing (`03`'s `ProcessingOutcome` enum is the reference - see
  `macro_conventions.md` rule 7's note that a real enum earns its keep here).
- **A run summary** in the final log line - counts per outcome (parked: 4,
  skipped: 12, failed: 1), not just "macro finished." (rule 2's mandatory end
  log - this is what to put in it for anything that processes a batch.)

Whether the per-record outcome itself lives in an extended property, an order
note, a folder, or some combination depends on what the requirement actually
needs visible and where - there's no single required storage mechanism, only the
requirement that *some* durable, inspectable record of the outcome exists beyond
a log line that scrolls away.

## Example

```csharp
private enum ProcessingOutcome { Parked, SkippedAlreadyProcessed, FailedValidation }

private readonly Dictionary<ProcessingOutcome, int> _counts = new();

private void RecordOutcome(Guid orderId, int numOrderId, ProcessingOutcome outcome, string reason = null)
{
    _counts[outcome] = _counts.GetValueOrDefault(outcome) + 1;

    var note = reason == null
        ? $"Macro: {outcome}."
        : $"Macro: {outcome}. {reason}";
    ExecuteApi("AddOrderNote", () => Api.Orders.AddOrderNote(orderId, note, false));

    Logger.WriteInfo($"Order {numOrderId}: {outcome}" + (reason != null ? $" ({reason})" : ""));
}

// At the end of Execute, in the finally block (rule 2):
Logger.WriteInfo(
    $"MacroName finished. " +
    string.Join(", ", _counts.Select(kv => $"{kv.Key}: {kv.Value}")));
```
