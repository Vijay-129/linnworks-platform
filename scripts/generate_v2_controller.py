"""
Generates a full v2 controller (models + controller class) from an OpenAPI 3 spec,
for surfaces with no v1 equivalent to port from - the spec is the only source of
truth. Builds on generate_v2_models.py's schema resolution.

Handles specs where operationId is missing (e.g. warehousetransfer-v2.json) by
deriving a method name from the HTTP verb + path. These derived names are
mechanical, not Linnworks-authored - flagged in the controller's file header so
nobody mistakes them for official method names.

Usage:
    python generate_v2_controller.py warehousetransfer-v2 WarehouseTransfer --path-prefix /warehousetransfer
"""

import argparse
import json
import pathlib
import re
import sys

PLATFORM_ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC_ROOT = PLATFORM_ROOT / "vendor" / "PublicApiSpecs" / "2.0"

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from generate_v2_models import (
    ref_name, resolve_type, merged_properties, render_enum, render_class,
    collect_dependencies,
)

VERB_PREFIX = {"get": "Get", "post": "Create", "put": "Update", "patch": "Patch", "delete": "Delete"}
CS_TYPE_MAP_SIMPLE = {"string": "String", "integer": "Int32", "boolean": "Boolean", "number": "Double"}
# "T?" is only valid C# for value types here (project targets C# 7.3 / netstandard2.0,
# no nullable reference types) - String/List<T> are reference types and are already
# nullable, so appending "?" to them is a compile error, not a no-op.
CS_VALUE_TYPES = {"Guid", "Int32", "Int64", "Boolean", "Double"}


def pascal(segment: str) -> str:
    segment = re.sub(r"[^a-zA-Z0-9]", " ", segment)
    return "".join(w[:1].upper() + w[1:] for w in segment.split())


def derive_method_name(verb: str, path: str, prefix: str) -> str:
    rel = path[len(prefix):].strip("/")
    parts = []
    for seg in rel.split("/"):
        if not seg:
            continue
        if seg.startswith("{") and seg.endswith("}"):
            parts.append("By" + pascal(seg[1:-1]))
        else:
            parts.append(pascal(seg))
    name = VERB_PREFIX.get(verb, pascal(verb)) + "".join(parts)
    return name


def dedupe_names(ops: list):
    seen = {}
    for op in ops:
        base = op["method_name"]
        seen[base] = seen.get(base, 0) + 1
        if seen[base] > 1:
            op["method_name"] = f"{base}{seen[base]}"


def response_type_for(op_spec: dict, schemas: dict) -> str:
    responses = op_spec.get("responses", {})
    for code in ("200", "201"):
        r = responses.get(code)
        if not r:
            continue
        schema = r.get("content", {}).get("application/json", {}).get("schema")
        if schema:
            return resolve_type(schema, schemas)
    return "void"


def request_body_type(op_spec: dict, schemas: dict):
    rb = op_spec.get("requestBody", {})
    schema = rb.get("content", {}).get("application/json", {}).get("schema")
    if not schema:
        return None
    return resolve_type(schema, schemas)


def param_cs_type(schema: dict) -> str:
    t = schema.get("type")
    fmt = schema.get("format")
    if t == "string" and fmt == "uuid":
        return "Guid"
    if t == "integer":
        return "Int64" if fmt == "int64" else "Int32"
    if t == "boolean":
        return "Boolean"
    if t == "array":
        item = schema.get("items", {})
        return f"List<{param_cs_type(item)}>"
    return "String"


def extract_operations(spec: dict, path_prefix: str):
    ops = []
    for path, methods in spec["paths"].items():
        if not path.startswith(path_prefix):
            continue
        for verb, op_spec in methods.items():
            if verb not in VERB_PREFIX:
                continue
            operation_id = op_spec.get("operationId")
            method_name = operation_id if operation_id else derive_method_name(verb, path, path_prefix)
            path_params = [p for p in op_spec.get("parameters", []) if p.get("in") == "path"]
            query_params = [p for p in op_spec.get("parameters", []) if p.get("in") == "query"]
            ops.append({
                "verb": verb.upper(),
                "path": path,
                "method_name": method_name,
                "derived": operation_id is None,
                "path_params": path_params,
                "query_params": query_params,
                "op_spec": op_spec,
                "summary": op_spec.get("summary") or op_spec.get("description") or "",
            })
    dedupe_names(ops)
    return ops


def collect_all_schema_refs(ops: list, schemas: dict) -> set:
    seeds = set()
    for op in ops:
        rb = op["op_spec"].get("requestBody", {})
        body_schema = rb.get("content", {}).get("application/json", {}).get("schema")
        if body_schema:
            for ref in re.findall(r'"\$ref":\s*"#/components/schemas/(\w+)"', json.dumps(body_schema)):
                seeds.add(ref)
        for code in ("200", "201"):
            r = op["op_spec"].get("responses", {}).get(code)
            if not r:
                continue
            resp_schema = r.get("content", {}).get("application/json", {}).get("schema")
            if resp_schema:
                for ref in re.findall(r'"\$ref":\s*"#/components/schemas/(\w+)"', json.dumps(resp_schema)):
                    seeds.add(ref)
    return collect_dependencies(list(seeds), schemas)


