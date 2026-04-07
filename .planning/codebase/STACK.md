# Technology Stack

**Analysis Date:** 2026-04-07

## Languages

**Primary:**
- C# 13 (.NET 9) — Backend API and scraping logic (`RiksrevisjonApi/Program.cs`)
- JavaScript (ES Modules) — Frontend application (`riksrevisjon-dashboard/src/`)

**Secondary:**
- HTML — SPA shell (`riksrevisjon-dashboard/index.html`), Vue templates
- CSS — Global styles (`riksrevisjon-dashboard/src/style.css`), scoped in `App.vue`

## Runtime

**Backend:**
- .NET 9 (ASP.NET Core Minimal API)
- Target framework: `net9.0` — `RiksrevisjonApi/RiksrevisjonApi.csproj`
- Nullable reference types: enabled
- Implicit usings: enabled

**Frontend:**
- Node.js (version not pinned — no `.nvmrc` or `.node-version` file)
- Browser ES Module support required

**Package Managers:**
- NuGet (implicit via `dotnet` CLI, no explicit `nuget.config`)
- npm — lockfile present: `riksrevisjon-dashboard/package-lock.json`

## Frameworks

**Core:**
- ASP.NET Core 9 Minimal API — Backend HTTP server, `RiksrevisjonApi/RiksrevisjonApi.csproj` (SDK: `Microsoft.NET.Sdk.Web`)
- Vue 3 `^3.5.32` — Frontend SPA, `riksrevisjon-dashboard/package.json`

**Build/Dev:**
- Vite `^8.0.4` — Frontend dev server and bundler, `riksrevisjon-dashboard/vite.config.js`
- `@vitejs/plugin-vue` `^6.0.5` — Vue SFC support for Vite

**Testing:**
- None detected — no test frameworks in either project

## Key Dependencies

**Backend (NuGet):**
- Zero external NuGet packages — uses only built-in ASP.NET Core libraries
- `System.Net.Http` (built-in) — `IHttpClientFactory` for HTTP scraping
- `System.Text.Json` (built-in) — JSON serialization for cache and API responses
- `System.Text.RegularExpressions` (built-in) — HTML parsing via regex

**Frontend (npm):**
- `vue` `^3.5.32` — Only runtime dependency
- `@vitejs/plugin-vue` `^6.0.5` — Dev dependency for SFC compilation
- `vite` `^8.0.4` — Dev dependency for build tooling
- No additional UI libraries, HTTP clients, or state management

## Configuration

**Backend Configuration Files:**
- `RiksrevisjonApi/appsettings.json` — Standard ASP.NET Core logging config
- `RiksrevisjonApi/appsettings.Development.json` — Development logging overrides
- `RiksrevisjonApi/Properties/launchSettings.json` — Dev server on `http://localhost:5000`

**Backend Hardcoded Config (in `Program.cs`):**
- Scrape target: `https://www.riksrevisjonen.no`
- User-Agent: `Mozilla/5.0 (compatible; RRDashboard/1.0)`
- HTTP timeout: 30 seconds
- Cache file: `{AppContext.BaseDirectory}/reports-cache.json`
- Cache max age: 12 hours
- Concurrent enrichment limit: 8 (SemaphoreSlim)
- List page URL pattern: `/rapporter/?p={page}`

**Frontend Configuration Files:**
- `riksrevisjon-dashboard/vite.config.js` — Vite config with Vue plugin and API proxy
- `riksrevisjon-dashboard/.vscode/extensions.json` — Recommended VS Code extensions

**Environment Variables:**
- `ASPNETCORE_ENVIRONMENT` — Set to `Development` in launch profile
- No `.env` files detected
- No custom environment variables used

**Vite Dev Proxy:**
```javascript
// riksrevisjon-dashboard/vite.config.js
server: {
  proxy: {
    '/api': 'http://localhost:5000'
  }
}
```

## Build & Run Commands

**Backend:**
```bash
cd RiksrevisjonApi
dotnet run                    # Starts API on http://localhost:5000
dotnet build                  # Build only
dotnet publish -c Release     # Production build
```

**Frontend:**
```bash
cd riksrevisjon-dashboard
npm install                   # Install dependencies
npm run dev                   # Vite dev server (proxies /api to :5000)
npm run build                 # Production build to dist/
npm run preview               # Preview production build
```

**Full Development Workflow:**
1. Start backend: `cd RiksrevisjonApi && dotnet run`
2. Start frontend: `cd riksrevisjon-dashboard && npm run dev`
3. Open browser to Vite dev server URL (typically `http://localhost:5173`)

## Platform Requirements

**Development:**
- .NET 9 SDK
- Node.js (compatible with npm and Vite 8)
- No Docker configuration present
- No database required

**Production:**
- .NET 9 runtime for backend
- Static file hosting for frontend build output (`riksrevisjon-dashboard/dist/`)
- Outbound HTTPS access to `www.riksrevisjonen.no` from backend
- Filesystem write access for `reports-cache.json` in backend's base directory
- No CI/CD pipeline configured
- No deployment configuration present

## External Fonts (CDN)

Loaded via Google Fonts CDN in `riksrevisjon-dashboard/src/App.vue` (line 221):
- Playfair Display (headings)
- Space Mono (monospace labels)
- Source Serif 4 (body text)

## HTTP Test File

`RiksrevisjonApi/RiksrevisjonApi.http` — VS Code REST Client / JetBrains HTTP Client file. Contains a stale `GET /weatherforecast/` endpoint reference (from project template, not updated for actual API).

---

*Stack analysis: 2026-04-07*
