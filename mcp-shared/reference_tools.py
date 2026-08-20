"""
Shared, generic reference-lookup logic used by both mcp-server/server.py (full
internal server) and mcp-server-api/server.py (public-safe subset). Deliberately
contains nothing macro-conventions/golden-example/standards-related - only the four
tools both servers exposed identically (or near-identically): list_controllers,
get_endpoint, search_api, get_model.

Extracted 2026-08-20 after the two files drifted once already (a TransportSecuritySettings
fix landed in mcp-server-api/server.py first, mcp-server/server.py's own comment had to
call that out explicitly - "first fixed there"). Both servers have in fact been hosted
publicly (via Cloudflare Tunnel, 2026-08-14) - this sharing is judged safe regardless,
because this module contains nothing macro/golden-example/standards-related no matter
which server(s) end up exposed; the boundary is enforced by what each server.py chooses
to register, not by which servers happen to be running at a given time. See
mcp-server-api/README.md for the updated note.

Plain functions, not @mcp.tool()-decorated - each server registers them against its
own FastMCP instance with `mcp.tool()(list_controllers)` etc. (registers under the
function's own __name__), so the same implementation runs under two independent tool
registries without either server importing the other.
"""

import pathlib
import re
from typing import Optional

PLATFORM_ROOT = pathlib.Path(__file__).resolve().parent.parent
API_DIR = PLATFORM_ROOT / "references" / "api"
STATUS_FILE = PLATFORM_ROOT / "migration" / "STATUS.md"


def read_text(path: pathlib.Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def find_by_stem(directory: pathlib.Path, name: str) -> Optional[pathlib.Path]:
    """Look up a file by name within directory, matching an existing file's stem -
    never builds a path directly from caller input. `directory / f"{name}.md"`
    would let a caller path-traverse out of `directory` via `../` in `name`;
    glob()-then-compare can't be escaped that way since `name` only ever gets
    compared against real filenames already inside `directory`, never concatenated
    into a path. (Fixed 2026-08-19 in get_macro_integration/get_macro_pattern,
    mcp-server-only - generalized here since get_endpoint needed the identical
    pattern already.)"""
    if not directory.exists():
        return None
    for f in directory.glob("*.md"):
        if f.stem.lower() == name.lower():
            return f
    return None


def find_controller_file(controller: str, version: str) -> Optional[pathlib.Path]:
    return find_by_stem(API_DIR / version, controller)


def list_controllers_impl(status: Optional[str], include_notes: bool) -> str:
    """Shared implementation behind each server's own list_controllers tool. The two
    servers differ here on purpose - mcp-server includes migration Notes (internal
    commentary, e.g. "draft spec, not confirmed authoritative"), mcp-server-api
    doesn't - so this isn't auto-registered as a tool itself; each server wraps it
    with its own docstring and include_notes value. See each server.py."""
    if not STATUS_FILE.exists():
        return "migration/STATUS.md not found."
    rows = []
    for line in read_text(STATUS_FILE).splitlines():
        if not line.startswith("|") or line.startswith("|---") or line.startswith("| Controller"):
            continue
        cols = [c.strip() for c in line.strip("|").split("|")]
        if len(cols) != 6:
            continue
        controller, _spec_version, api_version, _last_synced, row_status, notes = cols
        if status and row_status.lower() != status.lower():
            continue
        suffix = f" - {notes}" if (include_notes and notes) else ""
        rows.append(f"{controller} ({api_version}) - {row_status}{suffix}")
    return "\n".join(rows) if rows else f"No controllers found with status={status!r}."


def get_endpoint(controller: str, version: str = "v1") -> str:
    """Get the full endpoint reference for one controller: every HTTP method/path,
    rate limits, parameters, and referenced models. version is "v1" or "v2".
    Controller name is case-insensitive (e.g. "orders", "Orders", "ORDERS")."""
    f = find_controller_file(controller, version)
    if not f:
        available = sorted(p.stem for p in (API_DIR / version).glob("*.md")) if (API_DIR / version).exists() else []
        return f'No {version} controller named "{controller}". Available: {", ".join(available)}'
    return read_text(f)


def search_api(query: str, version: Optional[str] = None, max_results: int = 15) -> str:
    """Full-text search across every controller's endpoint reference (both v1 and v2
    unless version is specified). Matches endpoint paths, method names, model names,
    and descriptions. Use this when you don't know which controller an endpoint lives
    in - e.g. search_api("fulfillment status") or search_api("stock take")."""
    versions = [version] if version else ["v1", "v2"]
    pattern = re.compile(re.escape(query), re.IGNORECASE)
    results = []
    for v in versions:
        vdir = API_DIR / v
        if not vdir.exists():
            continue
        for f in sorted(vdir.glob("*.md")):
            for i, line in enumerate(read_text(f).splitlines(), start=1):
                if pattern.search(line):
                    results.append(f"{f.stem} ({v}):{i}: {line.strip()}")
                    if len(results) >= max_results:
                        break
            if len(results) >= max_results:
                break
        if len(results) >= max_results:
            break
    if not results:
        return f'No matches for "{query}".'
    return "\n".join(results)


def get_model(name: str, version: Optional[str] = None) -> str:
    """Get one model/schema's field table by name (e.g. "StockLocation",
    "GetOrdersResponse"). Searches every controller's docs since a model can be
    referenced from more than one. Returns every match if the same name appears in
    multiple controllers' docs."""
    versions = [version] if version else ["v1", "v2"]
    heading_re = re.compile(r"^### `?" + re.escape(name) + r"`?\s*$")
    matches = []
    for v in versions:
        vdir = API_DIR / v
        if not vdir.exists():
            continue
        for f in sorted(vdir.glob("*.md")):
            lines = read_text(f).splitlines()
            for i, line in enumerate(lines):
                if heading_re.match(line.strip()):
                    section = [line]
                    for j in range(i + 1, len(lines)):
                        if lines[j].startswith("## ") or lines[j].startswith("### "):
                            break
                        section.append(lines[j])
                    matches.append(f"--- from {f.stem} ({v}) ---\n" + "\n".join(section).strip())
    if not matches:
        return f'No model named "{name}" found.'
    return "\n\n".join(matches)
