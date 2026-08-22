---
title: Rules Engine
slug: rules_engine
related_concepts: [open_orders, folders, shipping, extended_properties]
related_workflows: [modify_open_orders_by_sku]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/RulesEngine.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/rulesengine.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The automated business rules evaluation engine in Linnworks.

The Rules Engine evaluates open orders against configured condition trees (e.g. order totals, package
weights, destination countries, sales channels, item SKUs, or order tags) and executes automated actions
(such as assigning postal services, moving to folders, allocating fulfillment locations, setting extended
properties, adding order notes, or invoking macros).

Macros interact with the Rules Engine by triggering batch re-evaluations on open orders
(`Orders.RunRulesEngine`) after executing programmatic modifications, or by reading and managing
rule configurations.

---

## Core Identifiers and Fields

| Identifier | Type | Description |
|---|---|---|
| `pkRuleId` | `int32` | Unique integer identifier of the rule header. |
| `pkConditionId` | `int32` | Unique integer identifier of a condition-tree node. |
| `pkActionId` | `int32` | Unique integer identifier of an action attached to a condition node. |
| `RuleName` | `string` | Human-readable name of the rule (e.g. `UK Express Shipping Allocation`). |
| `RuleType` | `string enum` | Scope/family of the rule. Current values include `Orders` and `Test`. |
| `RunOrder` | `int32` | Configured execution sequence priority of the rule. |
| `Enabled` | `boolean` | Indicates whether the rule or condition node is active. |

---

## Important Models

| Model | Description |
|---|---|
| `RuleHeaderBasic` | Rule summary: `pkRuleId`, `RuleName`, `RuleType`, `Enabled`, `RunOrder`, `pkRuleId_Draft`, `Draft`, `RuleTypeDisplayName`. |
| `RuleConditionHeader` | Condition-tree node: `pkConditionId`, `fkRuleId`, `RunOrder`, `Enabled`, `ConditionName`, `fkParentConditionId`, `Conditions`, `Action`, `Subrules`. |
| `RuleAction` | Action attached to a condition node: `pkActionId`, `ActionName`, `ActionType`, `ActionValue`, `fkConditionId`, `Properties`. |
| `RuleEvaluationResult` | Result returned by `TestEvaluateRule`, identifying the last condition and action reached (`LastConditionId`, `LastActionId`). |
| `FieldDescriptor` / `EvaluatorDescriptor` | Schema models describing available evaluation fields and comparison operator groups (e.g. `BasicEquality`, `Range`, `Set`, `StringEquality`). |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics |
|---|---|---|
| **Run rules against open orders** | `Orders.RunRulesEngine` | Re-evaluates rules on `orderIds` (`Guid[]`). Supply `ruleId` (int32) or null for all rules. |
| **List configured rules** | `RulesEngine.GetRules` / `GetRulesByType` | Retrieves rule headers across the account, optionally filtered by `RuleType`. |
| **Retrieve condition tree for a rule** | `RulesEngine.GetRuleConditionNodes` | Returns the hierarchy of condition nodes for `pkRuleId`. |
| **Discover valid evaluation fields** | `RulesEngine.GetEvaluationFields` | Returns available order/item fields that can be evaluated for a rule type. |
| **Discover valid evaluation operators** | `RulesEngine.GetEvaluatorTypes` | Returns supported comparison operators grouped by type. |
| **Discover supported action types** | `RulesEngine.GetActionTypes` | Returns valid rule actions (e.g. `AssignShippingService`, `AssignToFolder`, `ExecuteMacro`). |
| **Discover valid options for an action** | `RulesEngine.GetActionOptions` | Returns available target values for an `ActionType` (e.g. available postal service IDs). |
| **Test evaluate rule with mock values** | `RulesEngine.TestEvaluateRule` | Simulates rule execution against supplied test values without mutating live orders. |
| **Add action step to a condition node** | `RulesEngine.AddAction` | Attaches an action to a condition (cannot be attached to nodes with subconditions). |
| **Copy action between condition nodes** | `RulesEngine.CopyAction` | Duplicates an action to a target parent condition node. |
| **Reorder rule execution sequence** | `RulesEngine.SwapRules` | Swaps the `RunOrder` priority between two rules (`pkRuleId1`, `pkRuleId2`). |
| **Create draft copy for editing** | `RulesEngine.CreateDraftFromExisting` | Creates an editable draft rule copy. |
| **Publish draft rule to live** | `RulesEngine.SetDraftLive` | Promotes draft rule to live, replacing the previous active rule version. |
| **Enable/disable rule or condition** | `RulesEngine.SetRuleEnabled` / `SetConditionEnabled` | Toggles active status of a rule or condition branch. |

---

## Rules Engine Evaluation Flow

```
Open Order Received or Re-evaluated (Orders.RunRulesEngine)
       │
       ▼
Applicable Enabled Rules (Evaluated according to RunOrder)
       │
       ▼
Rule Condition Tree
       │
       ├─ Condition Matched ──────► Execute Configured Action (e.g. Assign Folder / Service)
       │
       └─ Condition Not Matched ──► Evaluate Next Subcondition or Subsequent Rule
```

---

## Gotchas & Operational Rules

### Explicitly rerun rules when workflows depend on changed order data

When a macro or integration modifies order properties (such as updating shipping address, changing line items, recalculating packaging, or modifying extended properties), do not assume rules will automatically re-evaluate in the background.
- If subsequent order routing depends on the updated values, explicitly call `Orders.RunRulesEngine(orderIds, ruleId)` for the affected open orders.

**Source:** `macro_convention` — `references/standards/macro_conventions.md` | `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Action attachment restrictions

`RulesEngine.AddAction` and `RulesEngine.CopyAction` enforce strict placement rules:
- Actions may only be attached to terminal condition nodes.
- Actions cannot be attached to the root rule header or to condition nodes that contain child subconditions.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/RulesEngine.cs`

### Guard against recursive loops with `ExecuteMacro`

The Rules Engine supports executing macros via the `ExecuteMacro` action type.
- If a macro called by the Rules Engine subsequently invokes `Orders.RunRulesEngine` or makes modifications that re-trigger the same rule, circular execution can occur.
- Design automated macros to be idempotent and verify guard conditions (such as checking folder assignment or an extended property flag) before performing mutations.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Discover rule fields and action types dynamically

Do not hardcode action values or condition field names. Use `RulesEngine.GetEvaluationFields`, `RulesEngine.GetActionTypes`, and `RulesEngine.GetActionOptions` to resolve available fields, postal service IDs, and folder options dynamically from the account.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/RulesEngine.cs`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Open orders are the primary subject of Rules Engine evaluation
- [`folders`](folders.md) — Moving orders into folders is a primary automated rule action
- [`shipping`](shipping.md) — Assigning postal services and shipping methods based on weight/destination
- [`extended_properties`](extended_properties.md) — Rules can evaluate or assign order extended properties

---

## Related Workflows

- [`modify_open_orders_by_sku`](../workflows/modify_open_orders_by_sku.md) — Can invoke `Orders.RunRulesEngine` following order line adjustments
