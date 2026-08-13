"""
Reads vendor/PublicApiSpecs (a local copy - see vendor/README.md for how to refresh
it) and regenerates references/api/v1/<Controller>.md and references/api/v2/<Controller>.md,
then updates the Spec Version / Last Synced / Status columns in migration/STATUS.md.

Handles both spec formats found in this API: Swagger 2.0 (PublicApiSpecs/1.0/*.json)
and OpenAPI 3.0 (PublicApiSpecs/2.0/*.json).

Never hand-edit references/api/**.md - edit is meaningless, the source is the spec
file. If a spec file changes, re-run this script.

Usage:
    python sync_api_spec.py
    python sync_api_spec.py --old-repo "C:\\path\\to\\a-refreshed-vendor-dir"
    python sync_api_spec.py --controller Orders
"""

import argparse
import datetime
import html
import json
import pathlib
import re
import sys

PLATFORM_ROOT = pathlib.Path(__file__).resolve().parent.parent
DEFAULT_OLD_REPO = PLATFORM_ROOT / "vendor"
SPEC_SUBDIR = "PublicApiSpecs"
STATUS_FILE = PLATFORM_ROOT / "migration" / "STATUS.md"

# spec file stem -> canonical controller name (must match migration/STATUS.md and
# the eventual LinnworksAPI/V{1,2}/Controllers/<Name> folder names).
CONTROLLER_NAME_MAP = {
    "auth": "Auth",
    "customer": "Customer",
    "dashboards": "Dashboards",
    "email": "Email",
    "genericlistings": "GenericListings",
    "importexport": "ImportExport",
    "inventory": "Inventory",
    "listings": "Listings",
    "locations": "Locations",
    "macro": "Macro",
    "openorders": "OpenOrders",
    "orders": "Orders",
    "picking": "Picking",
    "postalservices": "PostalServices",
    "postsale": "PostSale",
    "printservice": "PrintService",
    "processedorders": "ProcessedOrders",
    "purchaseorder": "PurchaseOrder",
    "returnsrefunds": "ReturnsRefunds",
    "rulesengine": "RulesEngine",
    "settings": "Settings",
    "shippingservice": "ShippingService",
    "shipstation": "ShipStation",
    "stock": "Stock",
    "warehousetransfer": "WarehouseTransfer",
    "wms": "Wms",
    "orders-v2": "Orders",
    "warehousetransfer-v2": "WarehouseTransfer",
}
# Draft/alternate specs that exist alongside a canonical one for the same
# controller - documented, but not used to update STATUS.md (ambiguous which
# spec is "the" source of truth until a human decides).
UNMAPPED_SUFFIXES = ["-new"]


