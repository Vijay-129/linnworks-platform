"""
Generates C# POCOs from an OpenAPI 3 spec's components.schemas, for the v2
controllers where there's no existing C# implementation to port from (unlike v1,
where port_controller.py copies working code). The spec is the only source of
truth here, so this generates directly from it.

Simplifications, called out because they affect fidelity:
- allOf is flattened into one class (union of all member properties) rather than
  modeled as C# inheritance - simpler and behaves identically for JSON (de)serialization.
- oneOf picks the first ref as the field's type and adds a comment; C# has no
  native union type. If the API can genuinely return either shape with different
  fields, deserializing the "wrong" one will just leave those fields null/default.
- additionalProperties/free-form objects become `object`.

Usage:
    python generate_v2_models.py orders-v2 GetOrdersResponse AnonymousGetOrdersResponse FulfillmentStatusRequest OrderFulfillmentStatus UpdateFulfillmentStatusesResponse FulfillmentStatus
"""

import argparse
import json
import pathlib
import re
import sys

PLATFORM_ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC_ROOT = PLATFORM_ROOT / "vendor" / "PublicApiSpecs" / "2.0"

CS_KEYWORDS = {"namespace", "class", "object", "string", "params"}


def ref_name(ref: str) -> str:
    return ref.rsplit("/", 1)[-1]


def csharp_identifier(name: str) -> str:
    return name + "Value" if name in CS_KEYWORDS else name


def resolve_type(schema: dict, schemas: dict) -> str:
    if not schema:
        return "object"
    if "$ref" in schema:
        return ref_name(schema["$ref"])
    if "oneOf" in schema:
        refs = [o["$ref"] for o in schema["oneOf"] if "$ref" in o]
        return ref_name(refs[0]) if refs else "object"
    t = schema.get("type")
    fmt = schema.get("format")
    nullable = schema.get("nullable", False)
    if t == "array":
        item_type = resolve_type(schema.get("items", {}), schemas)
        return f"List<{item_type}>"
    if t == "string":
        if fmt == "uuid":
            return "Guid?" if nullable else "Guid"
        if fmt == "date-time":
            return "DateTime?" if nullable else "DateTime"
        return "String"
    if t == "integer":
        base_t = "Int64" if fmt == "int64" else "Int32"
        return base_t + "?" if nullable else base_t
    if t == "number":
        return "Double?" if nullable else "Double"
    if t == "boolean":
        return "Boolean?" if nullable else "Boolean"
    if t == "object" or t is None:
        return "object"
    return "object"


def merged_properties(schema: dict, schemas: dict) -> dict:
    """Flattens allOf into one property dict (name -> schema)."""
    props = {}
    if "allOf" in schema:
        for member in schema["allOf"]:
            if "$ref" in member:
                props.update(merged_properties(schemas[ref_name(member["$ref"])], schemas))
            else:
                props.update(member.get("properties", {}))
    props.update(schema.get("properties", {}))
    return props


def render_enum(name: str, schema: dict) -> str:
    values = schema["enum"]
    lines = [
        "using Newtonsoft.Json;",
        "using Newtonsoft.Json.Converters;",
        "",
        "namespace LinnworksAPI.V2",
        "{",
        "    [JsonConverter(typeof(StringEnumConverter))]",
        f"    public enum {name}",
        "    {",
    ]
    for v in values:
        lines.append(f"        {csharp_identifier(v)},")
    lines.append("    }")
    lines.append("}")
    return "\n".join(lines) + "\n"


def render_class(name: str, schema: dict, schemas: dict) -> str:
    props = merged_properties(schema, schemas)
    lines = [
        "using System;",
        "using System.Collections.Generic;",
        "",
        "namespace LinnworksAPI.V2",
        "{",
    ]
    desc = schema.get("description")
    if desc and desc != name:
        lines.append("    /// <summary>")
        lines.append(f"    /// {desc}")
        lines.append("    /// </summary>")
    lines.append(f"    public class {name}")
    lines.append("    {")
    for pname, pschema in props.items():
        ptype = resolve_type(pschema, schemas)
        note = ""
        if "oneOf" in pschema:
            note = "  // oneOf in spec - see generate_v2_models.py docstring"
        lines.append(f"        public {ptype} {csharp_identifier(pname)} {{ get; set; }}{note}")
        lines.append("")
    if lines[-1] == "":
        lines.pop()
    lines.append("    }")
    lines.append("}")
    return "\n".join(lines) + "\n"


def collect_dependencies(seed_names: list, schemas: dict) -> set:
    to_visit = list(seed_names)
    visited = set()
    while to_visit:
        name = to_visit.pop()
        if name in visited or name not in schemas:
            visited.add(name)
            continue
        visited.add(name)
        schema = schemas[name]
        for ref in re.findall(r'"\$ref":\s*"#/components/schemas/(\w+)"', json.dumps(schema)):
            if ref not in visited:
                to_visit.append(ref)
    return visited & set(schemas)


def generate(spec_stem: str, seed_names: list, out_dir: pathlib.Path):
    spec_path = SPEC_ROOT / f"{spec_stem}.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    schemas = spec["components"]["schemas"]

    all_names = collect_dependencies(seed_names, schemas)
    out_dir.mkdir(parents=True, exist_ok=True)

    written = []
    for name in sorted(all_names):
        schema = schemas[name]
        if "enum" in schema:
            content = render_enum(name, schema)
        else:
            content = render_class(name, schema, schemas)
        out_path = out_dir / f"{name}.cs"
        out_path.write_text(content, encoding="utf-8")
        written.append(out_path)

    return written


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("spec_stem", help="e.g. orders-v2")
    parser.add_argument("seed_names", nargs="+", help="Top-level schema names to start from")
    parser.add_argument("--out", default=None)
    args = parser.parse_args()

    out_dir = pathlib.Path(args.out).resolve() if args.out else PLATFORM_ROOT / "LinnworksAPI" / "V2" / "Controllers" / "Models"
    try:
        written = generate(args.spec_stem, args.seed_names, out_dir)
    except FileNotFoundError as e:
        print(str(e), file=sys.stderr)
        sys.exit(1)

    for p in written:
        print(p.relative_to(PLATFORM_ROOT))


if __name__ == "__main__":
    main()
