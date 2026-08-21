---
title: Import and Export Feeds
slug: import_export
related_concepts: [inventory, open_orders, processed_orders]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/ImportExport.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/importexport.json
  - type: operational_guidance
    ref: vendor/llms.txt
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The bulk data feed and scheduled file automation subsystem in Linnworks.

Linnworks Import/Export automations process configured tabular data feeds, commonly CSV-based, with
files sourced from or delivered to configured remote storage locations such as FTP/SFTP, Dropbox, and
supported HTTP endpoints.

Macros use this API primarily to discover configured import/export jobs, inspect their settings,
enable or disable schedules, and request immediate execution using `RunNowImport` or `RunNowExport`.

---

## Architectural Separation: Import Flow vs Export Flow

```
1. Bulk Import Pipeline
   Schedule / RunNowImport
           ↓
   Queued for execution (Async)
           ↓
   Configured source file retrieved (FTP/SFTP/Dropbox/HTTP)
           ↓
   Import-specific parsing, column mapping, and validation
           ↓
   Applicable Linnworks records updated (Inventory, Orders, Customers)
           ↓
   Execution status recorded in job history

2. Bulk Export Pipeline
   Schedule / RunNowExport
           ↓
   Queued for execution (Async)
           ↓
   Configured export dataset queried from database
           ↓
   Filters, column mappings, and expressions applied
           ↓
   Output tabular file generated (e.g. CSV)
           ↓
   File uploaded to destination (FTP/SFTP/Dropbox/HTTP)
```

---

## Core Identifiers and Configuration Fields

| Identifier | Type | Meaning & Constraints |
|---|---|---|
| `importId` | `int32` | Primary unique ID of a configured import job in Linnworks. |
| `exportId` | `int32` | Primary unique ID of a configured export job in Linnworks. |
| `FriendlyName` | `string` | Human-readable name assigned to the import or export job. |
| `Type` / `ImportType` | `string` / model-dependent | Data feed category (e.g. `Inventory Import`, `Stock Level Import`, `Open Order Import`). |
| `Schedule` | `object` / model-dependent | Configuration controlling automated execution frequency. Inspect exact model schema rather than assuming cron syntax. |

---

## Important Models

| Model | Description |
|---|---|
| `ImportRegister` | Summary model returned by `GetImportList`: `Id` (int32), `FriendlyName`, `Type`, `Enabled`, `Executing`, `IsQueued`, `LastRun`, `NextSchedule`. |
| `ExportRegister` | Summary model returned by `GetExportList`: `Id` (int32), `FriendlyName`, `Type`, `Enabled`, `Executing`, `IsQueued`, `LastRun`, `NextSchedule`. |
| `Import` | Full import job configuration containing `Specification` (column mappings, feeds) and `Schedules`. |
| `Schedule` | Schedule definition model controlling automatic execution intervals. |
| `ImportExport_RunNowImportRequest` | Execution payload for `RunNowImport`: `importId` (int32). |
| `ImportExport_RunNowExportRequest` | Execution payload for `RunNowExport`: `exportId` (int32). |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Permissions Required | Rate Limit |
|---|---|---|---|
| **List configured import jobs** | `ImportExport.GetImportList` | `GlobalPermissions.Settings.ImportExport.ImportNode` | 150/min |
| **Read full import configuration & mappings** | `ImportExport.GetImport` | `GlobalPermissions.Settings.ImportExport.ImportNode` | 150/min |
| **Enable or disable an import schedule** | `ImportExport.EnableImport` | `GlobalPermissions.Settings.ImportExport.ImportNode` | 150/min |
| **Queue an import job for immediate run** | `ImportExport.RunNowImport` | `GlobalPermissions.Settings.ImportExport.ImportNode` | 150/min |
| **List configured export jobs** | `ImportExport.GetExportList` | `GlobalPermissions.Settings.ImportExport.ExportNode` | 150/min |
| **Read full export configuration & mappings** | `ImportExport.GetExport` | `GlobalPermissions.Settings.ImportExport.ExportNode` | 150/min |
| **Enable or disable an export schedule** | `ImportExport.EnableExport` | `GlobalPermissions.Settings.ImportExport.ExportNode` | 150/min |
| **Queue an export job for immediate run** | `ImportExport.RunNowExport` | `GlobalPermissions.Settings.ImportExport.ExportNode` | 150/min |

---

## Common Operations

- `ImportExport.GetImportList` — Discover all active and inactive import jobs in the account.
- `ImportExport.RunNowImport` — Place a configured import job into the execution queue immediately.
- `ImportExport.GetExportList` — Discover all configured export jobs in the account.
- `ImportExport.RunNowExport` — Place a configured export job into the execution queue immediately.
- `ImportExport.EnableImport` / `ImportExport.EnableExport` — Toggle automatic scheduling on or off.

---

## Gotchas & Operational Rules

### `RunNow` queues execution — it does not wait for completion

`ImportExport.RunNowImport` and `ImportExport.RunNowExport` place an existing configured job into Linnworks' backend execution queue.
- The API responds with `204 No Content` to confirm that the job was accepted into the queue.
- `204 No Content` does NOT indicate that data processing or file generation has finished.
- Do not immediately assume imported records or exported files are available in subsequent macro steps.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/importexport.json`

### Avoid partially uploaded source files (Atomic Uploads)

When external ERPs or automated processes generate files for scheduled or `RunNow` imports, ensure Linnworks does not read the file while it is still being written:
1. Upload the file to the remote server using a temporary filename (e.g. `inventory_feed.tmp`).
2. Once the upload is completely finished, atomically rename the file to the monitored filename (e.g. `inventory_feed.csv`).
3. This prevents Linnworks from locking and reading incomplete or corrupted data feeds.

**Source:** `operational_guidance` — `vendor/llms.txt`

### Direct API vs Configured Import/Export Automation

- **Direct APIs (`Stock.SetStockLevel`, `Orders.SetExtendedProperties`):** Provide fine-grained request/response control, immediate feedback, and are appropriate for targeted, transactional updates.
- **Import/Export Automation (`RunNowImport` / `RunNowExport`):** Appropriate when upstream or downstream systems already exchange files, when operations are naturally bulk/tabular, or when scheduled batch feeds simplify operational architecture.
- Do not apply an arbitrary fixed row-count threshold; select the mechanism based on latency, error handling, and external system architecture.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Separate file format from transport mechanisms

Linnworks Import/Export feeds operate primarily on tabular CSV structures. Remote storage integrations (FTP, SFTP, Dropbox, HTTP) act as transport layers. Verify that file schemas, delimiter settings, and column headers match the configured job definition.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/importexport.json`

---

## Related Concepts

- [`inventory`](inventory.md) — Catalog imports and stock level exports interface directly with inventory
- [`open_orders`](open_orders.md) — Order imports ingest new orders from external sales channels
- [`processed_orders`](processed_orders.md) — Processed order exports generate historical dispatch and shipping records

---

## Related Workflows

- (Used in automated file exchange, ERP synchronization, and catalog feed orchestration macros)
