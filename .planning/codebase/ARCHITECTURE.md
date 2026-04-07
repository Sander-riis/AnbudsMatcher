# Architecture

**Analysis Date:** 2026-04-07

## Pattern Overview

**Overall:** Two-tier client/server — a .NET 9 Minimal API backend that scrapes riksrevisjonen.no and a Vue 3 SPA frontend that polls the API and renders a dashboard. No database; data is scraped on startup, cached to a JSON file, and served from memory.

**Key Characteristics:**
- Single-file backend (`Program.cs`) using .NET Minimal API conventions (no controllers, no MVC)
- Singleton in-memory `ReportService` with file-based JSON cache
- Frontend is a single-component Vue 3 app (`App.vue`) with all logic, template, and styles co-located
- Vite dev server proxies `/api` requests to the .NET backend at `http://localhost:5000`
- Reports stream progressively — the API returns partial data while scraping is still in progress; the frontend polls until `loading === false`

## Layers

**API Layer (HTTP Endpoints):**
- Purpose: Expose scraped report data to the frontend
- Location: `RiksrevisjonApi/Program.cs` (lines 25–33)
- Contains: Two minimal API endpoints defined via `app.MapGet` and `app.MapPost`
- Depends on: `ReportService`
- Used by: Vue frontend via `fetch('/api/reports')`

**Service Layer (Scraping + Caching):**
- Purpose: Scrape riksrevisjonen.no, parse HTML, cache results, serve reports from memory
- Location: `RiksrevisjonApi/Program.cs` — `ReportService` class (lines 39–221)
- Contains: HTTP scraping logic, regex-based HTML parsing, JSON file caching, severity extraction
- Depends on: `IHttpClientFactory` (named client `"rr"`)
- Used by: API endpoints

**Data Model:**
- Purpose: Represent a single audit report
- Location: `RiksrevisjonApi/Program.cs` (line 223–224)
- Contains: C# `record Report` with 7 fields
- Used by: `ReportService`, serialized to JSON for the API response

**Frontend (Single-Page Application):**
- Purpose: Display reports grouped by severity with filtering, search, and distribution chart
- Location: `riksrevisjon-dashboard/src/App.vue`
- Contains: All UI logic, template, and styles in one file
- Depends on: `/api/reports` endpoint
- Used by: End users via browser

## Data Model

**`Report` record** (`RiksrevisjonApi/Program.cs`, line 223–224):

```csharp
record Report(int Id, string Title, string Summary, string Severity,
              string PublishedDate, string Department, string Url);
```

| Field           | Type     | Description                                      |
|-----------------|----------|--------------------------------------------------|
| `Id`            | `int`    | Sequential ID assigned during list-page parsing   |
| `Title`         | `string` | Report title, HTML-decoded                        |
| `Summary`       | `string` | First paragraph of blockquote, tags stripped       |
| `Severity`      | `string` | One of 4 severity levels (see below)              |
| `PublishedDate`  | `string` | ISO date string `yyyy-MM-dd` or empty             |
| `Department`    | `string` | Category/department from search result metadata   |
| `Url`           | `string` | Full URL to the report on riksrevisjonen.no       |

**Severity levels** (ordered by criticality):
1. `"Sterkt kritikkverdig"` — Most critical
2. `"Kritikkverdig"` — Critical
3. `"Ikke tilfredsstillende"` — Unsatisfactory
4. `"Ingen karakter"` — No grade (default/fallback)

## API Shape

**`GET /api/reports`** — Returns all reports with loading state

Response:
```json
{
  "loading": false,
  "reports": [
    {
      "id": 1,
      "title": "...",
      "summary": "...",
      "severity": "Kritikkverdig",
      "publishedDate": "2024-01-15",
      "department": "Forsvarsdepartementet",
      "url": "https://www.riksrevisjonen.no/rapporter-mappe/..."
    }
  ]
}
```

- While scraping is ongoing, `loading: true` and `reports` contains partial results
- Frontend polls every 2 seconds when `loading === true`

**`POST /api/reports/refresh`** — Force a fresh scrape (ignores cache)

- Returns `202 Accepted` immediately
- Triggers background re-scrape
- Frontend should poll `GET /api/reports` after calling this

## Data Flow

**Startup Scraping Pipeline:**

