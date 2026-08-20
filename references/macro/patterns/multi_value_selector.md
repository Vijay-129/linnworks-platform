---
title: Parsing a multi-value selector parameter (locations, folders, sources, SKUs, ...)
added_by: platform
date: 2026-08-20
---

## Problem

A scheduled macro's config parameter is often a *selector* that a user might
reasonably want to scope to one value, several, or all of - a location name, a
folder, an order source/sub-source, a list of SKUs. Every macro that needs this
tends to invent its own parsing on the spot (a plain string compared with `==`,
or a CSV split with no documented rules for whitespace/case/duplicates/"ALL"),
which is exactly how `references/standards/macro_conventions.md` section 0.1's
`Guid.Empty`-as-"all" bug happened - an ad hoc "empty means unfiltered" shortcut
that turned out to mean one specific location instead. This pattern defines the
parsing/resolution rules once so every macro that needs a multi-value selector
handles it the same, already-correct way.

## Which API / helper

Whichever list endpoint resolves the selector's names to real entities -
`Inventory.GetStockLocations()` for locations, the relevant folder/source/vendor/
category endpoint otherwise. Check `search_api`/`get_endpoint` for the specific
controller; this pattern is about the parameter parsing/resolution shape around
that call, not the call itself.

## Rules

Given a raw parameter string (see `macro_conventions.md` rule 8 - it's always a
plain `string`, documented via its `<param>` doc comment):

1. **Trim** every value (leading/trailing whitespace around each delimited item,
   not just the whole string).
2. **Case-insensitive match** against the real names returned by the resolving
   API call - a user typing "main warehouse" should match "Main Warehouse".
3. **Remove duplicates** after trimming/casing (so `"A,a,A"` resolves to one
   value, not three).
4. **Resolve every name to its real ID** via the list endpoint - never construct
   or guess an ID from the name. Do this once per run (rule 7 - cache reference
   data), not per record.
5. **Reject or warn on unknown names** - a typo'd location name that silently
   matches nothing is worse than a macro that fails loudly and tells the user
   which name it didn't recognize.
6. **`Guid.Empty` is never how you represent "ALL"** - see section 0.1. If "ALL"
   is a value this parameter accepts, it must be a literal, documented string
   token (`"ALL"` below), resolved by enumerating every real value from the list
   endpoint - not by passing an empty/default ID through unfiltered.
7. **`"ALL"` combined with anything else is ambiguous - reject it.** `"ALL,Main
   Warehouse"` doesn't have an obvious meaning (all of them, redundantly plus one
   named again? everything except that one?) - fail with a clear message rather
   than silently picking an interpretation.
8. **Blank means something you decide and document, not a hidden default.**
   Depending on the macro, blank might mean "ALL" or might mean "nothing to do,
   exit early" - either is fine, but the `<param>` doc comment must say which
   (mirrors `03_PickListMonitoring.cs`'s `"Leave empty to scan all locations"`
   wording - just make sure the implementation actually does what the doc says,
   which is exactly what `03` gets wrong per section 0.1).

## Example

```csharp
/// <param name="locationNames">
/// Comma-separated stock location names to scope this run to, e.g. "Main
/// Warehouse,Overflow Warehouse". Case-insensitive, whitespace around each name
/// is ignored. Pass "ALL" (alone - not combined with named locations) to scope
/// to every location. Leave blank to exit without doing anything.
/// </param>
public void Execute(string locationNames = "")
{
    var locations = ResolveLocationSelector(locationNames, allLocations);
    // ... allLocations = ExecuteApi("GetStockLocations", () => Api.Inventory.GetStockLocations());
}

private static List<StockLocation> ResolveLocationSelector(string raw, List<StockLocation> allLocations)
{
    var tokens = (raw ?? "")
        .Split(',')
        .Select(t => t.Trim())
        .Where(t => t.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (tokens.Count == 0)
        return new List<StockLocation>(); // blank - this macro's documented meaning, decide per macro

    var isAll = tokens.Any(t => t.Equals("ALL", StringComparison.OrdinalIgnoreCase));
    if (isAll && tokens.Count > 1)
        throw new InvalidOperationException("locationNames: \"ALL\" cannot be combined with named locations.");
    if (isAll)
        return allLocations;

    var resolved = new List<StockLocation>();
    var unknown = new List<string>();
    foreach (var token in tokens)
    {
        var match = allLocations.FirstOrDefault(l => string.Equals(l.LocationName, token, StringComparison.OrdinalIgnoreCase));
        if (match == null) unknown.Add(token);
        else resolved.Add(match);
    }
    if (unknown.Count > 0)
        throw new InvalidOperationException($"locationNames: unrecognized location(s): {string.Join(", ", unknown)}.");

    return resolved;
}
```
