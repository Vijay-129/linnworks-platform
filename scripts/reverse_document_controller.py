"""
For controllers with no PublicApiSpecs file (Customer, OrderPrintStatus, OrderWorkflow),
generates references/api/v1/<Name>.md straight from the archived legacy v1 source
(legacy/LinnworksAPI-v1-source/ - see legacy/README.md) instead of leaving them
undocumented. Extracts, per method: XML doc summary, the GetResponse endpoint
path/HTTP verb actually called, and the C# signature.

This is a lower-confidence source than sync_api_spec.py (no rate limits, no
Linnworks-authored descriptions) - the output banner says so. If Linnworks ever
publishes a spec for one of these, delete the corresponding hand/script doc and run
sync_api_spec.py instead.

Usage:
    python reverse_document_controller.py Customer
    python reverse_document_controller.py Customer --old-repo "C:\\path\\to\\a-different-legacy-source"
"""

import argparse
import datetime
import pathlib
import re
import sys

PLATFORM_ROOT = pathlib.Path(__file__).resolve().parent.parent
DEFAULT_OLD_REPO = PLATFORM_ROOT / "legacy" / "LinnworksAPI-v1-source"

DOC_COMMENT_RE = re.compile(r"^\s*///\s?(.*)$")
METHOD_RE = re.compile(
    r"public\s+(?:override\s+|virtual\s+|async\s+|static\s+)*"
    r"(?P<ret>[\w<>\[\],\.\? ]+?)\s+(?P<name>\w+)\s*"
    r"\((?P<params>[^)]*)\)\s*\{"
)
GET_RESPONSE_RE = re.compile(
    r'GetResponse\(\s*"([^"]+)"\s*,\s*[^,]*(?:,\s*"([A-Z]+)")?'
)


def read(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def find_matching_brace(text: str, open_brace_idx: int) -> int:
    depth = 0
    for i in range(open_brace_idx, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return i
    return len(text) - 1


def leading_doc_comment(lines, decl_line_idx):
    """Returns (summary, [(param_name, param_doc), ...])."""
    doc_lines = []
    i = decl_line_idx - 1
    while i >= 0:
        m = DOC_COMMENT_RE.match(lines[i])
        if not m:
            break
        doc_lines.insert(0, m.group(1))
        i -= 1
    raw = " ".join(doc_lines)

    summary_m = re.search(r"<summary>(.*?)</summary>", raw)
    summary = summary_m.group(1).strip() if summary_m else re.sub(r"<[^>]+>", "", raw).strip()

    params = re.findall(r'<param name="([^"]+)">(.*?)</param>', raw)
    return summary, params


def extract_methods(text: str):
    lines = text.splitlines()
    offsets = [0]
    for line in lines:
        offsets.append(offsets[-1] + len(line) + 1)

    for m in METHOD_RE.finditer(text):
        char_idx = m.start()
        line_idx = next(i for i, off in enumerate(offsets) if off > char_idx) - 1
        summary, params_doc = leading_doc_comment(lines, line_idx)
        open_brace = text.index("{", m.end() - 1)
        close_brace = find_matching_brace(text, open_brace)
        body = text[open_brace + 1 : close_brace]

        gr = GET_RESPONSE_RE.search(body)
        endpoint = gr.group(1) if gr else None
        verb = gr.group(2) if gr and gr.group(2) else "POST"

        yield {
            "name": m.group("name"),
            "ret": m.group("ret").strip(),
            "params": m.group("params").strip(),
            "doc": summary,
            "params_doc": params_doc,
            "endpoint": endpoint,
            "verb": verb,
        }


def render_markdown(name: str, controller_src: pathlib.Path, methods: list, old_repo: pathlib.Path):
    out = [
        "<!-- REVERSE-DOCUMENTED by scripts/reverse_document_controller.py. No "
        "PublicApiSpecs file exists for this controller - this was derived from the "
        "old repo's working C# code, not from a Linnworks-published spec. Lower "
        "confidence than sync_api_spec.py output: no rate limits, no official "
        "descriptions. If Linnworks publishes a spec for this controller, delete "
        "this file and run sync_api_spec.py instead. -->\n"
    ]
    out.append(f"# {name} (v1, reverse-documented)\n")
    out.append(f"Source: `{controller_src.relative_to(old_repo).as_posix()}`  \n_Last synced: {datetime.date.today().isoformat()}_\n")

    out.append("## Endpoints\n")
    out.append("| Method | Path | C# signature |")
    out.append("|---|---|---|")
    for m in methods:
        sig = f"{m['ret']} {m['name']}({m['params']})"
        path = f"/api/{m['endpoint']}" if m["endpoint"] else "(no GetResponse call found)"
        out.append(f"| {m['verb']} | `{path}` | `{sig}` |")
    out.append("")

    for m in methods:
        out.append(f"### {m['verb']} `/api/{m['endpoint']}`" if m["endpoint"] else f"### {m['name']}")
        out.append("")
        if m["doc"]:
            out.append(m["doc"] + "\n")
        if m["params_doc"]:
            for pname, pdoc in m["params_doc"]:
                out.append(f"- `{pname}`: {pdoc.strip()}")
            out.append("")
        out.append(f"`{m['ret']} {m['name']}({m['params']})`\n")

    return "\n".join(out)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("controller")
    parser.add_argument("--old-repo", default=str(DEFAULT_OLD_REPO))
    args = parser.parse_args()

    old_repo = pathlib.Path(args.old_repo)
    controller_src = old_repo / "Controllers" / f"{args.controller}.cs"
    if not controller_src.exists():
        print(f"No controller file at {controller_src}", file=sys.stderr)
        sys.exit(1)

    methods = list(extract_methods(read(controller_src)))
    md = render_markdown(args.controller, controller_src, methods, old_repo)

    out_path = PLATFORM_ROOT / "references" / "api" / "v1" / f"{args.controller}.md"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(md, encoding="utf-8")
    print(f"{args.controller}: {len(methods)} method(s) -> {out_path.relative_to(PLATFORM_ROOT)}")


if __name__ == "__main__":
    main()
