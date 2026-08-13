"""
Ports one controller from the archived legacy v1 source (legacy/LinnworksAPI-v1-source/,
a frozen copy of the pre-rewrite SDK - see legacy/README.md) into
LinnworksAPI/V1/Controllers/<Name>/, automating what was done by hand for
Locations/Auth/PostalServices:

1. Copy Controllers/<Name>.cs and Interfaces/I<Name>.cs into the new per-controller folder.
2. Recursively resolve every model type they reference (BFS over ClassBase/*.cs),
   skipping types that already exist somewhere in the new repo (Core/Shared/other
   controllers already ported).
3. For each newly-needed type, check whether any OTHER controller in the legacy source
   also references it. If so, it's cross-controller -> Shared/Common/. Otherwise it's
   local -> V1/Controllers/<Name>/Models/.

This does not fix formatting or verify against the spec - that's still a human step
(compare against references/api/v1/<Name>.md, then flip migration/STATUS.md to `done`
once it builds and matches).

Every controller has already been ported once (see migration/STATUS.md) - this script
is now mainly useful for reference/history, or if the legacy source ever needs
re-diffing against a newer export from Linnworks.

Usage:
    python port_controller.py Orders
    python port_controller.py Orders --old-repo "C:\\path\\to\\a-different-legacy-source"
"""

import argparse
import pathlib
import re
import sys

PLATFORM_ROOT = pathlib.Path(__file__).resolve().parent.parent
DEFAULT_OLD_REPO = PLATFORM_ROOT / "legacy" / "LinnworksAPI-v1-source"

TYPE_DECL_RE = re.compile(
    r"public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(?:class|enum|struct|interface)\s+(\w+)"
)
# Not anchored to PascalCase: the old SDK has at least one model file with a
# lowercase-leading class name (ClassBase/batchAssignment.cs), so this must not
# assume an uppercase first letter or it silently misses real referenced types.
IDENTIFIER_RE = re.compile(r"\b(\w+)\b")

# BCL / already-handled names that should never be treated as a portable ClassBase
# model even if a same-named file doesn't exist - keeps the identifier scan from
# wasting cycles on obvious noise.
IGNORE_NAMES = {
    "String", "Boolean", "Int32", "Int64", "Guid", "DateTime", "List", "Dictionary",
    "IEnumerable", "Object", "Double", "Decimal", "Byte", "Task", "Void", "Newtonsoft",
    "System", "Json", "JsonConvert", "JsonFormatter", "WebUtility", "Encoding", "Stream",
    "StreamReader", "StreamWriter", "IO", "Text", "Collections", "Generic", "Threading",
    "Tasks", "Net", "LinnworksAPI",
}


