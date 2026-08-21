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
---

## Purpose

The automated business rules evaluation engine in Linnworks. The Rules Engine evaluates incoming
open orders against condition trees (e.g. order total, weight, country, channel, SKU, order tag)
and automatically executes actions (assigning postal services, moving to folders, adding order notes,
or setting extended properties).

Macros interact with the Rules Engine either by triggering batch re-evaluations (`Orders.RunRulesEngine`)
after macro modifications, or by inspecting/updating rule condition sets.

## Core identifiers

| Identifier | Type | Description |
|---|---|---|
| `pkRuleId` | `integer` | Unique ID of a rules engine rule header. |
| `pkConditionId` | `integer` | Unique ID of a condition node within a rule. |
| `pkActionId` | `integer` | Unique ID of an action attached to a condition node. |
| `RuleName` | `string` | Human-readable name of the rule (e.g. `UK Express Shipping Allocation`). |
| `RuleSetType` | `enum` / `string` | Scope of the rule (e.g. `Orders`). |

## Important models

| Model | Description |
|---|---|
| `RuleHeaderBasic` | Summary of a rule: name, order priority, enabled/disabled state. |
| `RuleConditionHeader` | Condition tree node with evaluator expressions (equals, contains, greater than). |
| `RuleAction` | Action executed when condition evaluates to true (e.g. Assign Postal Service, Assign Folder). |
| `RuleEvaluationResult` | Evaluation outcome trace for an order against rules. |

Use `get_model` to see full field lists.

## Common operations

- `Orders.RunRulesEngine` — Force Linnworks to re-run the Rules Engine over a specific list of open orders. Call this after a macro modifies order weight or items.
- `RulesEngine.GetRules` / `GetRuleHeaders` — Query existing rules configured in the account.
- `RulesEngine.AddAction` / `CopyAction` — Programmatically attach new action steps to rule conditions.
- `RulesEngine.SwapRules` — Reorder rule priority execution sequence.

## Rules Engine Flow

```
Open Order Created or Updated
       ↓
Rule 1 (Evaluation Tree) ──[True]──► Execute Rule 1 Actions ──► Stop or Continue
       │ [False]
       ▼
Rule 2 (Evaluation Tree) ──[True]──► Execute Rule 2 Actions ──► Stop or Continue
       │ [False]
       ▼
Default System Routing
```

## Gotchas

### Triggering Rules Engine after macro order changes

If a macro changes order properties (such as updating shipping address, adding items, or recalculating totals),
the Rules Engine does NOT automatically re-evaluate unless explicitly triggered. Call `Orders.RunRulesEngine`
with the list of modified order IDs.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Rule evaluation stops at the first terminal rule match

Rules are evaluated in strict priority order (`SwapRules`). Unless a rule is explicitly configured
as "continue evaluating subsequent rules", matching a rule terminates further rule execution for that order.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/rulesengine.json`

### Macros vs Rules Engine execution boundary

A macro that mutates an order can cause circular logic if its mutation triggers a rule that invokes the
same macro. Ensure idempotency flags or folder checks prevent recursive loops.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

## Related concepts

- `open_orders` — Rules Engine evaluates and routes open orders
- `folders` — Common action is moving matched orders to specific folders
- `shipping` — Common action is assigning designated postal services

## Related workflows

- `modify_open_orders_by_sku` — Can trigger `Orders.RunRulesEngine` after updates
