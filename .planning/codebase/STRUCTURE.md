# Codebase Structure

**Analysis Date:** 2026-04-07

## Directory Layout

```
Oppgave 3/
├── RiksrevisjonApi/                # .NET 9 backend API
│   ├── Program.cs                  # ALL backend code: endpoints, service, model
│   ├── RiksrevisjonApi.csproj      # Project file targeting net9.0
│   ├── RiksrevisjonApi.http        # HTTP test file (stale, references /weatherforecast)
│   ├── appsettings.json            # Logging config
│   ├── appsettings.Development.json # Dev logging overrides
│   ├── Properties/
│   │   └── launchSettings.json     # Dev server config (port 5000)
│   ├── bin/                        # Build output (generated)
│   └── obj/                        # Build intermediates (generated)
│
├── riksrevisjon-dashboard/         # Vue 3 frontend SPA
│   ├── index.html                  # HTML shell, loads /src/main.js
│   ├── package.json                # npm dependencies (vue, vite)
│   ├── package-lock.json           # Lockfile
│   ├── vite.config.js              # Vite config with /api proxy to :5000
│   ├── public/                     # Static assets (copied as-is)
│   │   ├── favicon.svg
│   │   └── icons.svg
│   ├── src/
│   │   ├── main.js                 # Vue app creation and mount
│   │   ├── App.vue                 # ENTIRE dashboard UI (614 lines)
│   │   ├── style.css               # Global styles (Vite scaffold, mostly unused)
│   │   ├── assets/                 # Imported static assets
│   │   │   ├── hero.png
│   │   │   ├── vite.svg
│   │   │   └── vue.svg
│   │   └── components/
│   │       └── HelloWorld.vue      # Vite scaffold placeholder (unused)
│   ├── .vscode/
│   │   └── extensions.json         # Recommended VS Code extensions
│   ├── .gitignore
│   ├── README.md
│   └── node_modules/               # Dependencies (generated)
│
└── .planning/
    └── codebase/                   # Architecture documentation (this file)
```

## Directory Purposes

**`RiksrevisjonApi/`:**
- Purpose: Complete .NET 9 Minimal API backend
- Contains: Single `Program.cs` with all logic (endpoints, ReportService, Report record, scraping, caching)
- Key files: `Program.cs` (the only source file), `Properties/launchSettings.json` (port config)

**`riksrevisjon-dashboard/`:**
- Purpose: Vue 3 single-page application frontend
- Contains: Vite-scaffolded project with dashboard implementation in `App.vue`
- Key files: `src/App.vue` (all UI), `vite.config.js` (proxy config), `src/main.js` (bootstrap)

**`riksrevisjon-dashboard/src/components/`:**
- Purpose: Vue component directory (from scaffold)
- Contains: Only `HelloWorld.vue` — a Vite scaffold placeholder, NOT used by the dashboard
- Note: The actual dashboard has no extracted components; everything is in `App.vue`

**`riksrevisjon-dashboard/src/assets/`:**
- Purpose: Importable static assets
- Contains: Vite scaffold images (`hero.png`, `vite.svg`, `vue.svg`) — NOT used by the dashboard

## Key File Locations

**Entry Points:**
- `RiksrevisjonApi/Program.cs`: Backend entry point — all API and scraping code
- `riksrevisjon-dashboard/src/main.js`: Frontend entry point — creates and mounts Vue app
- `riksrevisjon-dashboard/index.html`: HTML shell loaded by browser

**Configuration:**
- `RiksrevisjonApi/RiksrevisjonApi.csproj`: .NET project config (target framework)
- `RiksrevisjonApi/Properties/launchSettings.json`: Dev server URL (`http://localhost:5000`)
- `RiksrevisjonApi/appsettings.json`: ASP.NET Core logging settings
- `riksrevisjon-dashboard/vite.config.js`: Vite plugins and dev proxy config
- `riksrevisjon-dashboard/package.json`: npm dependencies and scripts

**Core Logic:**
- `RiksrevisjonApi/Program.cs` lines 39–221: `ReportService` class (scraping, parsing, caching)
- `RiksrevisjonApi/Program.cs` lines 223–224: `Report` data model (C# record)
- `RiksrevisjonApi/Program.cs` lines 25–33: API endpoint definitions
- `riksrevisjon-dashboard/src/App.vue` lines 1–81: `<script setup>` with all frontend logic
- `riksrevisjon-dashboard/src/App.vue` lines 83–218: `<template>` with full dashboard markup
- `riksrevisjon-dashboard/src/App.vue` lines 220–613: `<style>` with all component CSS

**Testing:**
- None — no test files exist in either project

## Naming Conventions

**Files:**
- Backend: `PascalCase.cs` (standard .NET) — only `Program.cs` exists
- Frontend: `PascalCase.vue` for components, `camelCase.js` for scripts, `kebab-case.css` for styles
- Config: `camelCase.json` for frontend, `camelCase.json` for backend settings

**Directories:**
- Backend: `PascalCase` (`Properties/`)
- Frontend: `lowercase` (`src/`, `components/`, `assets/`, `public/`)

## Where to Add New Code

**New Backend Endpoint:**
- Add to `RiksrevisjonApi/Program.cs` between existing `app.MapGet`/`app.MapPost` calls and `app.Run()` (around line 33)
- If extracting to separate files: create `Services/`, `Models/`, `Endpoints/` directories under `RiksrevisjonApi/`

**New Backend Service/Model:**
- Currently everything is in `Program.cs`. To extract:
  - Models → `RiksrevisjonApi/Models/Report.cs`
  - Services → `RiksrevisjonApi/Services/ReportService.cs`
  - Follow standard .NET namespace conventions

**New Vue Component:**
- Place in `riksrevisjon-dashboard/src/components/`
- Use `PascalCase.vue` naming (e.g., `ReportCard.vue`, `SeverityChart.vue`)
- Import in `App.vue` or parent component

**New Frontend Page/View:**
- Currently single-page with no router. To add routing:
  - Install `vue-router`
  - Create `riksrevisjon-dashboard/src/views/` directory
  - Add `riksrevisjon-dashboard/src/router/index.js`

**New Utility/Helper:**
- Backend: Create `RiksrevisjonApi/Helpers/` or `RiksrevisjonApi/Utils/`
- Frontend: Create `riksrevisjon-dashboard/src/utils/` or `riksrevisjon-dashboard/src/composables/` (for Vue composables)

**New Static Assets:**
- Public (served as-is): `riksrevisjon-dashboard/public/`
- Imported (processed by Vite): `riksrevisjon-dashboard/src/assets/`

## Special Directories

**`RiksrevisjonApi/bin/` and `RiksrevisjonApi/obj/`:**
- Purpose: .NET build output and intermediates
- Generated: Yes
- Committed: No (should be in .gitignore)

**`riksrevisjon-dashboard/node_modules/`:**
- Purpose: npm dependencies
- Generated: Yes
- Committed: No (in `.gitignore`)

**`riksrevisjon-dashboard/dist/`:**
- Purpose: Vite production build output (does not exist until `npm run build`)
- Generated: Yes
- Committed: No

**Cache file (`reports-cache.json`):**
- Purpose: JSON cache of scraped reports
- Location: `{AppContext.BaseDirectory}/reports-cache.json` (in `bin/` output dir at runtime)
- Generated: Yes, at runtime by `ReportService`
- Committed: No

---

*Structure analysis: 2026-04-07*
