# vendor/LinnworksNetSDK/

Copied from `https://github.com/linnworks-dev/LinnworksNetSDK` (official, read-only
Linnworks developer SDK repo - "We are not accepting commits on this project at this
time"), commit `d88873c41b141cd29fe98944f2087fc3884dcdee` (2026-08-11), the paths
`Linnworks/src/netcore/LinnworksAPI/ClassBase/` and
`Linnworks/src/netcore/LinnworksAPI/Controllers/`.

Why this exists alongside `vendor/PublicApiSpecs/`: the OpenAPI/Swagger spec files in
`PublicApiSpecs/` carry little to no per-property description text, and some models
present here (e.g. `AddOrdersNoteRequest`) don't appear in `PublicApiSpecs/` at all.
This SDK's C# source has real `/// <summary>` XML doc comments on nearly every model
property (`ClassBase/*.cs`, one file per model, 985 files) and controller method
(`Controllers/*.cs`, one file per controller, matching `PublicApiSpecs/1.0`'s 27
controllers). `scripts/enrich_api_descriptions.py` reads this to fill in the
Description column `sync_api_spec.py` couldn't populate from the JSON specs alone.

Only `ClassBase/` and `Controllers/` were copied - not `LinnworksAPI2/` (v2/OpenAPI
variant, not yet consumed by anything here), `LinnworksMacro*`/`LinnMacroCustomer`
(this repo already has its own copies under `LinnworksMacroHelpers/`/
`LinnMacroCustomer/` at the platform root, ported separately - don't conflate the
two), or `Examples/`/`wiki-assets/` (not relevant to description enrichment).

## Refreshing

Re-clone `https://github.com/linnworks-dev/LinnworksNetSDK`, copy the same two
folders over these, update the commit hash above, and re-run
`scripts/enrich_api_descriptions.py` followed by `scripts/sync_api_spec.py`.
