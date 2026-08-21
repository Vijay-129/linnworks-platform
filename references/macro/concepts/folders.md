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
the Rules Engine, and macros use folders to triage orders (e.g. `Fraud Review`, `Priority Dispatch`,
`Awaiting Stock`, `Backorder`, `Printed`).

> [!NOTE]
> Folders are primarily an Open Orders organizational mechanism. Do not assume folder assignments
> remain mutable or queryable through the same APIs after an order is processed.

---

## Core Identifiers and Assignment Keys

| Identifier | Type | Description |
|---|---|---|
| `folder` / `FolderName` | `string` | **The primary assignment key.** APIs assign and unassign orders using the folder name string, not an ID. |
| `pkFolderId` | `Guid` (string) | Unique ID of a folder definition record returned by `Orders.GetAvailableFolders`. |
| `orderIds` | `Guid[]` | List of open order UUIDs (`pkOrderId`) targeted for assignment or unassignment. |

---

## Important Models

| Model | Description |
|---|---|
| `OrderFolder` | Folder definition model returned by `GetAvailableFolders`: `pkFolderId` (Guid) and `FolderName` (string). |
| `Orders_AssignToFolderRequest` | Request payload for `Orders.AssignToFolder`: `orderIds` (`List<Guid>`) and `folder` (string). |
| `Orders_UnassignToFolderRequest` | Request payload for `Orders.UnassignToFolder`: `orderIds` (`List<Guid>`) and `folder` (string). |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics | Rate Limit |
|---|---|---|---|
| **Discover configured account folders** | `Orders.GetAvailableFolders` | Account-level configuration lookup. Returns available `OrderFolder` records. | 150/min |
| **Assign folder membership to orders** | `Orders.AssignToFolder` | Takes `orderIds[]` + `folder` name string. Fails on locked/parked orders. | 250/min |
| **Remove folder membership from orders** | `Orders.UnassignToFolder` | Takes `orderIds[]` + `folder` name string. Fails on locked/parked orders. | 250/min |
| **Set/replace available-folder configuration** | `Orders.SetAvailableFolders` | Replaces the account-wide list of available folders. Configuration-level only. | 150/min |

---

## Common Operations

- `Orders.GetAvailableFolders` — Retrieve the global list of active folders configured in the account.
- `Orders.AssignToFolder` — Add an order (or batch of orders) to a designated folder by name.
- `Orders.UnassignToFolder` — Remove an order (or batch of orders) from a designated folder by name.

---

## Gotchas & Operational Rules

### `SetAvailableFolders` is account-wide configuration

`Orders.SetAvailableFolders` replaces the account-wide list of available folders that orders can be assigned to.
- Do NOT call `SetAvailableFolders` to assign an order to a folder; use `Orders.AssignToFolder`.
- Macros that configure available folders must retrieve the current list via `GetAvailableFolders` and preserve existing folders not owned by the automation.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Folder assignment is additive, not an exclusive "move"

An order can belong to multiple folders simultaneously. Calling `Orders.AssignToFolder` adds membership to the specified folder without removing existing folder assignments.
- If a macro requires exclusive folder membership (moving an order from `Pending` to `Processed`), it must explicitly call `Orders.UnassignToFolder(orderIds, "Pending")` and then `Orders.AssignToFolder(orderIds, "Processed")`.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Assign and Unassign fail on locked or parked orders

Linnworks explicitly rejects `AssignToFolder` and `UnassignToFolder` if any order in the batch is locked (`order.GeneralInfo.IsLocked == true`) or in a parked state.
- Filter out locked orders before calling folder assignment endpoints.
- Do not assume `HoldOrCancel` represents parked state; parked orders utilize specific status tagging (e.g. tag 7 via `Orders.ChangeOrderTag`).
- Always handle potential API rejection gracefully because an order's lock/parked status can change between read and write.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Folder assignment uses the folder name string

`Orders.AssignToFolder` and `Orders.UnassignToFolder` accept the folder name string directly (`folder`), not a GUID.
- Resolve the target folder against `Orders.GetAvailableFolders()` at startup or initialization.
- Reuse the canonical `FolderName` string returned by Linnworks rather than synthesizing or guessing variations.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Orders.cs`

### Resolve folder configuration before assignment

If a macro relies on a specific target folder (e.g. `Fraud Review`), verify its existence using `Orders.GetAvailableFolders()` during startup. If the configured folder does not exist, treat it as a configuration error and log the `NumOrderId` plus the missing folder name rather than silently falling back to an arbitrary folder.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Batch folder assignment operations

`Orders.AssignToFolder` and `Orders.UnassignToFolder` accept a list of `orderIds`. Always batch order GUIDs (e.g. 50–100 orders per request) rather than making single-order API calls in a tight loop.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Folders are primarily used to organize and stage open orders
- [`rules_engine`](rules_engine.md) — Rules Engine conditions frequently evaluate or assign order folders

---

## Related Workflows

- [`modify_open_orders_by_sku`](../workflows/modify_open_orders_by_sku.md) — Workflow for filtering orders and moving matched orders to a folder
