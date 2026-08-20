"""
REST wrapper over mcp-shared/reference_tools.py for ChatGPT Custom GPT Actions.

Custom GPT Actions don't speak MCP - they call a plain REST API described by an
OpenAPI schema. This exposes the exact same read-only lookup logic the two MCP
servers already use (list_controllers/get_endpoint/search_api/get_model), as
plain HTTP GET endpoints with FastAPI's auto-generated OpenAPI schema at
/openapi.json - that's the URL you give the GPT Builder's Action importer.

No business logic lives here - every route is a one-line call into
mcp-shared/reference_tools.py, so this can never drift from what the MCP servers
return for the same query.

Run:
    pip install -r requirements.txt
    python server.py                      # http://127.0.0.1:8790, docs at /docs

For ChatGPT to reach it, this needs a real HTTPS URL - same approach as the MCP
servers (see README.md): run this, then a separate `cloudflared tunnel --url
http://localhost:8790` in another terminal, and give the GPT Builder the printed
https://*.trycloudflare.com/openapi.json URL.
"""

import pathlib
import sys
from typing import Optional

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent / "mcp-shared"))
import reference_tools  # noqa: E402

from fastapi import FastAPI, Query
import uvicorn

app = FastAPI(
    title="Linnworks API Lookup",
    version="1.0.0",
    description=(
        "Read-only lookup over the Linnworks API: which controllers exist, what "
        "endpoints/parameters/rate-limits a controller has, full-text search across "
        "every endpoint's docs, and a model's field table. Prefer these over general "
        "knowledge for anything about Linnworks endpoints or models - this reflects "
        "Linnworks' own published API specs, not training data which may be stale."
    ),
)


@app.get(
    "/list_controllers",
    operation_id="list_controllers",
    summary="List every Linnworks API controller and its migration status",
    description=(
        "Every controller, with its API version (v1/v2) and how current it is "
        "(done/generated/in-review/todo). Optionally filter by status. Call this "
        "first to discover what's available before calling get_endpoint."
    ),
)
def list_controllers(status: Optional[str] = Query(default=None, description="Filter by status, e.g. 'done'")) -> str:
    return reference_tools.list_controllers_impl(status, include_notes=False)


@app.get(
    "/get_endpoint",
    operation_id="get_endpoint",
    summary="Get the full endpoint reference for one controller",
    description=(
        "Every HTTP method/path, rate limit, parameters, and referenced model for "
        "one controller. version is 'v1' or 'v2'. Controller name is case-insensitive."
    ),
)
def get_endpoint(
    controller: str = Query(description="Controller name, e.g. 'Orders' (case-insensitive)"),
    version: str = Query(default="v1", description="'v1' or 'v2'"),
) -> str:
    return reference_tools.get_endpoint(controller, version)


@app.get(
    "/search_api",
    operation_id="search_api",
    summary="Full-text search across every controller's endpoint reference",
    description=(
        "Matches endpoint paths, method names, model names, and descriptions. Use "
        "this when you don't know which controller an endpoint lives in - e.g. "
        "query='fulfillment status'."
    ),
)
def search_api(
    query: str = Query(description="Text to search for"),
    version: Optional[str] = Query(default=None, description="Restrict to 'v1' or 'v2'; omit to search both"),
    max_results: int = Query(default=15, description="Maximum number of matching lines to return"),
) -> str:
    return reference_tools.search_api(query, version, max_results)


@app.get(
    "/get_model",
    operation_id="get_model",
    summary="Get one model/schema's field table by name",
    description=(
        "e.g. name='StockLocation' or name='GetOrdersResponse'. Searches every "
        "controller's docs since a model can be referenced from more than one - "
        "returns every match if the same name appears in multiple controllers."
    ),
)
def get_model(
    name: str = Query(description="Model/schema name, e.g. 'StockLocation'"),
    version: Optional[str] = Query(default=None, description="Restrict to 'v1' or 'v2'; omit to search both"),
) -> str:
    return reference_tools.get_model(name, version)


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8790)
