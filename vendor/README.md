# vendor/

Third-party material this repo builds from but doesn't own:

- `PublicApiSpecs/` — Linnworks' own OpenAPI/Swagger spec files (copied from
  `linnworks-api-python-main/PublicApiSpecs` in the original `linnworks-api-master`
  checkout). `scripts/sync_api_spec.py`, `scripts/generate_v2_models.py`, and
  `scripts/generate_v2_controller.py` all read from here by default.
- `LinnworksNetSDK/` — Linnworks' official developer SDK repo (C# source, not a
  spec file) - richer per-property/per-method description text than
  `PublicApiSpecs/` carries, sometimes for models `PublicApiSpecs/` doesn't have at
  all. See `LinnworksNetSDK/README.md` for exactly what was copied and why.
  `scripts/enrich_api_descriptions.py` reads from here.
- `llms.txt` — the live apidocs.linnworks.net doc index, used as a cross-check
  against the spec files (they can drift out of sync with each other).

## Refreshing

When Linnworks updates their API, replace the contents of `PublicApiSpecs/` with a
fresh export and re-run `scripts/sync_api_spec.py` - it diffs against
`migration/STATUS.md`'s `Spec Version` column and only the controllers whose spec
actually changed need re-review. To refresh `LinnworksNetSDK/` instead (or as well),
see `LinnworksNetSDK/README.md` - then re-run `scripts/enrich_api_descriptions.py`
before `sync_api_spec.py` so the enrichment reflects the new source.
