"""
REST wrapper over mcp-server's full macro toolset, for a ChatGPT Custom GPT whose
job is writing Linnworks macros - not just looking up API docs (that's
../chatgpt-action/, the narrower one). Exposes the same tools mcp-server gives an
MCP client: API lookup, macro conventions, golden examples, scaffolding, and real
compile verification (`dotnet build` against the confirmed macro engine target).

No new logic lives here. The 4 API-lookup routes call mcp-shared/reference_tools.py
directly (same as ../chatgpt-action/). The 13 macro routes import mcp-server/server.py
as a plain module and call its already-decorated functions - @mcp.tool() registers a
function against FastMCP's registry as a side effect but returns the function itself
unchanged, so it's still directly callable; confirmed working 2026-08-20 before this
file was written. This can never answer differently than the MCP server does for the
same query.

This is a bigger exposure than ../chatgpt-action/: golden-example macro source and
internal conventions become reachable through this URL, same as mcp-server itself
already is (hosted publicly since 2026-08-14) - not a new decision, the same one
already made for the MCP server, applied to this REST surface too.

Run:
    pip install -r requirements.txt
    python server.py                      # http://127.0.0.1:8791, docs at /docs

For ChatGPT to reach it: same tunnel approach as ../chatgpt-action/README.md -
run this, then `cloudflared tunnel --url http://localhost:8791` in another
terminal, give the GPT Builder the printed https://*.trycloudflare.com/openapi.json
URL. See README.md for the exact GPT instructions text to paste in.
"""

import os
import pathlib
import sys
from typing import Optional

_ROOT = pathlib.Path(__file__).resolve().parent.parent
sys.path.insert(0, str(_ROOT / "mcp-shared"))
sys.path.insert(0, str(_ROOT / "mcp-server"))
import reference_tools  # noqa: E402
import server as macro_server  # noqa: E402  (mcp-server/server.py, imported as a plain module)

from fastapi import FastAPI, Query
from pydantic import BaseModel
import uvicorn

# ChatGPT's Action importer requires an OpenAPI `servers` entry - the generated
# schema only has relative paths ("/list_controllers"), so without this it
# rejects the schema with "Could not find a valid URL in `servers`" (confirmed
# 2026-08-20). FastAPI doesn't add one on its own. Set PUBLIC_BASE_URL to
# whatever HTTPS URL currently reaches this process (the ngrok/cloudflared
# tunnel URL) before starting - it's ephemeral on the free tiers, so this has
# to be set per run, not hardcoded:
#   PUBLIC_BASE_URL=https://your-tunnel-url.example python server.py
_PUBLIC_BASE_URL = os.environ.get("PUBLIC_BASE_URL", "").rstrip("/")
_SERVERS = [{"url": _PUBLIC_BASE_URL}] if _PUBLIC_BASE_URL else None

app = FastAPI(
    title="Linnworks Macro Assistant",
    version="1.0.0",
    description=(
        "Everything needed to write a correct, verified Linnworks macro: API "
        "endpoint lookup, macro-authoring conventions derived from real approved "
        "macros, golden example macros to search/read, a scaffolding tool that "
        "generates the mandatory structure, and real dotnet-compile verification "
        "against the confirmed macro engine target. Always search_golden_examples "
        "and get_macro_conventions before writing macro code from scratch; always "
        "check_against_standards then check_macro_compiles before presenting "
        "generated code as final."
    ),
    servers=_SERVERS,
)

if not _PUBLIC_BASE_URL:
    print(
        "WARNING: PUBLIC_BASE_URL is not set - the /openapi.json this serves will "
        "have no `servers` entry, and ChatGPT's Action importer will reject it "
        "with \"Could not find a valid URL in `servers`\". Set it to your current "
        "tunnel URL before starting if you're about to import this into a GPT.",
        file=sys.stderr,
    )


# ---- API lookup (same 4 as chatgpt-action/, reused for endpoint/filter selection) ----

@app.get("/list_controllers", operation_id="list_controllers",
         summary="List every Linnworks API controller and its migration status")
def list_controllers(status: Optional[str] = Query(default=None)) -> str:
    return reference_tools.list_controllers_impl(status, include_notes=False)


@app.get("/get_endpoint", operation_id="get_endpoint",
         summary="Get the full endpoint reference for one controller")
def get_endpoint(controller: str = Query(...), version: str = Query(default="v1")) -> str:
    return reference_tools.get_endpoint(controller, version)


@app.get("/search_api", operation_id="search_api",
         summary="Full-text search across every controller's endpoint reference")
def search_api(query: str = Query(...), version: Optional[str] = Query(default=None), max_results: int = Query(default=15)) -> str:
    return reference_tools.search_api(query, version, max_results)


@app.get("/get_model", operation_id="get_model",
         summary="Get one model/schema's field table by name")
def get_model(name: str = Query(...), version: Optional[str] = Query(default=None)) -> str:
    return reference_tools.get_model(name, version)


# ---- Macro guidance ----
# Descriptions below are fixed, short strings (verified <=300 chars each), not
# macro_server.X.__doc__ - ChatGPT's Action importer enforces a hard 300-char
# limit per operation description and rejects the whole schema if any operation
# exceeds it (hit in the GPT Builder UI 2026-08-20; several real docstrings here
# run past 2000 chars). Using literal strings instead of the docstrings also
# means this can't silently break again if a docstring in mcp-server/server.py
# grows past 300 chars later - the full docstring is still one call away via the
# tool itself, this is just what the GPT sees before deciding to call it.

