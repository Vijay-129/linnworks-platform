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
from pydantic import BaseModel, Field
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
    macro_name: str = Field(..., description='PascalCase identifier used as both namespace and class name, e.g. "OrderSyncMacro". Name it for what the macro does - never a placeholder like "LinnworksMacro".')
    trigger: str = Field(..., description='"rule" or "scheduled" - never both. Ask the user if the trigger type is unclear from their requirement.')
    config_params: str = Field(default="", description='"name:description" pairs separated by "|" - ONE per configurable value, e.g. "locationName:Name of the stock location to scan|maxOrders:Maximum orders to process per run". Never pass a single JSON/CSV blob here - each value must be its own named, described parameter, since Linnworks\' macro settings UI edits one text field per Execute parameter. A pair with no ":description" is rejected.')


@app.post("/scaffold_macro", operation_id="scaffold_macro",
          summary="Generate a starting skeleton with the mandatory macro structure filled in",
          description="Generate a starting macro skeleton: logging, try/catch, rate-limit wrapper, an XML <param> doc per parameter. trigger is 'rule' or 'scheduled' - never both. config_params is 'name:description' pairs separated by '|', never a blob. Business logic left as TODOs, not a finished macro.")
def scaffold_macro(body: ScaffoldRequest) -> str:
    return macro_server.scaffold_macro(body.macro_name, body.trigger, body.config_params)


class CodeRequest(BaseModel):
    code: str = Field(..., description="A complete C# macro file - full using directives, namespace, and a class deriving LinnworksMacroBase with its Execute method. Not a bare snippet.")


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


# ---------------------------------------------------------------------------
# Knowledge layer — concept, workflow, find_relevant_operations, verify_api_usage
# ---------------------------------------------------------------------------

@app.get("/list_linnworks_concepts", operation_id="list_linnworks_concepts",
         summary="List available Linnworks concept docs",
         description=(
             "List available Linnworks concept docs (references/macro/concepts/). "
             "Each concept covers a domain area: what it is, core identifiers, important "
             "model names, common operations, lifecycle, and gotchas with provenance. "
             "Call find_relevant_operations first — this is a drill-down discovery tool."
         ))
def list_linnworks_concepts() -> str:
    return macro_server.list_linnworks_concepts()


@app.get("/get_linnworks_concept", operation_id="get_linnworks_concept",
         summary="Read one Linnworks concept doc in full by slug or title",
         description=(
             "Read one Linnworks concept doc in full by slug or title. "
             "Covers: what the entity is, core identifiers, important model names "
             "(use get_model for full field lists), common operations "
             "(use get_endpoint for HTTP signatures), lifecycle, and gotchas with "
             "inline source provenance. Use list_linnworks_concepts to discover slugs."
         ))
def get_linnworks_concept(
    name: str = Query(..., description="Concept slug or title, e.g. 'open_orders' or 'Open Orders'"),
) -> str:
    return macro_server.get_linnworks_concept(name)


@app.get("/list_linnworks_workflows", operation_id="list_linnworks_workflows",
         summary="List available Linnworks workflow docs",
         description=(
             "List available Linnworks workflow docs (references/macro/workflows/). "
             "Each workflow covers a common macro task: intent, preconditions, step "
             "sequence, decision points, relevant operations, and gotchas. "
             "Call find_relevant_operations first — this is a drill-down discovery tool."
         ))
def list_linnworks_workflows() -> str:
    return macro_server.list_linnworks_workflows()


@app.get("/get_linnworks_workflow", operation_id="get_linnworks_workflow",
         summary="Read one Linnworks workflow doc in full by slug",
         description=(
             "Read one Linnworks workflow doc in full by slug. "
             "Covers: intent, preconditions, step-by-step sequence, decision points, "
             "relevant operations (Controller.Method + reason — use get_endpoint for "
             "signatures), gotchas with inline provenance, and counter-cases. "
             "Use list_linnworks_workflows to discover slugs."
         ))
def get_linnworks_workflow(
    name: str = Query(..., description="Workflow slug, e.g. 'modify_open_orders_by_sku'"),
) -> str:
    return macro_server.get_linnworks_workflow(name)


class FindRelevantOperationsRequest(BaseModel):
    goal: str = Field(
        ...,
        description=(
            "Natural-language description of the macro task, e.g. "
            "'modify open orders containing SKU ABC and move them to the Review folder'."
        ),
    )


@app.post("/find_relevant_operations", operation_id="find_relevant_operations",
          summary="Primary knowledge entry point — resolve a macro goal to workflows, concepts, and operations",
          description=(
              "Primary knowledge entry point. Given a natural-language goal, returns structured "
              "JSON with: best-matching workflow, relevant concepts, candidate API operations "
              "(controller + method + reason — use get_endpoint for signatures), known gotchas "
              "with source provenance, match confidence evidence, and any ambiguities that must "
              "be resolved before generating code. Always call this first for any new macro task. "
              "Does NOT return method signatures — use get_endpoint and get_model for those."
          ))
def find_relevant_operations(body: FindRelevantOperationsRequest) -> str:
    return macro_server.find_relevant_operations(body.goal)


class VerifyApiUsageRequest(BaseModel):
    controller: str = Field(..., description="Controller name, e.g. 'OpenOrders', 'Orders', 'Inventory'")
    method: str = Field(..., description="Method name, e.g. 'GetOpenOrders', 'SetExtendedProperties'")
    model: Optional[str] = Field(
        None,
        description="Optional request model name to verify, e.g. 'GetOpenOrdersRequest'",
    )
    fields: Optional[list[str]] = Field(
        None,
        description="Optional list of field names to verify against the model, e.g. ['ViewId', 'LocationId']",
    )


@app.post("/verify_api_usage", operation_id="verify_api_usage",
          summary="Pre-generation API verification — confirm controller/method/model/fields exist",
          description=(
              "Pre-generation conceptual check. Confirms a controller/method pair exists in the "
              "endpoint reference; optionally verifies a model name and specific field names. "
              "Returns structured JSON with: valid flag, matched request/response models, HTTP "
              "method type, rate limit, field-level problems with suggestions, and source "
              "provenance. Use this BEFORE writing code to catch wrong field names and wrong "
              "controllers. Distinct from check_macro_compiles (real dotnet build, post-generation). "
              "Both are needed."
          ))
def verify_api_usage(body: VerifyApiUsageRequest) -> str:
    return macro_server.verify_api_usage(
        body.controller,
        body.method,
        body.model,
        body.fields,
    )


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8791)
