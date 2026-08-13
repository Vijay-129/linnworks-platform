# legacy/

`LinnworksAPI-v1-source/` is a frozen copy of the pre-rewrite SDK's `Controllers/`,
`Interfaces/`, and `ClassBase/` folders (from the original `linnworks-api-master`
checkout, before this repo existed). It is **not built** as part of this project -
it exists only as input for two scripts:

- `scripts/port_controller.py` — copied working v1 controller code out of here into
  `LinnworksAPI/V1/Controllers/<Name>/`
- `scripts/reverse_document_controller.py` — derived `references/api/v1/*.md` for
  controllers with no `PublicApiSpecs` file, straight from this code's XML doc comments

Every controller has already been ported once - see `migration/STATUS.md` (all rows
`done`). This folder is kept for history and in case the legacy source ever needs
re-diffing, not because anything here still needs to happen. Do not add new code to
it, and do not reference it from anything under `LinnworksAPI/`.