@app.get("/get_macro_conventions", operation_id="get_macro_conventions",
         summary="Get macro-authoring conventions (structure, logging, rate limits, idempotency)",
         description="Macro-authoring conventions derived from real approved macros: mandatory structure, start/end logging, human-readable IDs only (never raw GUIDs), the required rate-limit-safe API pattern, server-side filter selection, and idempotency rules. Read before writing or reviewing any macro.")
def get_macro_conventions() -> str:
    return macro_server.get_macro_conventions()


@app.get("/get_macro_calling_guide", operation_id="get_macro_calling_guide",
         summary="How a macro reaches LinnworksAPI (inside Linnworks' engine vs standalone)",
         description="How a macro reaches LinnworksAPI: running inside Linnworks' macro engine (pre-authenticated Api property) vs standalone code (manual auth), session lifetime, and how errors surface. Read before writing macro code that calls the SDK.")
def get_macro_calling_guide() -> str:
    return macro_server.get_macro_calling_guide()


@app.get("/get_standards", operation_id="get_standards",
         summary="Get SDK-layer coding conventions",
         description="Coding conventions macros/plugins generated against this platform should follow: naming, error handling, logging.")
def get_standards() -> str:
    return macro_server.get_standards()


@app.get("/get_macro_integration", operation_id="get_macro_integration",
         summary="Get FTP/SFTP/Email/Dropbox/raw-HTTP integration reference for macros",
         description="Macro integration reference for FTP/SFTP/Email/Dropbox/raw-HTTP: the IProxyHelper contract, request/response types, and real working call-site code. category is one of: ftp, ftps, sftp, email, dropbox, web.")
def get_macro_integration(category: str = Query(..., description="ftp, ftps, sftp, email, dropbox, or web")) -> str:
    return macro_server.get_macro_integration(category)


@app.get("/list_macro_patterns", operation_id="list_macro_patterns",
         summary="List hand-written macro pattern docs",
         description="List hand-written macro pattern docs - things a developer figured out that aren't derivable from code or the API spec alone (gotchas, non-obvious sequencing, workarounds). Call get_macro_pattern to read one.")
def list_macro_patterns() -> str:
    return macro_server.list_macro_patterns()


@app.get("/get_macro_pattern", operation_id="get_macro_pattern",
         summary="Read one hand-written macro pattern doc by name",
         description="Read one hand-written macro pattern doc by name (see list_macro_patterns for available names).")
def get_macro_pattern(name: str = Query(...)) -> str:
    return macro_server.get_macro_pattern(name)


# ---- Golden examples ----

@app.get("/list_golden_examples", operation_id="list_golden_examples",
         summary="List real approved macros kept as reference examples",
         description="List real, approved macros kept as reference examples, with a one-line summary of what each demonstrates. Call get_golden_example to read one, or get_golden_example_notes for what to copy or fix in each.")
def list_golden_examples() -> str:
    return macro_server.list_golden_examples()


@app.get("/get_golden_example", operation_id="get_golden_example",
         summary="Read one golden example macro's full source by name",
         description="Read one golden example macro's full source by name (see list_golden_examples). Not all are fully compliant with get_macro_conventions - call get_golden_example_notes for what to copy vs fix before using one as a template.")
def get_golden_example(name: str = Query(...)) -> str:
    return macro_server.get_golden_example(name)


@app.get("/get_golden_example_notes", operation_id="get_golden_example_notes",
         summary="What to copy vs fix in each golden example macro",
         description="Annotated breakdown of every golden example macro: what each demonstrates well (copy this) and where it violates get_macro_conventions (fix this if reusing). Read before using any golden example as a template - none are perfect.")
def get_golden_example_notes() -> str:
    return macro_server.get_golden_example_notes()


@app.get("/search_golden_examples", operation_id="search_golden_examples",
         summary="Find the closest existing golden example macro to a new requirement",
         description="Find the closest existing golden example macro to a new requirement without knowing its filename. Searches each example's full write-up, not just the name. Call get_golden_example(name) to read the match's full source.")
def search_golden_examples(query: str = Query(...), max_results: int = Query(default=5)) -> str:
    return macro_server.search_golden_examples(query, max_results)


# ---- Scaffolding and verification ----

class ScaffoldRequest(BaseModel):
    macro_name: str
    trigger: str
    config_params: str = ""


@app.post("/scaffold_macro", operation_id="scaffold_macro",
          summary="Generate a starting skeleton with the mandatory macro structure filled in",
          description="Generate a starting macro skeleton with the mandatory structure filled in: start/end logging, try/catch, and the rate-limit-safe ExecuteApi wrapper. trigger is 'rule' or 'scheduled' - never both. Business logic is left as TODOs, not a finished macro.")
def scaffold_macro(body: ScaffoldRequest) -> str:
    return macro_server.scaffold_macro(body.macro_name, body.trigger, body.config_params)


class CodeRequest(BaseModel):
    code: str


@app.post("/check_against_standards", operation_id="check_against_standards",
          summary="Lint a C# macro snippet against mechanically-checkable convention rules",
          description="Lint a C# macro snippet against mechanically-checkable convention rules: nullable reference types, missing StringEnumConverter, interface naming, SDK-layer logging, empty catches, async usage. A regex linter, not a compiler - can miss things.")
def check_against_standards(body: CodeRequest) -> str:
    return macro_server.check_against_standards(body.code)


@app.post("/check_macro_compiles", operation_id="check_macro_compiles",
          summary="Really compile a macro (dotnet build) against LinnworksAPI + LinnworksMacroHelpers",
          description="Really compile a macro (dotnet build) against LinnworksAPI + LinnworksMacroHelpers, net10.0/C# latest/nullable enabled - the confirmed macro engine target. Catches real errors a linter can't. code must be a complete, compilable macro file.")
def check_macro_compiles(body: CodeRequest) -> str:
    return macro_server.check_macro_compiles(body.code)


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8791)
