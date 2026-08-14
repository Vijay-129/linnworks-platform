<!-- Hand-written, not generated. Keep this thin - it's calling convention, not an
endpoint reference. Endpoint details live in references/api/v1/<Controller>.md and
references/api/v2/<Controller>.md; look those up via search_api/get_endpoint rather
than duplicating them here. -->

# Calling LinnworksAPI from a macro

Two genuinely different situations, easy to conflate. Know which one you're in
before writing any code.

## Situation A: running inside a Linnworks macro (`LinnworksMacroBase`)

If your code is a macro that Linnworks itself executes (a class deriving from
`LinnworksMacroBase`), **you do not call `Auth.AuthorizeByApplication` yourself.**
The Linnworks macro engine authenticates the macro and injects a ready-to-use `Api`
object before your macro code runs. Confirmed directly from
`LinnworksMacroHelpers/LinnworksMacroBase.cs`:

```csharp
public class LinnworksMacroBase
{
    public IRuntimeHelper RunTime { get; set; }
    public LinnworksAPI.ApiObjectManager Api { get; set; }
    public IProxyFactory ProxyFactory { get; set; }
    public ILogger Logger { get; set; }
    public MacroConfigurationProxy Configuration { get; set; }
    public ISettingsHelper SettingsHelper { get; set; }
}
```

`Api` is a real `ApiObjectManager` - identical type to the one built manually in
Situation B below, just pre-authenticated. `Logger`, `RunTime`, `Configuration`, and
`SettingsHelper` are also injected; see `references/standards/macro_conventions.md`
for how real macros use `Logger` (mandatory start/end logging, human-readable IDs
only) and see `references/standards/golden_examples/` for real macros using this
exact base class.

Inside a macro, calling the SDK looks like:

```csharp
public class MyMacro : LinnworksMacroBase
{
    public void Execute()
    {
        var locations = Api.Locations.GetWarehouseTOTEs(new GetWarehouseTotesRequest
        {
            LocationId = someLocationId,
        });
        // ...
    }
}
```

No `ApiContext` construction, no session management - that's the macro engine's job.
`Api` here has the same shape as `ApiObjectManager` in Situation B (one property per
controller: `Api.Orders`, `Api.Stock`, `Api.Locations`, etc.) - once you're past
getting the `Api` object, everything below about controllers/models/errors applies
identically.

## Situation B: standalone code (console app, service, ASP.NET, a script that talks
## to Linnworks from outside Linnworks' own macro engine)

This is what `LinnworksAPI/` itself was live-tested against (see
`migration/STATUS.md`'s "Core fix" and "Full read-only sweep" sections) - the pattern
below is confirmed working against a real account, not just spec-derived.

### 1. Authenticate

```csharp
using LinnworksAPI;

var bootstrapContext = new ApiContext("https://api.linnworks.net");
var auth = new AuthController(bootstrapContext);

var session = auth.AuthorizeByApplication(new AuthorizeByApplicationRequest
{
    ApplicationId = /* your app's ApplicationId */,
    ApplicationSecret = /* your app's ApplicationSecret */,
    Token = /* an installation/user token */,
});
```

`session.Server` and `session.Locality` (`EU`/`US`/`AS`) tell you which regional
Linnworks server this account lives on - live-confirmed to vary (an EU account
returned `https://eu-ext.linnworks.net`). Don't hardcode a server URL; always take it
from the session.

### 2. Build an authenticated context and reach any controller

```csharp
var ctx = new ApiContext(session.Token, session.Server);
var mgr = new ApiObjectManager(ctx);

// locationId here MUST be a real location's StockLocationId, fetched from
// Inventory.GetStockLocations() - see the warning below.
var openOrders = mgr.OpenOrders.GetOpenOrders(new GetOpenOrdersRequest
{
    ViewId = 1,           // a real view id from the account - 0 is not a valid default,
                            // it will fail server-side (confirmed live)
    LocationId = locationId,
    EntriesPerPage = 10,
    PageNumber = 1,
});
```

> **`Guid.Empty` is not "all locations" - it's the real ID of whichever location is
> named "Default" in this account.** Live-tested 2026-08-14 on a 30-location
> account: `GetOpenOrders(LocationId = Guid.Empty)` returned 1,871 orders, exactly
> matching a call scoped to the "Default" location explicitly - the true sum
> across all 30 locations was 23,520. Code that does `locationId ?? Guid.Empty`
> to mean "unfiltered" is silently dropping the other 92% of records. If you want
> every location, call `Inventory.GetStockLocations()` once and loop, issuing one
> scoped call per location - don't rely on an empty/default GUID meaning "no
> filter." See `references/standards/macro_conventions.md` section 0.1 for the
> full evidence and the real macro (`golden_examples/03_PickListMonitoring.cs`)
> that had this exact bug.

`ApiObjectManager` exposes one property per ported controller (`.Orders`, `.Stock`,
`.Inventory`, `.PostalServices`, ...). Look up which controller/method you need with
`search_api`/`get_endpoint` (MCP tools) or `references/api/v1/<Controller>.md`
directly - don't guess a method name.

### Session lifetime

`session.TTL` is the session's lifetime in seconds (live-observed: `1800`, i.e. 30
minutes). Nothing in `LinnworksAPI/` refreshes a session automatically - if a
long-running process needs to keep working past the TTL, it has to re-authenticate
itself. There's no built-in retry/refresh to rely on.

## Error handling (applies to both situations)

Every v1 call that fails throws a plain `Exception`. As of the 2026-08-13 fix (see
`LinnworksAPI/Core/Factory.cs`), the message is the **real Linnworks error text**,
e.g. `"Linnworks API error 400 calling OpenOrders/GetOpenOrders: Object reference not
set to an instance of an object."` - read the message, it usually says exactly what's
wrong (a bad ID, a missing required field, a permission gap). There is no automatic
retry or backoff anywhere in the SDK; add it yourself at the call site if you need it.

## Combining with macro helper integrations

FTP/SFTP/Email/Dropbox/raw-HTTP inside a macro go through `LinnworksMacroHelpers`/
`LinnMacroCustomer`, not through `LinnworksAPI` - those are a separate concern from
calling the Linnworks API itself. See `get_macro_integration` (MCP tool) or
`references/macro/integrations/*.md`.

## Coding standard

Code generated against either situation should follow `references/standards/conventions.md`
(`get_standards`/`check_against_standards` via MCP) - naming, nullable-type rules,
and the v1/v2 namespace separation apply the same way whether you're inside a macro
or standalone.