def render_controller(controller_name: str, ops: list, schemas: dict, spec_file_rel: str) -> str:
    lines = [
        "using System;",
        "using System.Collections.Generic;",
        "",
        "namespace LinnworksAPI.V2",
        "{",
        "    /// <summary>",
        f"    /// v2 {controller_name}. No v1 SDK equivalent exists to port from - generated directly",
        f"    /// against {spec_file_rel} via scripts/generate_v2_controller.py.",
        "    /// This spec has no operationId on most operations, so method names below were",
        "    /// mechanically derived from HTTP verb + path, not Linnworks-authored - treat them",
        "    /// as provisional pending confirmation against real usage or official docs.",
        "    /// </summary>",
        f"    public class {controller_name}Controller",
        "    {",
        "        private readonly ApiContextV2 apiContext;",
        "",
        f"        public {controller_name}Controller(ApiContextV2 apiContext)",
        "        {",
        "            this.apiContext = apiContext;",
        "        }",
        "",
    ]

    for op in ops:
        ret_type = response_type_for(op["op_spec"], schemas)
        body_type = request_body_type(op["op_spec"], schemas)

        cs_params = []
        for pp in op["path_params"]:
            cs_params.append(f"{param_cs_type(pp.get('schema', {}))} {pp['name']}")
        if body_type:
            cs_params.append(f"{body_type} body")
        for qp in op["query_params"]:
            t = param_cs_type(qp.get("schema", {}))
            if t in CS_VALUE_TYPES and not qp.get("required", False):
                t += "?"
            cs_params.append(f"{t} {qp['name']} = null" if not qp.get("required", False) else f"{t} {qp['name']}")

        method_ret = "void" if ret_type == "void" else ret_type
        lines.append("        /// <summary>")
        lines.append(f"        /// {op['summary'] or op['method_name']}")
        if op["derived"]:
            lines.append("        /// (method name derived from path - no operationId in spec)")
        lines.append("        /// </summary>")
        lines.append(f"        public {method_ret} {op['method_name']}({', '.join(cs_params)})")
        lines.append("        {")

        path_expr = op["path"]
        for pp in op["path_params"]:
            path_expr = path_expr.replace("{" + pp["name"] + "}", "{" + pp["name"] + "}")
        path_literal = f'$"{path_expr.lstrip("/")}"' if op["path_params"] else f'"{path_expr.lstrip("/")}"'

        if op["query_params"]:
            lines.append("            var query = new Dictionary<string, string>")
            lines.append("            {")
            for qp in op["query_params"]:
                lines.append(f'                ["{qp["name"]}"] = {qp["name"]}?.ToString(),')
            lines.append("            };")
            query_arg = "query"
        else:
            query_arg = "null"

        body_arg = "body" if body_type else "null"
        call_generic = f"<{ret_type}>" if ret_type != "void" else ""
        send = f'RestClient.Send{call_generic}(apiContext, "{op["verb"]}", {path_literal}, {query_arg}, {body_arg})'
        if ret_type == "void":
            lines.append(f"            {send};")
        else:
            lines.append(f"            return {send};")
        lines.append("        }")
        lines.append("")

    if lines[-1] == "":
        lines.pop()
    lines.append("    }")
    lines.append("}")
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("spec_stem")
    parser.add_argument("controller_name")
    parser.add_argument("--path-prefix", required=True)
    args = parser.parse_args()

    spec_path = SPEC_ROOT / f"{args.spec_stem}.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    schemas = spec["components"]["schemas"]

    ops = extract_operations(spec, args.path_prefix)
    print(f"{len(ops)} operations found")

    all_schema_names = collect_all_schema_refs(ops, schemas)
    out_dir = PLATFORM_ROOT / "LinnworksAPI" / "V2" / "Controllers" / args.controller_name
    models_dir = out_dir / "Models"
    models_dir.mkdir(parents=True, exist_ok=True)

    for name in sorted(all_schema_names):
        schema = schemas[name]
        content = render_enum(name, schema) if "enum" in schema else render_class(name, schema, schemas)
        (models_dir / f"{name}.cs").write_text(content, encoding="utf-8")
    print(f"{len(all_schema_names)} model(s) written to {models_dir.relative_to(PLATFORM_ROOT)}")

    controller_src = render_controller(args.controller_name, ops, schemas, f"linnworks-api-python-main/PublicApiSpecs/2.0/{args.spec_stem}.json")
    controller_path = out_dir / f"{args.controller_name}Controller.cs"
    controller_path.write_text(controller_src, encoding="utf-8")
    print(f"Controller written to {controller_path.relative_to(PLATFORM_ROOT)}")

    derived = [op["method_name"] for op in ops if op["derived"]]
    print(f"\n{len(derived)}/{len(ops)} method names were derived (no operationId in spec):")
    for n in derived:
        print(f"  {n}")


if __name__ == "__main__":
    main()
