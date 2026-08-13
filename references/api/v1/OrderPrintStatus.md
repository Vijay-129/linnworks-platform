<!-- REVERSE-DOCUMENTED by scripts/reverse_document_controller.py. No PublicApiSpecs file exists for this controller - this was derived from the old repo's working C# code, not from a Linnworks-published spec. Lower confidence than sync_api_spec.py output: no rate limits, no official descriptions. If Linnworks publishes a spec for this controller, delete this file and run sync_api_spec.py instead. -->

# OrderPrintStatus (v1, reverse-documented)

Source: `LinnworksAPI/Controllers/OrderPrintStatus.cs`  
_Last synced: 2026-08-13_

## Endpoints

| Method | Path | C# signature |
|---|---|---|
| POST | `/api/OrderPrintStatus/SetOrderPrintStatus` | `void SetOrderPrintStatus(PrintJobProcessedDto result)` |

### POST `/api/OrderPrintStatus/SetOrderPrintStatus`

Set Order Print Status flag in database so that on refresh, Open orders "printed" flag is highlighted.

- `result`: Processed Print job information

`void SetOrderPrintStatus(PrintJobProcessedDto result)`
