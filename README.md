# Linnworks Platform

Rebuilt, self-contained Linnworks SDK + AI reference layer. This is now the canonical
repo — it no longer reads from `linnworks-api-master` for anything; the source
material it needs (specs, macro helper code, legacy v1 SDK) is copied in under
`vendor/` and `legacy/`. Migration status: `migration/STATUS.md`, all 30 rows `done`.

## Layout

```
LinnworksAPI/              Reusable C# SDK — the only thing that talks to Linnworks
├── V1/Controllers/        27 controllers, all ported and building clean
├── V2/Controllers/        Orders (5 endpoints) + WarehouseTransfer/FBA-inbound (45 endpoints),
│                          written fresh — no v1 equivalent existed to port from
├── Shared/Common/         Models referenced by more than one v1 controller
└── Core/                  v1: ApiContext/Factory/BaseController/ApiObjectManager
                            v2: LinnworksAPI.V2 namespace — ApiContextV2/RestClient/
                            ApiObjectManagerV2 (real REST verbs + JSON bodies, unlike v1)

LinnworksMacroHelpers/, LinnMacroCustomer/   Macro helper libraries (FTP/SFTP/Email/
                                              Dropbox/raw HTTP) — real code macros use,
                                              not just a doc source

references/                What the AI assistant reads
├── api/v1, api/v2/        Generated from vendor/PublicApiSpecs (+ reverse-documented
│                          for the 3 controllers with no spec file)
├── macro/
│   ├── linnworks-api/     How a macro calls into LinnworksAPI (thin — no endpoint duplication)
│   ├── integrations/      GENERATED from LinnworksMacroHelpers/Classes + LinnMacroCustomer
│   └── patterns/          HAND-WRITTEN, append-only — things not derivable from code
├── plugin/                Same shape as macro/, built once a plugin SDK exists
└── standards/             Coding conventions + golden example macros (still a stub)

vendor/                    Linnworks-authored source this repo builds from but doesn't own
├── PublicApiSpecs/        OpenAPI/Swagger spec files — replace to pick up API changes
└── llms.txt               Live apidocs.linnworks.net index, cross-check for the specs

legacy/LinnworksAPI-v1-source/   Frozen pre-rewrite v1 SDK source. Not built. Input for
                                  port_controller.py / reverse_document_controller.py only.

migration/
├── STATUS.md              Per-controller migration tracker (30 rows, all done)
└── spec_snapshot/          Pinned copy of PublicApiSpecs at last sync, for diffing next time

scripts/
├── sync_api_spec.py               vendor/PublicApiSpecs -> references/api/vX
├── sync_macro_integrations.py     LinnworksMacroHelpers/LinnMacroCustomer -> references/macro/integrations
├── port_controller.py             legacy/ -> LinnworksAPI/V1/Controllers/<Name>/ (bootstrap tool, done)
├── reverse_document_controller.py legacy/ -> references/api/v1/<Name>.md, for no-spec controllers
├── generate_v2_models.py          vendor/PublicApiSpecs/2.0/*.json -> C# POCOs (recursive $ref resolution)
└── generate_v2_controller.py      Same, plus a full controller class (derives method names when
                                    operationId is missing in the spec)

mcp-shared/                 API-lookup logic (list_controllers/get_endpoint/search_api/
                            get_model) and --http transport setup shared between the two
                            servers below - both are internal-only in practice, so this
                            was judged safe. Nothing macro/golden-example-related lives
                            here.

mcp-server/                 Full internal MCP server: mcp-shared/'s API lookup + macro
                            conventions + golden examples + standards linting + real
                            dotnet-compile validation. stdio and --http.

mcp-server-api/             Narrower subset built on the same mcp-shared/ lookup tools,
                            nothing about macro conventions or golden examples. stdio and
                            --http - see mcp-server-api/README.md.
```

## Rules

1. `LinnworksAPI/` is the only place that calls the real Linnworks API. Nothing else
   duplicates endpoint knowledge — `references/api/` describes it, never re-implements it.
2. `references/macro/integrations/*.md` is generated. Never hand-edit — edit the source
   class in `LinnworksMacroHelpers`/`LinnMacroCustomer`, then re-run the sync script.
3. `references/macro/patterns/` is the only hand-written, freely-growing folder. New dev
   solves something novel → add a file here using `_template.md`.
4. A controller isn't "done" until `migration/STATUS.md` says so and it builds clean.
5. When a model turns out to be used by more than one v1 controller, it belongs in
   `Shared/Common/`, not duplicated per controller.
6. v2 lives in the `LinnworksAPI.V2` namespace, never the flat `LinnworksAPI` v1
   namespace — v1 and v2 have already had at least one real name collision
   (`FulfillmentStatus`), and v2's REST calling convention (JSON body, path params,
   real verbs) is genuinely different from v1's form-encoded style.
7. `vendor/` and `legacy/` are inputs, not code to maintain by hand — refresh `vendor/`
   from a new Linnworks export when the API changes; `legacy/` should never change.
