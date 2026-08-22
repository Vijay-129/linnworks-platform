---
title: Order Folders
slug: folders
related_concepts: [open_orders, rules_engine]
related_workflows: [modify_open_orders_by_sku]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Orders.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/ClassBase/OrderFolder.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The organizational and workflow staging subsystem for open orders in Linnworks.

Folders provide a visual and operational categorization layer in the Open Orders grid. Operators,
the Rules Engine, and macros use folders to triage and stage orders (e.g. `Fraud Review`, `Priority Dispatch`,
`Awaiting Stock`, `Backorder`, `Printed`).

> [!NOTE]
> Folders are primarily an Open Orders organizational mechanism. Do not assume folder assignments
> remain mutable or queryable through the same APIs after an order is processed.

---

## Core Identifiers and Assignment Keys

| Identifier | Type | Description |
|---|---|---|
| `folder` / `FolderName` | `string` | **The primary assignment key.** APIs assign and unassign orders using the folder name string, not a GUID. |
| `pkFolderId` | `Guid` (string) | Unique ID of a folder definition record returned by `Orders.GetAvailableFolders`. |
| `orderIds` | `Guid[]` / `List<Guid>` | List of open order UUIDs (`pkOrderId`) targeted for assignment or unassignment. |

---

## Important Models

| Model | Description |
|---|---|
| `OrderFolder` | Folder definition model returned by `GetAvailableFolders`: `pkFolderId` (`Guid`) and `FolderName` (`string`). |
| `OpenOrder.FolderName` | Open-order model property exposing an array of strings (`string[]`), representing the order's active folder memberships. |
| `Orders_AssignToFolderRequest` | OpenAPI request wrapper for `Orders.AssignToFolder`: `orderIds` (`Guid[]`) and `folder` (`string`). |
| `Orders_UnassignToFolderRequest` | OpenAPI request wrapper for `Orders.UnassignToFolder`: `orderIds` (`Guid[]`) and `folder` (`string`). |

> [!NOTE]
> **SDK vs. OpenAPI Signatures:**
> - **.NET SDK:** `Orders.AssignToFolder(List<Guid> orderIds, string folder)` and `Orders.UnassignToFolder(List<Guid> orderIds, string folder)` return `List<Guid>`.
> - **OpenAPI / Raw HTTP:** Takes `Orders_AssignToFolderRequest` and returns `Guid[]`.

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics | Rate Limit |
|---|---|---|---|
| **Discover configured account folders** | `Orders.GetAvailableFolders` | Account-level configuration lookup. Returns available `List<OrderFolder>` records. | 150/min |
| **Assign folder membership to orders** | `Orders.AssignToFolder` | Takes `orderIds` + `folder` name string. Returns `List<Guid>` of affected orders. | 250/min |
| **Remove folder membership from orders** | `Orders.UnassignToFolder` | Takes `orderIds` + `folder` name string. Returns `List<Guid>` of affected orders. | 250/min |
| **Set available-folder configuration** | `Orders.SetAvailableFolders` | Full-list setter for account-wide available folders (`List<OrderFolder>`). Configuration only. | 150/min |

---

## Common Operations

- `Orders.GetAvailableFolders` — Retrieve the configured folder definitions currently available for order assignment.
- `Orders.AssignToFolder` — Add an order (or batch of orders) to a designated folder by name.
- `Orders.UnassignToFolder` — Remove an order (or batch of orders) from a designated folder by name.

---

## Gotchas & Operational Rules

### `SetAvailableFolders` is a full-list configuration operation

`Orders.SetAvailableFolders` sets the account's available folder list.
- Do NOT call `SetAvailableFolders` to assign an order to a folder; use `Orders.AssignToFolder`.
- When automation manages available folder configuration, retrieve the current list first via `Orders.GetAvailableFolders` and preserve existing folders not owned by the automation rather than submitting a partial assumed list.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json` | `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Orders.cs`

### Folder assignment is additive, not an exclusive "move"

An order can belong to multiple folders simultaneously. Open-order models expose `FolderName` as an array of strings (`string[]`). Calling `Orders.AssignToFolder` adds membership to the specified folder without removing existing assignments.
- If your workflow treats folders as mutually exclusive states (e.g. transitioning an order from `Pending` to `Processed`), enforce that policy explicitly by calling `Orders.UnassignToFolder(orderIds, "Pending")` and then `Orders.AssignToFolder(orderIds, "Processed")`.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Assign and Unassign fail on locked or parked orders

`Orders.AssignToFolder` and `Orders.UnassignToFolder` cannot be executed on locked (`order.GeneralInfo.IsLocked == true`) or parked orders (parked status is initialized via tag 7 with `Orders.ChangeOrderTag`).
- Filter out locked and parked orders before constructing a batch.
- Do not assume undocumented partial-success behavior for mixed valid/invalid batches.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json` | `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Orders.cs`

### Folder assignment uses the folder name string

`Orders.AssignToFolder` and `Orders.UnassignToFolder` accept the folder name string directly (`folder`), not a GUID.
- Resolve the target folder against `Orders.GetAvailableFolders()` at startup or initialization.
- Reuse the canonical `FolderName` string returned by Linnworks rather than synthesizing or guessing variations.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Orders.cs`

### Resolve folder configuration before assignment

If a macro relies on a specific target folder (e.g. `Fraud Review`), verify its existence using `Orders.GetAvailableFolders()` during startup. If the configured folder does not exist, treat it as a configuration error and log the `NumOrderId` plus the missing folder name rather than silently falling back to an arbitrary folder.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Batch folder assignment operations

`Orders.AssignToFolder` and `Orders.UnassignToFolder` accept multiple order UUIDs in one request and return the affected `List<Guid>`. Prefer batching compatible orders rather than issuing one API call per order, while keeping batch sizing configurable and respecting the 250 requests/minute rate limit.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Folders are primarily used to organize and stage open orders
- [`rules_engine`](rules_engine.md) — Rules Engine conditions frequently evaluate or assign order folders

---

## Related Workflows

- [`modify_open_orders_by_sku`](../workflows/modify_open_orders_by_sku.md) — Workflow for filtering orders and moving matched orders to a folder
