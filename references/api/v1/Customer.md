<!-- REVERSE-DOCUMENTED by scripts/reverse_document_controller.py. No PublicApiSpecs file exists for this controller - this was derived from the old repo's working C# code, not from a Linnworks-published spec. Lower confidence than sync_api_spec.py output: no rate limits, no official descriptions. If Linnworks publishes a spec for this controller, delete this file and run sync_api_spec.py instead. -->

# Customer (v1, reverse-documented)

Source: `Controllers/Customer.cs`  
_Last synced: 2026-08-13_

## Endpoints

| Method | Path | C# signature |
|---|---|---|
| POST | `/api/Customer/CreateNewCustomer` | `void CreateNewCustomer(CustomerAddress customerDetails)` |

### POST `/api/Customer/CreateNewCustomer`

Creates a new customer.

- `customerDetails`: Includes all the customer details

`void CreateNewCustomer(CustomerAddress customerDetails)`
