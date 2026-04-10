# Phase 5: IT-only filter for reports and notices — Research

**Researched:** 2026-04-10  
**Domain:** Riksrevisjonen HTML scraping + Doffin REST API + cache versioning  
**Confidence:** HIGH (all critical unknowns verified against live URLs)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Change report scraping URL from `/rapporter/?p={page}` to `https://www.riksrevisjonen.no/sok/?t=1&cat=10&p={page}` — SUPERSEDED by research (see Critical Finding #1 below). The actual working URL is `/rapporter/?q=Digitalisering/ikt&p={page}`.
- **D-02:** `/sok/` HTML structure may differ — research must verify. **VERIFIED:** `/sok/` is JavaScript-rendered (empty without browser). Use `/rapporter/?q=Digitalisering/ikt` instead.
- **D-03:** Replace single `searchString=helfo` with 6 IT search terms: `IKT`, `IT`, `utvikling`, `programvare`, `skytjeneste`, `digitalisering`.
- **D-04:** Each search adds `&type=COMPETITION%2CPLANNING`. Full URL: `https://www.doffin.no/search?searchString={term}&type=COMPETITION%2CPLANNING&page={n}`.
- **D-05:** One full paginated scrape per search term. After all 6, merge and deduplicate by URL. Keep notice with lower/first ID on duplicate.
- **D-06:** Post-filter: keep only notices where detail page shows "Kontraktens hovedelement" = "Tjenester". Research must find CSS selector/text pattern. **VERIFIED:** Field is "Kontraktens art" (not "hovedelement"), value "Tjenester". REST API approach available (see §Architecture Patterns).
- **D-07:** On startup, detect config change and delete stale caches if search terms changed.

### the agent's Discretion
- Exact mechanism for cache fingerprinting / version comparison
- Whether to use `.cache-version` file or `_version` field in cache JSON
- Concurrency limits for 6-term Doffin scrape (reuse `SemaphoreSlim(5)` or run sequentially)
- How to assign IDs to deduplicated notices

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.
</user_constraints>

---

## Summary

Five live URL investigations plus Doffin JS bundle analysis resolved all five critical unknowns. Two of the five unknowns have significantly better answers than originally expected.

**Critical Finding #1 — Riksrevisjonen URL:** The specified `/sok/?t=1&cat=10` URL is **JavaScript-rendered** and returns an empty HTML shell (no `rr-link-wrapper`, `blockquote`, `<time>` elements). The existing HttpClient-based `ParseListPage` regex will return zero results against it. The correct URL is `/rapporter/?q=Digitalisering/ikt&p={page}` — this is **server-side rendered**, uses the identical HTML structure as the existing scraper (all regex patterns work unchanged), and is discoverable from the category link on the `/rapporter/` page itself.

**Critical Finding #2 — Doffin REST API:** The Doffin frontend calls a public REST API at `https://api.doffin.no/webclient/api/v2/`. Both the search endpoint (`POST /search-api/search`) and the notice detail endpoint (`GET /notices-api/notices/{id}`) are accessible via plain `HttpClient` — no Playwright browser rendering required. The notice detail JSON contains the `"Kontraktens art"` field directly, eliminating the need to scrape the rendered detail page.

**Primary recommendation:** Use `/rapporter/?q=Digitalisering/ikt&p={page}` for reports (zero code changes to `ParseListPage`). For Doffin, use HttpClient against the REST API — both for list pagination and for the "Tjenester" post-filter — replacing the Playwright `ScrapeListPage`/`FetchDetailDescription` pattern with faster, simpler HttpClient calls.

---

## Standard Stack

No new packages required. All implementation uses existing dependencies.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Net.Http.HttpClient` (built-in) | .NET 9 | Doffin REST API calls | Already registered via `IHttpClientFactory` |
| `System.Text.Json` (built-in) | .NET 9 | Deserialize Doffin API responses | Already used throughout |
| `Microsoft.Playwright` | existing | Doffin list-page scraping (if keeping Playwright path) | Already installed |

**No new NuGet packages needed.** The REST API approach removes the need for Playwright in `DoffinService` entirely, but Playwright can be kept if the planner prefers the existing pattern.

---

## Architecture Patterns

### Recommended Project Structure
No structural changes — single `Program.cs` pattern is preserved. All new logic follows the existing inline class pattern.

---

### Pattern 1: Riksrevisjonen IT-Category URL (VERIFIED)

**What:** Replace `/rapporter/?p={page}` with `/rapporter/?q=Digitalisering/ikt&p={page}`.  
**When to use:** Always — this is the only server-side-rendered IT filter URL.

**Verification results:**
- Page 1: 10 × `rr-link-wrapper`, 10 × `<blockquote>`, 10 × `<time>` ✅
- Page 2–4: 10 results each ✅  
- Page 5: 2 results ✅  
- Page 6+: 0 results (pagination stops cleanly) ✅
- `rr-search-result-meta` div: present, contains "Digitalisering/ikt" ✅

**The existing `ParseListPage` regex works with zero changes.** The `href` pattern `/rapporter-mappe/...` still appears correctly.

```csharp
// In FetchListPage — change ONE line:
// BEFORE:
var html = await client.GetStringAsync($"/rapporter/?p={page}");
// AFTER:
var html = await client.GetStringAsync($"/rapporter/?q=Digitalisering/ikt&p={page}");
```

**Do NOT use** `/sok/?t=1&cat=10` — it is a React SPA page that requires JavaScript execution.

---

### Pattern 2: Doffin REST API for Search + Detail (VERIFIED)

**What:** Call the Doffin backend REST API directly via `HttpClient` instead of Playwright.  
**Why:** The API is publicly accessible (no auth, no CORS restriction, no browser needed). Tested from PowerShell without `Origin` or `Referer` headers — returns HTTP 200 JSON.

#### 2a — List Pagination via Search API

```
POST https://api.doffin.no/webclient/api/v2/search-api/search
Content-Type: application/json
```

Request body (verified format from JS bundle reverse engineering):
```json
{
  "numHitsPerPage": 20,
  "page": 1,
  "searchString": "IKT",
  "sortBy": "RELEVANCE",
  "facets": {
    "cpvCodesLabel":  { "checkedItems": [] },
    "cpvCodesId":     { "checkedItems": [] },
    "type":           { "checkedItems": ["COMPETITION", "PLANNING"] },
    "status":         { "checkedItems": [] },
    "contractNature": { "checkedItems": [] },
    "publicationDate":{ "from": null, "to": null },
    "location":       { "checkedItems": [] },
    "buyer":          { "checkedItems": [] },
    "winner":         { "checkedItems": [] }
  }
}
```

Response:
```json
{
  "numHitsTotal": 5143,
  "numHitsAccessible": 1000,
  "hits": [
    {
      "id": "2026-103994",
      "heading": "Anskaffelse av sikkerhetsovervåking og respons (SOC/IRT)",
      "buyer": [{ "id": "...", "name": "IKT Agder IKS" }],
      "description": "...",
      "type": "ANNOUNCEMENT_OF_COMPETITION",
      "publicationDate": "2026-01-15",
      "issueDate": "2026-01-15T..."
    }
  ],
  "facets": { ... }
}
```

**Pagination:** Increment `page` until `hits.length === 0`. `numHitsAccessible: 1000` caps results at 50 pages (1000/20).

**Notice URL construction:** `https://www.doffin.no/notices/{id}` (e.g., `https://www.doffin.no/notices/2026-103994`).

#### 2b — Detail Fetch + Contract Nature Check

```
GET https://api.doffin.no/webclient/api/v2/notices-api/notices/{id}
```

Response contains an `eform` array. The "Kontraktens art" value is found by recursing through eform blocks:

**New eForm format (2023+):** Label = `"Kontraktens art"`, values = `"Tjenester"` / `"Varer"` / `"Bygge- og anleggsarbeider"`  
**Old format (pre-2023):** Label = `"Nature of the contract"`, values = `"Services"` / `"Supplies"` / `"Works"`

```csharp
// Helper method — returns true if notice is a "Tjenester" contract
static bool IsServiceContract(JsonElement eform)
{
    // eform is JsonElement array — recurse through blocks → sections → subs → subsubs
    // Find any subsub where label == "Kontraktens art" && value == "Tjenester"
    //                   OR  label == "Nature of the contract" && value == "Services"
    foreach (var block in eform.EnumerateArray())
    foreach (var section in block.GetProperty("sections").EnumerateArray())
    foreach (var sub in section.GetProperty("sections").EnumerateArray())
    foreach (var subsub in sub.GetProperty("sections").EnumerateArray())
    {
        var label = subsub.GetProperty("label").GetString() ?? "";
        var value = subsub.GetProperty("value").GetString() ?? "";
        if ((label == "Kontraktens art" && value == "Tjenester") ||
            (label == "Nature of the contract" && value == "Services"))
            return true;
    }
    return false;
}
```

**Alternative — add contractNature filter to search URL** (avoids detail-page scraping entirely):  
Add `contractNature: {checkedItems: ["SERVICES"]}` to the search body. This returns only service contracts at the list level (2261 hits for IKT+SERVICES vs 5143 without). D-06 still requires post-filtering at detail level per decisions, but this can reduce volume significantly.

---

### Pattern 3: Doffin Playwright Approach (Fallback — if REST API not used)

If the planner prefers keeping the Playwright-based list scraping intact:

1. **URL format** (verified correct): `https://www.doffin.no/search?searchString={term}&type=COMPETITION%2CPLANNING&page={n}`
2. **Existing selectors still valid** — `a[class*=card]`, `[data-testid='notice-card-title']`, `p[class*=buyer]`, `p[class*=issue_date]`, `[data-cy='Neste side']` — no changes needed to `ScrapeListPage`.
3. **For "Kontraktens art" check on detail page** (Playwright): The eform is rendered in an accordion. Use text-based selector approach:

```csharp
// Playwright approach for contract nature check on detail page
var contractNatureValue = await page.EvaluateAsync<string>(@"() => {
    // Look for dt element containing 'Kontraktens art' or 'Nature of the contract'
    const dts = Array.from(document.querySelectorAll('dt, th, [class*=label]'));
    const dtEl = dts.find(el => 
        el.textContent?.includes('Kontraktens art') || 
        el.textContent?.includes('Nature of the contract'));
    if (!dtEl) return '';
    const ddEl = dtEl.nextElementSibling;
    return ddEl?.textContent?.trim() ?? '';
}");
bool isTjenester = contractNatureValue == "Tjenester" || contractNatureValue == "Services";
```

**Note:** The exact DOM structure for the rendered eform depends on Playwright rendering. The REST API approach (Pattern 2b) is safer and faster.

---

### Pattern 4: Multi-Term Doffin Scrape Loop

**What:** Run 6 sequential scrapes (one per search term), collect all notices, deduplicate by URL.  
**When to use:** Always — as specified in D-03 through D-05.

```csharp
private static readonly string[] SearchTerms = 
    ["IKT", "IT", "utvikling", "programvare", "skytjeneste", "digitalisering"];

// In ScrapeAsync():
var allNotices = new List<Notice>();
foreach (var term in SearchTerms)
{
    var termNotices = await ScrapeTerm(term);  // paginate until empty
    allNotices.AddRange(termNotices);
}

// Deduplicate by URL — keep first occurrence (lowest ID assigned during scrape)
var deduped = allNotices
    .GroupBy(n => n.Url)
    .Select(g => g.First())
    .Select((n, i) => n with { Id = i + 1 })  // re-assign sequential IDs
    .ToList();

// Post-filter: Tjenester only
// For REST API approach: filter via IsServiceContract(detail) 
// For URL approach: add contractNature=SERVICES or post-filter via detail page
```

**Concurrency recommendation (agent's discretion):** Run each of the 6 term scrapes **sequentially** (one term at a time) to avoid overloading Doffin. Within a single term's pages, scrape list pages sequentially (existing pattern). For detail enrichment, use `SemaphoreSlim(5)` across all deduped notices (existing pattern).

---

### Pattern 5: Cache Fingerprinting

**What:** Embed a `_cacheVersion` field in each cache JSON file. On startup, compare the current config fingerprint against the stored value.  
**Recommendation (agent's discretion):** Embed in JSON root. No separate file management needed.

```csharp
// Version string — change this string when search config changes
private const string CacheVersion = "it-filter-v1";
// Fingerprint embeds search terms + type filter to detect any change
private static string ComputeFingerprint() =>
    $"v={CacheVersion}|terms={string.Join(",", SearchTerms)}|type=COMPETITION,PLANNING";

// Wrapper record for versioned cache
record CacheEnvelope<T>(string Version, List<T> Data);

// In TryLoadCache:
var envelope = JsonSerializer.Deserialize<CacheEnvelope<Notice>>(json);
if (envelope?.Version != ComputeFingerprint()) return false;  // stale — reject

// In SaveCache:
var envelope = new CacheEnvelope<Notice>(ComputeFingerprint(), Notices.ToList());
File.WriteAllText(CacheFile, JsonSerializer.Serialize(envelope));
```

**Alternative:** Write a companion `.cache-version` file (simpler code, adds file management overhead). **Not recommended** — the envelope approach is atomic and self-contained.

**Stale cache cleanup:** In `LoadAsync()`, if `TryLoadCache` returns false due to version mismatch, delete the three cache files before scraping:
```csharp
if (!forceRefresh && !TryLoadCache(out var cached))
{
    // Delete all three caches if our cache was stale (version mismatch)
    // This prevents matches-cache from referencing non-existent notices
    foreach (var f in new[] { CacheFile, ReportsCacheFile, MatchesCacheFile })
        if (File.Exists(f)) File.Delete(f);
}
```
Note: `DoffinService` should delete all three because matches depend on both reports and notices.

---

### Anti-Patterns to Avoid

- **Using `/sok/?t=1&cat=10`:** JavaScript-rendered, empty without browser. Will silently return 0 reports.
- **Using HttpClient for Doffin detail pages (the www.doffin.no URL):** Returns 1306-byte HTML shell. Use the REST API endpoint `api.doffin.no/...` instead.
- **Checking for "Kontraktens hovedelement":** The actual field label is "Kontraktens art". Searching for "hovedelement" will match nothing.
- **Running all 6 Doffin scrapes in parallel:** Risks overloading Doffin's backend; run terms sequentially.
- **Deleting only notices-cache.json on version change:** Matches-cache also becomes stale — must delete all three.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HTTP pagination with retry | Custom retry logic | Existing `FetchListPage` pattern (try/catch returns `[]`) | Already handles failures gracefully |
| JSON serialization | Custom serializer | `System.Text.Json` (existing `JsonOpts`) | Consistent with existing code |
| Contract nature detection | Regex on HTML | Direct API field: `eform.*.label == "Kontraktens art"` | Exact match, no fragility |
| Cache invalidation | File timestamp comparison | Version string in JSON envelope | Timestamp approach misses same-day config changes |

---

## Runtime State Inventory

> Phase is a filter/refactor of existing scraping — searches the existing runtime state.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | `notices-cache.json` at `{AppContext.BaseDirectory}` — contains old `helfo`-based results | Delete on startup if version mismatch (D-07) |
| Stored data | `reports-cache.json` — contains all reports (not IT-filtered) | Delete on startup if version mismatch (D-07) |
| Stored data | `matches-cache.json` — references IDs from old notices/reports | Delete on startup if version mismatch (D-07) |
| Live service config | None — no external services configured | — |
| OS-registered state | None — no scheduled tasks or service registrations | — |
| Secrets/env vars | None — no auth tokens needed for Doffin API | — |
| Build artifacts | None — no published binaries with hardcoded URLs | — |

**Startup sequence:** `DoffinService.LoadAsync()` must check version FIRST, delete stale files, THEN scrape. `MatchService.LoadAsync()` must wait for both upstream services as currently implemented.

---

## Common Pitfalls

### Pitfall 1: Wrong Riksrevisjonen URL (JS-rendered)
**What goes wrong:** Pointing the scraper at `/sok/?t=1&cat=10` — `ParseListPage` returns 0 results silently.  
**Why it happens:** The `/sok/` page is a React SPA — the category filter is applied by JavaScript after page load. Without a browser, the HTML is an empty skeleton.  
**How to avoid:** Use `/rapporter/?q=Digitalisering/ikt&p={page}`. This is the server-rendered category filter URL.  
**Warning signs:** `[scrape] Found 0 pages` or `[scrape] Done — 0 reports loaded`.

### Pitfall 2: "Kontraktens hovedelement" vs "Kontraktens art"
**What goes wrong:** Searching for "Kontraktens hovedelement" finds nothing in the DOM or API.  
**Why it happens:** The actual label in both the Doffin eForm JSON and the rendered page is "Kontraktens art". The term "hovedelement" appears in older Doffin documentation but not in the current data.  
**How to avoid:** Check for BOTH `"Kontraktens art"` (new format, Norwegian value "Tjenester") AND `"Nature of the contract"` (old format, English value "Services").

### Pitfall 3: Using `www.doffin.no` URLs for HttpClient Detail Scraping
**What goes wrong:** `client.GetStringAsync("https://www.doffin.no/notices/2026-103994")` returns the 1306-byte SPA shell.  
**Why it happens:** Doffin's www domain always returns the React shell regardless of path.  
**How to avoid:** Use `https://api.doffin.no/webclient/api/v2/notices-api/notices/{id}` for JSON data.

### Pitfall 4: Stale matches-cache After Notice Cache Delete
**What goes wrong:** Notices are re-scraped (new IT-only set) but matches cache loaded from disk still references old notice IDs.  
**Why it happens:** The three caches have different ages/versions. If only `notices-cache.json` is deleted, `matches-cache.json` may survive with stale references.  
**How to avoid:** Version-invalidate all three caches atomically. When version changes, delete all three before any service starts scraping.

### Pitfall 5: Empty URL Deduplication Key
**What goes wrong:** Notices with empty `Url` field all deduplicate to the same key, dropping valid notices.  
**Why it happens:** If a `ScrapeListPage` returns notices with `Url = ""` due to a fetch error, they all collide on deduplication.  
**How to avoid:** Only include notices with non-empty URLs in deduplication: `GroupBy(n => n.Url).Where(g => !string.IsNullOrEmpty(g.Key))`.

---

## Code Examples

### Riksrevisjonen URL Change (1-line fix)
```csharp
// FetchListPage — verified working with existing ParseListPage
static async Task<List<Report>> FetchListPage(HttpClient client, int page)
{
    try
    {
        // CHANGED: /rapporter/?p= → /rapporter/?q=Digitalisering/ikt&p=
        var html = await client.GetStringAsync($"/rapporter/?q=Digitalisering/ikt&p={page}");
        return ParseListPage(html, page);
    }
    catch { return []; }
}
```

### Doffin REST API Search Client Setup
```csharp
// In Program.cs builder.Services setup
builder.Services.AddHttpClient("doffin-api", c =>
{
    c.BaseAddress = new Uri("https://api.doffin.no/webclient/api/v2/");
    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; RRDashboard/1.0)");
    c.DefaultRequestHeaders.Add("Accept", "application/json");
    c.Timeout = TimeSpan.FromSeconds(30);
});
```

### Doffin Search API Pagination (HttpClient approach)
```csharp
private async Task<List<Notice>> ScrapeTermAsync(HttpClient apiClient, string term)
{
    var notices = new List<Notice>();
    int page = 1;
    while (true)
    {
        var body = JsonSerializer.Serialize(new
        {
            numHitsPerPage = 20, page,
            searchString = term,
            sortBy = "RELEVANCE",
            facets = new
            {
                type = new { checkedItems = new[] { "COMPETITION", "PLANNING" } },
                contractNature = new { checkedItems = Array.Empty<string>() },
                cpvCodesLabel = new { checkedItems = Array.Empty<string>() },
                cpvCodesId = new { checkedItems = Array.Empty<string>() },
                status = new { checkedItems = Array.Empty<string>() },
                publicationDate = new { from = (string?)null, to = (string?)null },
                location = new { checkedItems = Array.Empty<string>() },
                buyer = new { checkedItems = Array.Empty<string>() },
                winner = new { checkedItems = Array.Empty<string>() }
            }
        });
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var resp = await apiClient.PostAsync("search-api/search", content);
        resp.EnsureSuccessStatusCode();
        var result = JsonSerializer.Deserialize<DoffinSearchResult>(
            await resp.Content.ReadAsStringAsync(), JsonOpts)!;
        if (result.Hits.Count == 0) break;
        notices.AddRange(result.Hits.Select(h => MapHit(h, page)));
        page++;
    }
    return notices;
}
```

### Cache Envelope Pattern
```csharp
record CacheEnvelope<T>(string Version, List<T> Data);

private static readonly string SearchVersion = 
    $"it-v1|{string.Join(",", SearchTerms)}|COMPETITION,PLANNING";

private bool TryLoadCache(out List<Notice>? notices)
{
    notices = null;
    if (!File.Exists(CacheFile)) return false;
    if (File.GetLastWriteTime(CacheFile) < DateTime.Now - CacheMaxAge) return false;
    try
    {
        var json = File.ReadAllText(CacheFile);
        var env = JsonSerializer.Deserialize<CacheEnvelope<Notice>>(json, JsonOpts);
        if (env?.Version != SearchVersion) return false;  // version mismatch → stale
        notices = env.Data;
        return notices?.Count > 0;
    }
    catch { return false; }
}
```

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET 9 | Backend runtime | ✓ | net9.0 (project TFM) | — |
| Playwright Chromium | D-04 Doffin list scraping (Playwright path) | ✓ | Installed via `EnsureBrowsersInstalled()` | REST API approach (no browser needed) |
| `api.doffin.no` HTTPS | D-06 Contract nature check | ✓ | Accessible, no auth needed | — |
| `riksrevisjonen.no` HTTPS | D-01/D-02 Report scraping | ✓ | `/rapporter/?q=Digitalisering/ikt` verified | — |

---

## Validation Architecture

> `nyquist_validation: true` in config.json — section required.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 |
| Config file | `RiksrevisjonApi.Tests/RiksrevisjonApi.Tests.csproj` |
| Quick run command | `dotnet test RiksrevisjonApi.Tests --no-build -q` |
| Full suite command | `dotnet test RiksrevisjonApi.Tests -q` |

### Phase Requirements → Test Map

| ID | Behavior | Test Type | Automated Command | File Exists? |
|----|----------|-----------|-------------------|-------------|
| IT-01 | `ParseListPage` correctly parses `/rapporter/?q=Digitalisering/ikt` HTML (same structure as `/rapporter/`) | unit | `dotnet test --filter "ParseListPage"` | ❌ Wave 0 |
| IT-02 | Multi-term Doffin scrape deduplicates by URL, keeps first | unit | `dotnet test --filter "Deduplication"` | ❌ Wave 0 |
| IT-03 | `IsServiceContract` returns true for eform with "Kontraktens art" = "Tjenester" | unit | `dotnet test --filter "IsServiceContract"` | ❌ Wave 0 |
| IT-04 | `IsServiceContract` returns true for old-format "Nature of the contract" = "Services" | unit | `dotnet test --filter "IsServiceContract"` | ❌ Wave 0 |
| IT-05 | Cache version mismatch triggers cache deletion | unit | `dotnet test --filter "CacheVersion"` | ❌ Wave 0 |
| IT-06 | ID re-assignment after deduplication is sequential starting at 1 | unit | `dotnet test --filter "IdReassignment"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test RiksrevisjonApi.Tests --filter "Category=Unit" -q`
- **Per wave merge:** `dotnet test RiksrevisjonApi.Tests -q`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `RiksrevisjonApi.Tests/ItFilterTests.cs` — covers IT-01 through IT-06
- [ ] `RiksrevisjonApi.Tests/DoffinDeduplicationTests.cs` — covers IT-02
- [ ] `RiksrevisjonApi.Tests/CacheVersionTests.cs` — covers IT-05

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `/rapporter/?p=N` (all reports) | `/rapporter/?q=Digitalisering/ikt&p=N` (IT only) | Phase 5 | ~42 reports instead of 200+ |
| `searchString=helfo` (1 term) | 6 IT terms with dedup + post-filter | Phase 5 | More relevant notices, deduplicated |
| Playwright detail scraping for description | Optionally: REST API `GET /notices-api/notices/{id}` | Phase 5 | No browser needed for detail enrichment |

---

## Open Questions

1. **Playwright vs REST API for DoffinService list scraping**
   - What we know: The REST API works. The Playwright approach also works (unchanged selectors).
   - What's unclear: The planner must decide whether to replace `ScrapeListPage` entirely (REST API path) or extend it (add `term` parameter, keep Playwright).
   - Recommendation: Use REST API for both list pagination AND detail enrichment (eliminates browser overhead entirely). The `SemaphoreSlim(5)` pattern is preserved but applies to REST API calls.

2. **`contractNature=SERVICES` pre-filter vs post-filter**
   - What we know: Adding `contractNature: {checkedItems: ["SERVICES"]}` to the search body reduces IKT results from 5143 to 2261 (D-06 style pre-filter). D-06 says post-filter at detail level.
   - What's unclear: Is the 2261 number after type filter (COMPETITION+PLANNING)? Testing showed `contractNature=SERVICES + type=COMPETITION,PLANNING` gives 2261 hits.
   - Recommendation: Add pre-filter to search body AND keep post-filter check at detail level as belt-and-suspenders. The pre-filter reduces the number of detail-page fetches by ~56%.

3. **Old-format notices (pre-eForm)**
   - What we know: Pre-2023 notices use English labels ("Nature of the contract" = "Services"). Current cached data (helfo notices from 2025) uses new eForm format.
   - What's unclear: Will the 2025+ IT notices ever have the old format?
   - Recommendation: Handle both label/value combinations as shown in Pattern 2b. Low overhead, high safety.

---

## Sources

### Primary (HIGH confidence)
- Live URL: `https://www.riksrevisjonen.no/rapporter/?q=Digitalisering/ikt&p=1` — verified HTML structure, selectors, pagination
- Live URL: `https://www.riksrevisjonen.no/sok/?t=1&cat=10&p=1` — verified JS-rendered (empty), do not use
- Live API: `https://api.doffin.no/webclient/api/v2/notices-api/notices/2026-103994` — verified eform structure, "Kontraktens art" = "Tjenester"
- Live API: `https://api.doffin.no/webclient/api/v2/search-api/search` (POST) — verified search with contractNature filter
- JS bundle: `https://www.doffin.no/assets/index-BDYm66pL.js` — verified API base URLs, search body format (p9 function), eform rendering components

### Secondary (MEDIUM confidence)
- Existing `Program.cs` — existing ParseListPage regex patterns confirmed against live HTML

### Tertiary (LOW confidence)
- "Kontraktens hovedelement" as a label: RESEARCH SHOWS this label does NOT appear in current Doffin data. The actual field is "Kontraktens art". The term "hovedelement" from CONTEXT.md likely refers to conceptual description only.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages, verified existing patterns work
- Riksrevisjonen URL: HIGH — live URL verified, HTML structure confirmed identical
- Doffin REST API: HIGH — called directly, no auth, eform structure fully mapped
- Architecture: HIGH — code examples derived from verified API responses
- Pitfalls: HIGH — all verified against live systems

**Research date:** 2026-04-10  
**Valid until:** 2026-07-10 (Riksrevisjonen URL stable, Doffin API stable; Doffin JS bundle hash may change but API contract is unlikely to change)
