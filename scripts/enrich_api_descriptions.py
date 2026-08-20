"""
Parses vendor/LinnworksNetSDK/ (Linnworks' official developer SDK - C# source with
real XML doc comments) into two lookups sync_api_spec.py uses to fill in description
text the OpenAPI/Swagger specs in vendor/PublicApiSpecs/ don't carry: per-model-
property descriptions (ClassBase/*.cs) and per-endpoint-method summaries/param
descriptions (Controllers/*.cs, used only as a fallback where the JSON spec's own
description is empty - the JSON spec's endpoint descriptions are usually already
populated, unlike model property descriptions which sync_api_spec.py previously
discarded entirely).

Standalone usage (writes vendor/LinnworksNetSDK/descriptions.json for inspection):
    python enrich_api_descriptions.py

Importable (what sync_api_spec.py actually uses):
    from enrich_api_descriptions import load_descriptions
    d = load_descriptions()
    d["models"]["AddOrdersNoteRequest"]["OrderIds"]      # "List of order Ids"
    d["methods"]["Orders"]["GetOrderById"]["summary"]
    d["methods"]["Orders"]["GetOrderById"]["params"]["pkOrderId"]
"""

import json
import pathlib
import re

PLATFORM_ROOT = pathlib.Path(__file__).resolve().parent.parent
VENDOR_SDK_DIR = PLATFORM_ROOT / "vendor" / "LinnworksNetSDK"

_SUMMARY_LINE_RE = re.compile(r"^\s*///\s?(.*)$")
_CLASS_OR_ENUM_RE = re.compile(r"\bpublic\s+(?:sealed\s+)?(?:class|enum)\s+(\w+)")

# Property doc comment: /// <summary> ... /// </summary> immediately followed
# (skipping any other /// tag lines, e.g. none expected for properties but harmless
# if present) by `public <Type> <Name> { get`.
_PROPERTY_RE = re.compile(
    r"///\s*<summary>\s*\n"
    r"(?P<summary>(?:\s*///.*\n)+?)"
    r"\s*///\s*</summary>\s*\n"
    r"(?:\s*///[^\n]*\n)*?"
    r"\s*public\s+[\w<>\[\],\.\? ]+?\s+(?P<name>\w+)\s*\{\s*get"
)

# Method doc comment: /// <summary> ... /// </summary>, then zero or more
# /// <param name="x">...</param> lines, then optionally /// <returns>, then the
# method signature `public <ReturnType> <Name>(`.
_METHOD_RE = re.compile(
    r"///\s*<summary>\s*\n"
    r"(?P<summary>(?:\s*///.*\n)+?)"
    r"\s*///\s*</summary>\s*\n"
    r"(?P<paramblock>(?:\s*///\s*<param[^\n]*\n)*)"
    r"(?:\s*///[^\n]*\n)*?"
    r"\s*public\s+[\w<>\[\],\.\? ]+?\s+(?P<name>\w+)\s*\("
)
_PARAM_RE = re.compile(r'<param name="(\w+)">(.*?)</param>')


def _clean_summary(raw: str) -> str:
    lines = [m.group(1).strip() for line in raw.splitlines() if (m := _SUMMARY_LINE_RE.match(line))]
    return " ".join(line for line in lines if line).strip()


def _parse_models(classbase_dir: pathlib.Path) -> dict:
    models = {}
    for f in sorted(classbase_dir.glob("*.cs")):
        text = f.read_text(encoding="utf-8", errors="replace")
        class_match = _CLASS_OR_ENUM_RE.search(text)
        if not class_match:
            continue
        props = {}
        for m in _PROPERTY_RE.finditer(text):
            desc = _clean_summary(m.group("summary"))
            if desc:
                props[m.group("name")] = desc
        if props:
            models[class_match.group(1)] = props
    return models


def _parse_controllers(controllers_dir: pathlib.Path) -> dict:
    controllers = {}
    for f in sorted(controllers_dir.glob("*.cs")):
        text = f.read_text(encoding="utf-8", errors="replace")
        methods = {}
        for m in _METHOD_RE.finditer(text):
            summary = _clean_summary(m.group("summary"))
            params = {name: desc.strip() for name, desc in _PARAM_RE.findall(m.group("paramblock"))}
            if summary or params:
                methods[m.group("name")] = {"summary": summary, "params": params}
        if methods:
            controllers[f.stem] = methods
    return controllers


def load_descriptions(vendor_dir: pathlib.Path = VENDOR_SDK_DIR) -> dict:
    classbase_dir = vendor_dir / "ClassBase"
    controllers_dir = vendor_dir / "Controllers"
    return {
        "models": _parse_models(classbase_dir) if classbase_dir.exists() else {},
        "methods": _parse_controllers(controllers_dir) if controllers_dir.exists() else {},
    }


def main():
    descriptions = load_descriptions()
    out_path = VENDOR_SDK_DIR / "descriptions.json"
    out_path.write_text(json.dumps(descriptions, indent=2, sort_keys=True), encoding="utf-8")

    n_models = len(descriptions["models"])
    n_props = sum(len(p) for p in descriptions["models"].values())
    n_controllers = len(descriptions["methods"])
    n_methods = sum(len(m) for m in descriptions["methods"].values())
    total_model_files = len(list((VENDOR_SDK_DIR / "ClassBase").glob("*.cs"))) if (VENDOR_SDK_DIR / "ClassBase").exists() else 0
    print(
        f"{n_models}/{total_model_files} model files yielded descriptions "
        f"({n_props} described properties); {n_controllers} controllers "
        f"({n_methods} described methods) -> {out_path.relative_to(PLATFORM_ROOT)}"
    )


if __name__ == "__main__":
    main()
