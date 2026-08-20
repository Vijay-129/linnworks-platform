"""
Shared --http hosting setup for both MCP servers - the argparse flags and
DNS-rebinding-protection configuration were byte-for-byte identical between the two
servers already (a fix to one had to be manually copied to the other - see
mcp-server/server.py's history). Behavior is identical between the two servers; only
the default port differs (8788 internal, 8787 public-safe), passed in by the caller.
"""

import argparse


def add_http_args(parser: argparse.ArgumentParser, default_port: int) -> None:
    parser.add_argument("--http", action="store_true", help="Run as a network service (streamable-http) instead of stdio")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=default_port)
    parser.add_argument(
        "--allowed-host",
        action="append",
        default=[],
        help="Lock DNS-rebinding protection to this exact Host header value "
        "(repeatable). Only useful with a stable hostname (a named tunnel/domain). "
        "Omit this for a Cloudflare quick tunnel, whose hostname is random every "
        "restart - DNS-rebinding protection is disabled by default in that case "
        "(see configure_transport_security in mcp-shared/transport.py).",
    )


def configure_transport_security(mcp, allowed_hosts: list) -> None:
    """allowed_hosts: args.allowed_host from an argparse.Namespace built with
    add_http_args(). Explicit allow-list keeps DNS-rebinding protection on, scoped to
    the given host(s) - the secure path once you have a stable domain. Empty list
    (no stable host known, e.g. a Cloudflare quick tunnel) disables this specific
    protection - an acceptable tradeoff for a read-only server with no secrets, but
    NOT a substitute for the auth work tracked as TODO in both servers' READMEs,
    which is the actual access control for hosted mode."""
    from mcp.server.transport_security import TransportSecuritySettings

    if allowed_hosts:
        mcp.settings.transport_security = TransportSecuritySettings(
            enable_dns_rebinding_protection=True,
            allowed_hosts=allowed_hosts,
            allowed_origins=[f"https://{h}" for h in allowed_hosts],
        )
    else:
        mcp.settings.transport_security = TransportSecuritySettings(
            enable_dns_rebinding_protection=False
        )