def read(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def build_existing_type_registry(platform_root: pathlib.Path) -> set:
    """Every type already declared anywhere in the new repo - don't re-port these."""
    registry = set()
    linnworks_api_dir = platform_root / "LinnworksAPI"
    for cs_file in linnworks_api_dir.rglob("*.cs"):
        for m in TYPE_DECL_RE.finditer(read(cs_file)):
            registry.add(m.group(1))
    return registry


def build_classbase_index(old_repo: pathlib.Path) -> dict:
    """type name -> ClassBase/<name>.cs path, for every model file in the old repo."""
    classbase_dir = old_repo / "ClassBase"
    return {p.stem: p for p in classbase_dir.glob("*.cs")}


def referenced_type_names(text: str) -> set:
    return {m.group(1) for m in IDENTIFIER_RE.finditer(text) if m.group(1) not in IGNORE_NAMES}


def controller_and_interface_paths(old_repo: pathlib.Path, name: str):
    controller = old_repo / "Controllers" / f"{name}.cs"
    interface = old_repo / "Interfaces" / f"I{name}.cs"
    if not controller.exists():
        raise FileNotFoundError(f"No controller file at {controller}")
    if not interface.exists():
        raise FileNotFoundError(f"No interface file at {interface}")
    return controller, interface


def is_referenced_elsewhere(old_repo: pathlib.Path, type_name: str, exclude_controller: str) -> bool:
    controllers_dir = old_repo / "Controllers"
    pattern = re.compile(r"\b" + re.escape(type_name) + r"\b")
    for cs_file in controllers_dir.glob("*.cs"):
        if cs_file.stem == exclude_controller:
            continue
        if pattern.search(read(cs_file)):
            return True
    return False


def resolve_models(old_repo: pathlib.Path, name: str, seed_text: str, existing_registry: set):
    """BFS over ClassBase to find every model the controller needs that isn't already
    present somewhere in the new repo. Returns {type_name: (source_path, is_shared)}."""
    classbase_index = build_classbase_index(old_repo)
    to_visit = list(referenced_type_names(seed_text) & set(classbase_index))
    visited = set()
    result = {}

    while to_visit:
        type_name = to_visit.pop()
        if type_name in visited:
            continue
        visited.add(type_name)

        if type_name in existing_registry:
            continue  # already ported somewhere in the new repo

        src = classbase_index.get(type_name)
        if not src:
            continue

        shared = is_referenced_elsewhere(old_repo, type_name, name)
        result[type_name] = (src, shared)

        # follow this model's own dependencies
        nested = referenced_type_names(read(src)) & set(classbase_index)
        for n in nested:
            if n not in visited:
                to_visit.append(n)

    return result


def port(old_repo: pathlib.Path, name: str):
    controller_src, interface_src = controller_and_interface_paths(old_repo, name)

    existing_registry = build_existing_type_registry(PLATFORM_ROOT)
    combined_text = read(controller_src) + "\n" + read(interface_src)
    models = resolve_models(old_repo, name, combined_text, existing_registry)

    out_dir = PLATFORM_ROOT / "LinnworksAPI" / "V1" / "Controllers" / name
    models_dir = out_dir / "Models"
    shared_dir = PLATFORM_ROOT / "LinnworksAPI" / "Shared" / "Common"
    out_dir.mkdir(parents=True, exist_ok=True)

    controller_dst = out_dir / f"{name}Controller.cs"
    interface_dst = out_dir / f"I{name}Controller.cs"
    controller_dst.write_text(read(controller_src), encoding="utf-8")
    interface_dst.write_text(read(interface_src), encoding="utf-8")

    report = {"controller": controller_dst, "interface": interface_dst, "local": [], "shared": []}

    for type_name, (src, shared) in sorted(models.items()):
        if shared:
            shared_dir.mkdir(parents=True, exist_ok=True)
            dst = shared_dir / f"{type_name}.cs"
            report["shared"].append((type_name, dst))
        else:
            models_dir.mkdir(parents=True, exist_ok=True)
            dst = models_dir / f"{type_name}.cs"
            report["local"].append((type_name, dst))
        if not dst.exists():
            dst.write_text(read(src), encoding="utf-8")

    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("controller", help="Controller name, e.g. Orders")
    parser.add_argument("--old-repo", default=str(DEFAULT_OLD_REPO))
    args = parser.parse_args()

    old_repo = pathlib.Path(args.old_repo)
    if not old_repo.exists():
        print(f"Old repo not found at {old_repo}", file=sys.stderr)
        sys.exit(1)

    try:
        report = port(old_repo, args.controller)
    except FileNotFoundError as e:
        print(str(e), file=sys.stderr)
        sys.exit(1)

    print(f"Controller: {report['controller'].relative_to(PLATFORM_ROOT)}")
    print(f"Interface:  {report['interface'].relative_to(PLATFORM_ROOT)}")
    print(f"\nLocal models ({len(report['local'])}):")
    for type_name, dst in report["local"]:
        print(f"  {type_name} -> {dst.relative_to(PLATFORM_ROOT)}")
    print(f"\nShared models ({len(report['shared'])}) - referenced by other controllers too:")
    for type_name, dst in report["shared"]:
        print(f"  {type_name} -> {dst.relative_to(PLATFORM_ROOT)}")

    print(
        "\nNext: dotnet build, diff endpoints against references/api/v1/"
        f"{args.controller}.md, wire into Core/ApiObjectManager.cs, update migration/STATUS.md."
    )


if __name__ == "__main__":
    main()