def clean_description(desc: str):
    """Strip HTML, unescape entities, and pull out the rate limit if present."""
    if not desc:
        return "", None
    text = re.sub(r"<[^>]+>", "", desc)
    text = html.unescape(text)
    m = re.search(r"(\d+)\s*/\s*minute", text)
    rate = m.group(1) if m else None
    text = re.sub(r"Rate limit:\s*\d+\s*/\s*minute", "", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text, rate


def ref_name(ref: str) -> str:
    return ref.rsplit("/", 1)[-1]


def param_type(p: dict) -> str:
    if "type" in p:
        t = p["type"]
    else:
        schema = p.get("schema", {})
        if "$ref" in schema:
            return ref_name(schema["$ref"])
        t = schema.get("type", "object")
        if t == "array":
            items = schema.get("items", {})
            if "$ref" in items:
                return ref_name(items["$ref"]) + "[]"
            return items.get("type", "object") + "[]"
    return t


def response_schema(op: dict):
    responses = op.get("responses", {})
    ok = responses.get("200") or responses.get("204") or {}
    schema = ok.get("schema")
    if schema is None:
        content = ok.get("content", {})
        aj = content.get("application/json", {})
        schema = aj.get("schema")
    if not schema:
        return None
    if "$ref" in schema:
        return ref_name(schema["$ref"])
    if "oneOf" in schema:
        return " | ".join(ref_name(r["$ref"]) for r in schema["oneOf"] if "$ref" in r)
    if schema.get("type") == "array":
        items = schema.get("items", {})
        if "$ref" in items:
            return ref_name(items["$ref"]) + "[]"
        return items.get("type", "object") + "[]"
    return schema.get("type", "object")


def request_body_schema(op: dict, params: list):
    # Swagger 2.0: a parameter with in=body carries the schema.
    for p in params:
        if p.get("in") == "body":
            schema = p.get("schema", {})
            if "$ref" in schema:
                return ref_name(schema["$ref"])
    # OpenAPI 3.0: requestBody.content.application/json.schema
    rb = op.get("requestBody", {})
    schema = rb.get("content", {}).get("application/json", {}).get("schema", {})
    if "$ref" in schema:
        return ref_name(schema["$ref"])
    if "oneOf" in schema:
        return " | ".join(ref_name(r["$ref"]) for r in schema["oneOf"] if "$ref" in r)
    return None


def normalize_endpoints(spec: dict):
    """Returns a list of endpoint dicts, format-agnostic (swagger2 or openapi3)."""
    endpoints = []
    for path, methods in spec.get("paths", {}).items():
        for verb, op in methods.items():
            if verb.lower() not in ("get", "post", "put", "delete", "patch"):
                continue
            desc, rate = clean_description(op.get("description", ""))
            params = [
                p for p in op.get("parameters", []) if p.get("in") in ("query", "path")
            ]
            endpoints.append(
                {
                    "method": verb.upper(),
                    "path": path,
                    "operation_id": op.get("operationId", op.get("summary", "")),
                    "summary": op.get("summary", ""),
                    "description": desc,
                    "rate_limit": rate,
                    "params": [
                        {
                            "name": p.get("name"),
                            "in": p.get("in"),
                            "type": param_type(p),
                            "required": p.get("required", False),
                        }
                        for p in params
                    ],
                    "request_model": request_body_schema(op, op.get("parameters", [])),
                    "response_model": response_schema(op),
                }
            )
    return endpoints


def get_definitions(spec: dict) -> dict:
    if "definitions" in spec:
        return spec["definitions"]
    return spec.get("components", {}).get("schemas", {})


def model_properties(defn: dict):
    props = defn.get("properties", {})
    out = []
    for name, p in props.items():
        if "$ref" in p:
            t = ref_name(p["$ref"])
        elif p.get("type") == "array":
            items = p.get("items", {})
            t = (ref_name(items["$ref"]) if "$ref" in items else items.get("type", "object")) + "[]"
        else:
            t = p.get("type", "object")
        out.append((name, t))
    return out


def generated_banner():
    return (
        "<!-- GENERATED by scripts/sync_api_spec.py. Do not hand-edit - the source "
        "is the spec file in linnworks-api-python-main/PublicApiSpecs. -->\n"
    )


def render_markdown(controller: str, version: str, spec_file: pathlib.Path, spec: dict, endpoints: list, extra_note: str = ""):
    defs = get_definitions(spec)
    referenced = set()
    for e in endpoints:
        for m in (e["request_model"], e["response_model"]):
            if m:
                for name in m.split(" | "):
                    referenced.add(name.replace("[]", ""))

    out = [generated_banner()]
    out.append(f"# {controller} ({version})\n")
    out.append(f"Source: `{spec_file.as_posix()}`  \n_Last synced: {datetime.date.today().isoformat()}_\n")
    if extra_note:
        out.append(f"> {extra_note}\n")

    out.append("## Endpoints\n")
    out.append("| Method | Path | Summary | Rate limit | Request model | Response model |")
    out.append("|---|---|---|---|---|---|")
    for e in endpoints:
        rate = f"{e['rate_limit']}/min" if e["rate_limit"] else "-"
        req = e["request_model"] or "-"
        resp = e["response_model"] or "-"
        out.append(f"| {e['method']} | `{e['path']}` | {e['summary']} | {rate} | {req} | {resp} |")
    out.append("")

    for e in endpoints:
        out.append(f"### {e['method']} `{e['path']}`\n")
        if e["description"]:
            out.append(e["description"] + "\n")
        if e["params"]:
            out.append("| Param | In | Type | Required |")
            out.append("|---|---|---|---|")
            for p in e["params"]:
                out.append(f"| `{p['name']}` | {p['in']} | `{p['type']}` | {p['required']} |")
            out.append("")

    if referenced:
        out.append("## Models\n")
        for name in sorted(referenced):
            defn = defs.get(name)
            if not defn:
                continue
            out.append(f"### `{name}`\n")
            props = model_properties(defn)
            if props:
                out.append("| Property | Type |")
                out.append("|---|---|")
                for pname, ptype in props:
                    out.append(f"| `{pname}` | `{ptype}` |")
                out.append("")

    return "\n".join(out)


def status_to_row_key(controller: str, version: str) -> str:
    return f"{controller.lower()}|{version}"


def update_status_md(synced: dict):
    """synced: {(controller, version): spec_info_version} - updates matching rows
    in migration/STATUS.md in place, preserving Notes and unmatched rows."""
    if not STATUS_FILE.exists() or not synced:
        return 0
    lines = STATUS_FILE.read_text(encoding="utf-8").splitlines()
    today = datetime.date.today().isoformat()
    updated = 0
    for i, line in enumerate(lines):
        if not line.startswith("|") or line.startswith("|---"):
            continue
        cols = [c.strip() for c in line.strip("|").split("|")]
        if len(cols) != 6:
            continue
        controller, spec_version, api_version, last_synced, status, notes = cols
        if controller in ("Controller",):
            continue
        key = status_to_row_key(controller, api_version)
        if key in synced:
            new_spec_version = synced[key]
            new_status = "generated" if status == "todo" else status
            lines[i] = f"| {controller} | {new_spec_version} | {api_version} | {today} | {new_status} | {notes} |"
            updated += 1
    STATUS_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return updated


def sync_file(spec_path: pathlib.Path, version: str, out_dir: pathlib.Path, old_repo: pathlib.Path, only_controller: str = None):
    stem = spec_path.stem  # e.g. "orders", "orders-v2", "warehousetransfer-new"
    lookup_key = stem
    extra_note = ""
    unmapped = False
    if any(stem.endswith(suf) for suf in UNMAPPED_SUFFIXES):
        base = stem
        for suf in UNMAPPED_SUFFIXES:
            base = base.removesuffix(suf)
        controller = CONTROLLER_NAME_MAP.get(base, base.title())
        extra_note = f"Draft/alternate spec ({spec_path.name}) alongside the canonical `{base}.json`. Not reflected in migration/STATUS.md automatically - confirm which spec is authoritative before promoting."
        out_name = f"{controller}-{stem.rsplit('-', 1)[-1]}"
        unmapped = True
    else:
        controller = CONTROLLER_NAME_MAP.get(lookup_key, stem.title())
        out_name = controller

    if only_controller and controller.lower() != only_controller.lower():
        return None

    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    endpoints = normalize_endpoints(spec)
    md = render_markdown(controller, version, spec_path.relative_to(old_repo), spec, endpoints, extra_note)

    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / f"{out_name}.md"
    out_path.write_text(md, encoding="utf-8")

    # Not spec["info"]["version"]: Linnworks' own spec files are inconsistent there
    # (orders-v2.json's info.version is literally "v1"). The filename stem is the
    # only thing that reliably distinguishes spec revisions here.
    return {
        "out_path": out_path,
        "n_endpoints": len(endpoints),
        "controller": controller,
        "version": version,
        "spec_info_version": stem,
        "unmapped": unmapped,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--old-repo", default=str(DEFAULT_OLD_REPO))
    parser.add_argument("--controller", default=None)
    args = parser.parse_args()

    old_repo = pathlib.Path(args.old_repo)
    spec_root = old_repo / SPEC_SUBDIR
    if not spec_root.exists():
        print(f"Spec root not found at {spec_root}", file=sys.stderr)
        sys.exit(1)

    version_dirs = {"v1": spec_root / "1.0", "v2": spec_root / "2.0"}
    synced_for_status = {}
    results = []

    for version, vdir in version_dirs.items():
        if not vdir.exists():
            continue
        out_dir = PLATFORM_ROOT / "references" / "api" / version
        for spec_path in sorted(vdir.glob("*.json")):
            result = sync_file(spec_path, version, out_dir, old_repo, args.controller)
            if result is None:
                continue
            results.append(result)
            rel = result["out_path"].relative_to(PLATFORM_ROOT)
            flag = " (draft, not tracked in STATUS.md)" if result["unmapped"] else ""
            print(f"{version}  {result['controller']:20s} -> {rel}  ({result['n_endpoints']} endpoint(s)){flag}")
            if not result["unmapped"]:
                key = status_to_row_key(result["controller"], version)
                synced_for_status[key] = result["spec_info_version"]

    updated_rows = update_status_md(synced_for_status)
    print(f"\nmigration/STATUS.md: {updated_rows} row(s) updated")


if __name__ == "__main__":
    main()
