---
title: Writing a Scheduled macro
added_by: platform
date: 2026-08-13
---

## Problem

A macro Linnworks runs on a timer (e.g. every 5-60 minutes), with no order IDs or
other working set handed to it - unlike a Rule macro (see `rule_macro.md`), it has
to discover what to act on itself, every run.

## Which API / helper

Same `Api.*` surface as any macro (`references/macro/linnworks-api/overview.md`).
The distinguishing concerns are pagination, rate-limit safety at volume, and
overlap/idempotency across runs - not any particular controller.

## A macro is Scheduled *or* Rule - never both

Same hard rule as `rule_macro.md`: **one `Execute` method per macro.** Don't add a
second `Execute(Guid[] OrderIds, ...)` overload to a scheduled macro "just in
case" it's also useful as a rule macro - write a separate macro if you actually
need both trigger types. See `references/standards/macro_conventions.md` section
1.1 for why (a real submitted macro got this wrong).

## Gotchas

- **Entry point signature has no `OrderIds`** — scalar config parameters only (a
  location name, a folder prefix, a view name), since there's no Rules Engine
  match feeding it a working set. Both golden scheduled examples take only strings:
  `Execute(string preSalesLocationName, string folderPrefix = "...")` and
  `Execute(string viewName = "", string location = "")`.
- **Must discover its own working set, paged** — never fetch "all open orders" in
  one unbounded call. Both `02_ContainerEtaFolderSync` and `03_PickListMonitoring`
  page explicitly (`PageNumber`/`EntriesPerPage`, looping until a short page or an
  explicit `TotalPages` signals the end). A scheduled macro that runs every few
  minutes against a growing order book will eventually time out or blow the rate
  limit if it isn't paged from day one.
- **Rate-limit safety matters far more here than in a Rule macro** — a scheduled
  macro processing hundreds of records every run, forever, is exactly the shape
  that produces sustained 429s. The mandatory `ExecuteApi` pacing+backoff wrapper
  (`references/standards/macro_conventions.md` rule 4) is non-negotiable for a
  scheduled macro; `02`'s `ExecuteApi<T>`/`PaceApiCall` is the reference
  implementation specifically because it was written to fix a real 429 problem
  caused by an earlier unwrapped version (see the file's own header comment).
- **Prevent overlapping executions** — if a run takes longer than the schedule
  interval, Linnworks (or your own logic) needs to not start a second run on top
  of the first. `02`'s header comment says this explicitly ("do not overlap
  executions"); this is a scheduling/deployment concern (set the schedule interval
  comfortably longer than the macro's typical run time) as much as a code one, but
  idempotency markers (below) are what make an accidental overlap safe rather than
  duplicative.
- **Idempotency across runs is mandatory, not optional** — every run re-scans the
  same kind of records; without a durable "have I already handled this" check
  (identifier tag, extended property marker), a scheduled macro will redo the same
  work - and if that work is a mutation (a note, an email, a state change) -
  every single run. `03`'s `TryCreateVerifiedMarker` (create, then re-read to
  verify, so two overlapping runs can't both think they "won") is the reference
  pattern.
- **Batch mutations, don't loop one-call-per-record** — `02` groups changes by
  target folder and applies them in batches of 100 rather than one
  `AssignToFolder` call per order. Fewer, larger calls beat many small ones both
  for rate-limit headroom and wall-clock time.
- **Log a summary before the final "finished" line** — `03`'s pattern (counts of
  parked/skipped/failed/etc. logged right before `"Macro finished."`) turns each
  run's log into an audit trail instead of just a heartbeat. Worth copying for any
  scheduled macro that processes a batch.

## Example

Minimal shape (see `references/standards/golden_examples/02_ContainerEtaFolderSync.cs`
and `03_PickListMonitoring.cs` for the full real versions, including the
`ExecuteApi`/`PaceApiCall` rate-limit wrapper from `macro_conventions.md` rule 4):

```csharp
public sealed class MyScheduledMacro : LinnworksMacroBase
{
    private const int PageSize = 200;

    public void Execute(string locationName)
    {
        var startedUtc = DateTime.UtcNow;
        var processed = 0;

        try
        {
            Logger.WriteInfo("MyScheduledMacro started.");

            var records = LoadWorkingSet(locationName);
            foreach (var record in records)
            {
                if (AlreadyHandled(record))   // idempotency check - see macro_conventions.md rule 6
                    continue;

                ProcessRecord(record);        // every Api.* call inside goes through ExecuteApi
                processed++;
            }
        }
        catch (Exception ex)
        {
            Logger.WriteError($"MyScheduledMacro failed: {ex}");
        }
        finally
        {
            var elapsed = DateTime.UtcNow - startedUtc;
            Logger.WriteInfo($"MyScheduledMacro finished in {elapsed.TotalSeconds:N1}s. Processed: {processed}.");
        }
    }

    private List<SomeRecord> LoadWorkingSet(string locationName)
    {
        var result = new List<SomeRecord>();
        var pageNumber = 1;
        while (true)
        {
            var page = ExecuteApi($"...(page={pageNumber})", () => /* paged call */ null);
            if (page == null || page.Count == 0) break;
            result.AddRange(page);
            if (page.Count < PageSize) break;
            pageNumber++;
        }
        return result;
    }
}
```