1. `Program.cs` line 23: `svc.LoadAsync()` is fire-and-forget on startup
2. `LoadAsync()` checks JSON file cache at `{AppContext.BaseDirectory}/reports-cache.json` — if valid (< 12 hours old), loads from cache and returns
3. If cache is stale or missing, `ScrapeAsync()` begins
4. **Page discovery**: Sequential probe of `https://www.riksrevisjonen.no/rapporter/?p={page}` incrementing page until an empty result
5. **Parallel page fetch**: All confirmed pages re-fetched in parallel via `Task.WhenAll`
6. **List parsing** (`ParseListPage`): Regex extracts from each `<a class="rr-link-wrapper">` block: href, title (h2), date (time), summary (blockquote), department (div.rr-search-result-meta)
7. **Parallel enrichment** (`Enrich`): Each report's detail page fetched with concurrency limit of 8 (`SemaphoreSlim(8)`)
8. **Severity extraction** (`ExtractSeverity`): Two-pass regex — first looks for `rr-font--space-mono-bold` CSS class label, then fallback to `<strong>` tags; matches against known Norwegian severity strings
9. Reports are added to `_reports` as enrichment completes (progressive loading)
10. On completion, cache is written to disk

**Frontend Rendering Pipeline:**

1. `onMounted` → `fetchReports()` calls `GET /api/reports`
2. If `data.loading === true`, sets 2-second poll timer
3. `filtered` computed: applies severity filter + text search, sorts by `severityOrder`
4. `grouped` computed: groups filtered results by severity level for section rendering
5. `counts` computed: counts per severity for filter chips and chart bars
6. `chartBars` computed: calculates percentages and widths for the distribution chart

**Severity Extraction Detail** (`RiksrevisjonApi/Program.cs`, lines 191–217):

```
Pass 1: Look for CSS class "rr-font--space-mono-bold" → extract <div> text inside
Pass 2 (fallback): Scan all <strong> tags for severity keywords
Keywords matched (case-insensitive):
  - "sterkt kritikkverdig" → "Sterkt kritikkverdig"
  - "kritikkverdig" → "Kritikkverdig"  (checked AFTER "sterkt" to avoid false match)
  - "ikke tilfredsstillende" → "Ikke tilfredsstillende"
Default: "Ingen karakter"
```

## Entry Points

**Backend Entry Point:**
- Location: `RiksrevisjonApi/Program.cs`
- Triggers: `dotnet run` from `RiksrevisjonApi/` directory
- Responsibilities: Configures CORS, HTTP client, registers ReportService, defines API routes, starts Kestrel on port 5000

**Frontend Entry Point:**
- Location: `riksrevisjon-dashboard/src/main.js`
- Triggers: `npm run dev` (Vite dev server) or built `index.html`
- Responsibilities: Creates Vue app, mounts to `#app`

**HTML Shell:**
- Location: `riksrevisjon-dashboard/index.html`
- Loads: `/src/main.js` as ES module

## Error Handling

**Strategy:** Minimal — silent failures with fallback defaults

**Backend Patterns:**
- Scrape page fetch failures: silently return empty list (`catch { return []; }`) — `RiksrevisjonApi/Program.cs`, line 148
- Enrichment failures: add un-enriched report with default severity `"Ingen karakter"` — `RiksrevisjonApi/Program.cs`, line 133
- Cache load failures: silently return false, trigger fresh scrape — `RiksrevisjonApi/Program.cs`, line 82
- Cache save failures: log to console, continue — `RiksrevisjonApi/Program.cs`, line 93
- No global exception handling or error response middleware

**Frontend Patterns:**
- No try/catch around `fetch('/api/reports')` — unhandled promise rejection on network error
- No error state displayed to user

## Cross-Cutting Concerns

**CORS:** Wide-open (`AllowAnyOrigin`, `AllowAnyHeader`, `AllowAnyMethod`) — `RiksrevisjonApi/Program.cs`, lines 7–8

**Logging:** `Console.WriteLine` only — no structured logging framework. Log messages prefixed with `[cache]` or `[scrape]` tags.

**Validation:** None — no input validation on either endpoint (no query parameters accepted)

**Authentication:** None — both endpoints are publicly accessible

**Concurrency:** `ReportService` is registered as singleton. `_reports` list protected by `lock` for thread-safe reads/writes during parallel scraping. `SemaphoreSlim(8)` throttles concurrent HTTP requests to riksrevisjonen.no.

**Caching:** File-based JSON cache at `{AppContext.BaseDirectory}/reports-cache.json` with 12-hour TTL. No in-memory cache eviction — reports persist in memory for the lifetime of the process.

---

*Architecture analysis: 2026-04-07*
