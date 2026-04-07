# Phase 1: Doffin Scraper - Research

**Researched:** 2026-04-07
**Domain:** Web scraping (Playwright headless Chromium), .NET 9 Minimal API, React SPA scraping
**Confidence:** HIGH

## Summary

Doffin.no is a React SPA that serves a 1306-byte HTML shell with `<div id="root"></div>` — all content is rendered client-side by JavaScript. This was verified by fetching the raw HTML (no content) and then rendering with Playwright (full content). **Playwright headless Chromium is mandatory; HttpClient is useless here.**

The scraping approach has been fully validated using both Node.js Playwright (DOM inspection) and .NET Playwright 1.58.0 (actual code verification). All CSS selectors, pagination mechanics, data extraction patterns, and parallel detail-page fetching have been tested against the live Doffin.no site. The existing `ReportService` pattern in `Program.cs` provides a clear template: singleton service, `lock()`-protected list, JSON file cache with 12h TTL, fire-and-forget startup, and `{ loading, notices }` response shape.

The most important discovery is that Doffin.no provides stable `data-testid` and `data-cy` attributes on key elements (cards, titles, descriptions, pagination buttons), making selectors resilient to CSS module hash changes. The search supports direct URL pagination (`&page=N`), and the result count text enables computing total pages upfront.

**Primary recommendation:** Implement `DoffinService` in `Program.cs` following the exact `ReportService` singleton pattern, using `Microsoft.Playwright` 1.58.0 with one shared browser instance per scrape operation, `SemaphoreSlim(5)` for parallel detail pages, and `data-testid`/`data-cy` selectors for maximum stability.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| REQ-01 | Scrape all procurement notices from doffin.no/search?searchString=helfo using Playwright headless Chromium. Paginate all result pages until no next-page button is found. Each notice must include: title, buyer, publication date, notice URL. | Verified: 106 notices across 6 pages. Selectors confirmed for all fields. Pagination via URL `&page=N` with "Neste side" button `disabled` attribute as termination signal. |
| REQ-02 | For each notice from list pages, fetch the detail sub-page to extract full description text. Fetch in parallel with SemaphoreSlim(5). Cache results to notices-cache.json (12h TTL). | Verified: Detail pages at `/notices/{id}` contain full description in `p[class*=content_section_description]`. Parallel fetching with shared browser + SemaphoreSlim(5) tested successfully (3 pages in ~2.4s). Cache pattern mirrors existing `reports-cache.json`. |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Playwright | 1.58.0 | Headless Chromium browser automation for scraping React SPA | Only viable approach for JS-rendered sites; official Microsoft package; verified working with .NET 9 |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | built-in | JSON serialization for cache and API responses | Already used by ReportService for cache pattern |
| System.Text.RegularExpressions | built-in | Parsing result count text and date strings | Already used by ReportService |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Playwright | HttpClient + AngleSharp | Would NOT work — Doffin is a React SPA, no server-rendered HTML |
| Playwright | Selenium WebDriver | Heavier, slower, requires separate driver management |
| Playwright | PuppeteerSharp | Less maintained .NET port; Playwright is the modern successor |

**Installation:**
```bash
cd RiksrevisjonApi
dotnet add package Microsoft.Playwright --version 1.58.0
dotnet build
# Then install Chromium browser (run once):
# In code: Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
# OR from build output: dotnet exec bin/Debug/net9.0/Microsoft.Playwright.dll install chromium
```

**Version verification:** `Microsoft.Playwright` 1.58.0 is the latest stable release on NuGet as of 2026-04-07. It downloads Chromium v1208 (Chrome for Testing 145.0.7632.6) to `%LOCALAPPDATA%\ms-playwright\chromium-1208`.

## Architecture Patterns

### DoffinService Placement
```
RiksrevisjonApi/
└── Program.cs          # ALL code goes here — no new files
    ├── lines 1-35      # Existing: builder, CORS, HttpClient, ReportService, endpoints, app.Run()
    ├── lines 37-221    # Existing: ReportService class
    ├── lines 223-224   # Existing: Report record
    ├── NEW             # DoffinService class (mirrors ReportService pattern)
    └── NEW             # Notice record
```

### Pattern 1: Singleton Service with Streaming (mirror ReportService exactly)
**What:** DoffinService registered as singleton, fire-and-forget LoadAsync on startup, streaming results via lock()-protected list
**When to use:** This is the ONLY pattern — matches existing architecture

```csharp
// Source: Verified pattern from existing Program.cs lines 39-221
class DoffinService
{
    private static readonly string CacheFile = Path.Combine(
        AppContext.BaseDirectory, "notices-cache.json");
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private List<Notice> _notices = [];
    public bool IsLoading { get; private set; }
    public IReadOnlyList<Notice> Notices { get { lock (_notices) return _notices.ToList(); } }

    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        try
        {
            if (!forceRefresh && TryLoadCache(out var cached))
            {
                lock (_notices) { _notices.Clear(); _notices.AddRange(cached!); }
                Console.WriteLine($"[cache] Loaded {cached!.Count} notices from {CacheFile}");
                return;
            }
            await ScrapeAsync();
            SaveCache();
        }
        finally { IsLoading = false; }
    }
    // ... TryLoadCache, SaveCache mirror ReportService exactly
    // ... ScrapeAsync uses Playwright instead of HttpClient
}
```

### Pattern 2: Playwright Lifecycle in ScrapeAsync (create-use-dispose per scrape)
**What:** Create IPlaywright + IBrowser at start of ScrapeAsync, dispose at end. Don't keep browser running between refreshes.
**When to use:** Always — avoids memory leaks from long-lived browser processes

```csharp
// Source: Verified with test project against live Doffin.no
private async Task ScrapeAsync()
{
    lock (_notices) _notices.Clear();

    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(
        new() { Headless = true });

    // 1. Scrape list pages (sequential pagination)
    int pageNum = 1;
    while (true)
    {
        var (notices, hasNext) = await ScrapeListPage(browser, pageNum);
        // Add to _notices with lock as they arrive
        lock (_notices) _notices.AddRange(notices);
        if (!hasNext) break;
        pageNum++;
    }
    Console.WriteLine($"[doffin] Found {_notices.Count} notices across {pageNum} pages");

    // 2. Fetch detail pages in parallel
    var sem = new SemaphoreSlim(5);
    List<Notice> snapshot;
    lock (_notices) snapshot = _notices.ToList();

    var enrichTasks = snapshot.Select(async notice =>
    {
        await sem.WaitAsync();
        try
        {
            var description = await FetchDetailDescription(browser, notice.Url);
            var enriched = notice with { Description = description };
            lock (_notices)
            {
                var idx = _notices.FindIndex(n => n.Id == notice.Id);
                if (idx >= 0) _notices[idx] = enriched;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[doffin] Detail fetch failed for {notice.Url}: {ex.Message}");
        }
        finally { sem.Release(); }
    });
    await Task.WhenAll(enrichTasks);
    // browser and playwright auto-disposed by using/await using
}
```

### Pattern 3: Page-per-request for Parallel Detail Fetching
**What:** Open a new `IPage` for each detail page fetch, close after extraction
**When to use:** For SemaphoreSlim(5) parallel detail page fetching

```csharp
// Source: Verified with live Doffin.no — 3 pages in 2.4 seconds
private static async Task<string> FetchDetailDescription(IBrowser browser, string url)
{
    var page = await browser.NewPageAsync();
    try
    {
        await page.GotoAsync(url, new() {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });
        await page.WaitForSelectorAsync("h1", new() { Timeout = 10_000 });

        var descEl = await page.QuerySelectorAsync("p[class*=content_section_description]");
        return descEl != null
            ? (await descEl.TextContentAsync())?.Trim() ?? ""
            : "";
    }
    finally { await page.CloseAsync(); }
}
```

### Anti-Patterns to Avoid
- **Don't create a new `IPlaywright` per page fetch:** `Playwright.CreateAsync()` is expensive. Create once in ScrapeAsync, share the `IBrowser` for all operations.
- **Don't use `EvaluateAsync<List<Dictionary<string, string>>>` for complex data:** The Playwright .NET JSON deserializer has issues with generic dictionary types. Use `QuerySelectorAsync` + `TextContentAsync`/`GetAttributeAsync` per element instead.
- **Don't keep the browser running between cache refreshes:** Create in ScrapeAsync, dispose when done. This avoids zombie Chromium processes.
- **Don't use `WaitUntilState.Load`:** The React SPA needs full JS execution. Use `WaitUntilState.NetworkIdle` to wait for API calls to complete.
- **Don't extract files or create new folders:** All code goes in `Program.cs` per project architecture.

## Doffin.no DOM Intelligence

### Verified Selectors (HIGH confidence — tested on live site 2026-04-07)

#### Search Results Page (`/search?searchString=helfo&page={N}`)

| Data | Selector | Extraction Method | Example Value |
|------|----------|-------------------|---------------|
| Result count | `p[class*=result_text]` | `TextContentAsync()` | `"Viser 1-20 av 106 treff"` |
| Notice card | `a[class*=card]` | `QuerySelectorAllAsync()` | 20 per page |
| Card URL | card element | `GetAttributeAsync("href")` | `"/notices/2025-112516"` |
| Buyer name | `p[class*=buyer]` within card | `TextContentAsync()` | `"Helfo"` |
| Title | `h2[data-testid="notice-card-title"]` within card | `TextContentAsync()` | Full title text |
| Short description | `p[data-testid="notice-card-description"]` within card | `TextContentAsync()` | Truncated description |
| Publication date | `p[class*=issue_date]` within card | `GetAttributeAsync("aria-label")` | `"Kunngjøringsdato: 01.09.2025"` |
| Next page button | `button[data-cy="Neste side"]` | Check `disabled` attribute | `disabled` on last page |

#### Notice Detail Page (`/notices/{id}`)

| Data | Selector | Extraction Method | Example Value |
|------|----------|-------------------|---------------|
| Full title | `h1` | `TextContentAsync()` | Full notice title |
| Full description | `p[class*=content_section_description]` | `TextContentAsync()` | 300-700 chars typically |
| Buyer name | `div[class*=content_container]` starting with "Offisielt navn" | Parse text content | `"Helfo"` |
| Reference ID | `dl[class*=description_list]` dt/dd pair "Referanse" | Parse dt/dd | `"2025-112516"` |
| Pub date | `dl[class*=description_list]` dt/dd pair "Kunngjøringsdato" | Parse dt/dd | `"01.09.2025"` |

### Selector Stability Notes

Doffin.no uses CSS Modules with hashed suffixes (e.g., `_card_1oojq_1`). The hash portion (`1oojq_1`) changes between builds. Two selector strategies are available:

1. **`[class*=card]` (partial class match):** Works because the base name (`card`, `buyer`, `title`) is stable. Hash suffix is irrelevant with `*=` matching.
2. **`[data-testid="notice-card-title"]` and `[data-cy="Neste side"]`:** Most stable — these are test automation attributes that won't change with CSS builds.

**Recommendation:** Use `data-testid` / `data-cy` where available; fall back to `[class*=X]` for elements without test attributes.

### Pagination Mechanics

- **URL pattern:** `https://www.doffin.no/search?searchString=helfo&page={N}` (page 1 has no `&page` param, or `&page=1`)
- **Items per page:** 20 (consistently observed)
- **Total results:** Parseable from `p[class*=result_text]` → regex `av (\d+) treff` → 106 results → ceil(106/20) = 6 pages
- **Termination signal:** `button[data-cy="Neste side"]` has `disabled` attribute on last page
- **Direct navigation works:** Confirmed that navigating to `&page=3` directly loads page 3 results
- **Current data:** 106 notices for "helfo" search across 6 pages (20+20+20+20+20+6)

### Data Model

```csharp
record Notice(int Id, string Title, string Buyer, string PublishedDate,
              string Url, string Description);
```

| Field | Source | Parsing |
|-------|--------|---------|
| `Id` | Sequential, assigned during scraping | `(page-1)*20 + index + 1` |
| `Title` | `h2[data-testid="notice-card-title"]` on list page | `TextContentAsync().Trim()` |
| `Buyer` | `p[class*=buyer]` on list page | `TextContentAsync().Trim()` |
| `PublishedDate` | `p[class*=issue_date]` aria-label | Parse `"Kunngjøringsdato: DD.MM.YYYY"` → `DateOnly.TryParseExact("dd.MM.yyyy")` → `"yyyy-MM-dd"` |
| `Url` | Card `href` attribute | Prefix with `"https://www.doffin.no"` |
| `Description` | Detail page `p[class*=content_section_description]` | `TextContentAsync().Trim()`, fallback to empty string |

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| React SPA rendering | Custom JS evaluation + DOM parsing | Playwright `WaitForSelectorAsync` + `QuerySelectorAsync` | Handles all async rendering, network requests, route changes |
| Browser lifecycle | Manual process management | Playwright `using`/`await using` disposal pattern | Auto-cleanup of Chromium processes |
| Concurrent page access | Manual thread pool | `SemaphoreSlim(5)` + `Task.WhenAll` with new `IPage` per request | Same pattern as existing ReportService |
| JSON cache | Custom file format | `System.Text.Json` + `File.WriteAllText`/`File.ReadAllText` | Exact pattern from existing ReportService |

**Key insight:** The existing `ReportService` already solved streaming, caching, concurrency, and API exposure. `DoffinService` is a near-copy with Playwright replacing HttpClient.

## Common Pitfalls

### Pitfall 1: CSS Module Hash Changes Breaking Selectors
**What goes wrong:** Doffin deploys a new build → CSS class hashes change → `_card_1oojq_1` becomes `_card_xyz123_1` → selectors break
**Why it happens:** CSS Modules generate unique hashes per build
**How to avoid:** Use `[class*=card]` (partial match) or `[data-testid="notice-card-title"]` (test attribute). Never use exact class names like `._card_1oojq_1`
**Warning signs:** Scraper returns 0 notices when Doffin has been recently updated

### Pitfall 2: Not Waiting for React SPA to Render
**What goes wrong:** `page.GotoAsync()` returns but DOM is still empty `<div id="root"></div>`
**Why it happens:** React needs to execute, make API calls, and render
**How to avoid:** Use `WaitUntilState.NetworkIdle` in `GotoAsync()`, then `WaitForSelectorAsync("a[class*=card]")` before extracting data
**Warning signs:** Empty card lists, missing text content

### Pitfall 3: Playwright Browser Zombie Processes
**What goes wrong:** Chromium processes remain running after scraping completes
**Why it happens:** Not disposing `IPlaywright` and `IBrowser` properly
**How to avoid:** Use `using var playwright = ...` and `await using var browser = ...` in `ScrapeAsync()`. Create per-scrape, dispose after scrape.
**Warning signs:** Growing memory usage, multiple `chrome.exe` in Task Manager

### Pitfall 4: Chromium Not Installed at Runtime
**What goes wrong:** `Playwright.CreateAsync()` throws "Browser not found" error
**Why it happens:** `Microsoft.Playwright.Program.Main(new[] { "install", "chromium" })` was never run
**How to avoid:** Call install programmatically at app startup before first scrape, or as a documented post-build step. Browsers install to `%LOCALAPPDATA%\ms-playwright` (no admin rights needed).
**Warning signs:** Exception on first launch: `PlaywrightException: Executable doesn't exist`

### Pitfall 5: Date Parsing Format Mismatch
**What goes wrong:** Dates not parsed correctly, stored as empty strings
**Why it happens:** Doffin uses `DD.MM.YYYY` format in Norwegian locale, aria-label format is `"Kunngjøringsdato: 01.09.2025"`
**How to avoid:** Extract date with regex `Kunngjøringsdato: (\d{2}\.\d{2}\.\d{4})`, then parse with `DateOnly.TryParseExact(rawDate, "dd.MM.yyyy", ...)`
**Warning signs:** All `publishedDate` fields are empty

### Pitfall 6: Page Close Forgetting in Parallel Fetching
**What goes wrong:** Browser runs out of memory or crashes with many open tabs
**Why it happens:** Opening 106 detail pages without closing them
**How to avoid:** Always `await page.CloseAsync()` in a `finally` block after extracting data from each detail page
**Warning signs:** Chromium crash, `BrowserClosedException`, slow performance

### Pitfall 7: Lock Contention on _notices During Parallel Enrichment
**What goes wrong:** Current ReportService pattern replaces items during enrichment, but DoffinService adds items from list pages then enriches them
**Why it happens:** Two-phase approach (list scrape → detail enrichment) means updating existing items
**How to avoid:** After list pages are fully scraped, take a snapshot for enrichment, then update items in-place using `FindIndex` + assignment inside lock
**Warning signs:** Missing descriptions, duplicate entries

## Code Examples

### Registration and Endpoint Wiring (add to Program.cs)
```csharp
// Source: Pattern from existing Program.cs lines 10-33, adapted for DoffinService

// Add after line 17 (builder.Services.AddSingleton<ReportService>())
builder.Services.AddSingleton<DoffinService>();

// Add after line 23 (var svc = ... svc.LoadAsync())
var doffinSvc = app.Services.GetRequiredService<DoffinService>();
_ = doffinSvc.LoadAsync();

// Add after line 33 (existing endpoints, before app.Run())
app.MapGet("/api/notices", (DoffinService s) =>
    Results.Ok(new { loading = s.IsLoading, notices = s.Notices }));

app.MapPost("/api/notices/refresh", async (DoffinService s) =>
{
    _ = s.LoadAsync(forceRefresh: true);
    return Results.Accepted();
});
```

### List Page Scraping (core scraping loop)
```csharp
// Source: Verified against live Doffin.no 2026-04-07
private static async Task<(List<Notice> notices, bool hasNextPage)> ScrapeListPage(
    IBrowser browser, int pageNum)
{
    var page = await browser.NewPageAsync();
    try
    {
        var url = $"https://www.doffin.no/search?searchString=helfo&page={pageNum}";
        await page.GotoAsync(url, new() {
            WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30_000 });

        // Wait for cards to render (or timeout = empty page)
        try { await page.WaitForSelectorAsync("a[class*=card]", new() { Timeout = 10_000 }); }
        catch { return ([], false); }

        var cardElements = await page.QuerySelectorAllAsync("a[class*=card]");
        var notices = new List<Notice>();
        int id = (pageNum - 1) * 20 + 1;

        foreach (var card in cardElements)
        {
            var href = await card.GetAttributeAsync("href") ?? "";
            var buyerEl = await card.QuerySelectorAsync("p[class*=buyer]");
            var titleEl = await card.QuerySelectorAsync("[data-testid='notice-card-title']");
            var dateEl = await card.QuerySelectorAsync("p[class*=issue_date]");

            var buyer = buyerEl != null ? (await buyerEl.TextContentAsync())?.Trim() ?? "" : "";
            var title = titleEl != null ? (await titleEl.TextContentAsync())?.Trim() ?? "" : "";
            var dateLabel = dateEl != null ? await dateEl.GetAttributeAsync("aria-label") ?? "" : "";

            // Parse "Kunngjøringsdato: 01.09.2025" → "2025-09-01"
            var dateMatch = Regex.Match(dateLabel, @"(\d{2}\.\d{2}\.\d{4})");
            var pubDate = "";
            if (dateMatch.Success && DateOnly.TryParseExact(
                    dateMatch.Groups[1].Value, "dd.MM.yyyy", out var d))
                pubDate = d.ToString("yyyy-MM-dd");

            var noticeUrl = $"https://www.doffin.no{href}";
            notices.Add(new Notice(id++, title, buyer, pubDate, noticeUrl, ""));
        }

        // Check if there's a next page
        var nextBtn = await page.QuerySelectorAsync("[data-cy='Neste side']");
        var hasNext = nextBtn != null &&
            await nextBtn.GetAttributeAsync("disabled") == null;

        return (notices, hasNext);
    }
    finally { await page.CloseAsync(); }
}
```

### Chromium Browser Install (one-time setup)
```csharp
// Source: Verified with test project — installs to %LOCALAPPDATA%\ms-playwright
// Call this ONCE before first scrape (e.g., at the start of LoadAsync)
// Returns 0 on success. Browsers cached — subsequent calls are fast no-ops.
var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
if (exitCode != 0)
    Console.WriteLine($"[doffin] WARNING: Playwright browser install failed with code {exitCode}");
```

### Date Parsing Helper
```csharp
// Parse Doffin date from aria-label: "Kunngjøringsdato: 01.09.2025" → "2025-09-01"
static string ParseDoffinDate(string ariaLabel)
{
    var match = Regex.Match(ariaLabel, @"(\d{2}\.\d{2}\.\d{4})");
    if (match.Success && DateOnly.TryParseExact(
            match.Groups[1].Value, "dd.MM.yyyy", out var date))
        return date.ToString("yyyy-MM-dd");
    return "";
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Playwright.CLI` dotnet tool | `Microsoft.Playwright.Program.Main()` in code | Playwright 1.40+ | No need for separate CLI tool install |
| `playwright.ps1` script | Direct DLL execution or in-code install | Playwright 1.40+ | Simpler browser provisioning |
| `page.WaitForNavigationAsync` | `page.GotoAsync` with `WaitUntilState.NetworkIdle` | Best practice | Single call handles both navigation and waiting |

**Deprecated/outdated:**
- `Microsoft.Playwright.CLI` dotnet tool: Deprecated. Use `Microsoft.Playwright.Program.Main()` instead.
- `page.$` / `page.$$` syntax: .NET uses `QuerySelectorAsync` / `QuerySelectorAllAsync` (not the JS shorthand).
- `page.WaitForSelectorAsync` with `State = WaitForSelectorState.Attached`: Default state is `Visible`, which is correct for rendered content.

## Open Questions

1. **Should Playwright browser install happen at startup or be a manual step?**
   - What we know: `Microsoft.Playwright.Program.Main(new[] { "install", "chromium" })` works at runtime, returns 0 quickly if already installed
   - What's unclear: First-time install downloads ~150MB — could slow startup significantly
   - Recommendation: Call at start of first `ScrapeAsync()`. If browsers exist, it's a fast no-op. Document as a prerequisite for first run.

2. **What if Doffin.no changes their DOM structure?**
   - What we know: `data-testid` and `data-cy` attributes are the most stable selectors
   - What's unclear: Whether Doffin regularly changes these
   - Recommendation: Use `data-testid`/`data-cy` where available, `[class*=X]` as fallback. Log meaningful errors when selectors fail (0 cards found).

3. **Search term "helfo" — hardcoded or configurable?**
   - What we know: REQUIREMENTS.md specifies `searchString=helfo`. Out of scope (999.1) lists configurable search terms.
   - Recommendation: Hardcode `helfo` for MVP. Extract as a constant for easy future change.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 9 SDK | Build & run | ✓ | 9.0.306 | — |
| NuGet (nuget.org) | Package install | ✓ | Configured | — |
| Chromium (Playwright) | Headless scraping | ✓ | v1208 (auto-installed) | — |
| Internet access to doffin.no | Scraping target | ✓ | — | — |
| %LOCALAPPDATA% write access | Playwright browser cache | ✓ | — | — |
| C:\Temp\RrApi | Published output | ✓ | — | — |

**Missing dependencies with no fallback:** None

**Missing dependencies with fallback:** None

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | None detected (no test projects exist) |
| Config file | None |
| Quick run command | N/A — manual verification via HTTP requests |
| Full suite command | N/A |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-01 | GET /api/notices returns ≥10 notices with title, buyer, date, url | smoke | `curl http://localhost:5000/api/notices` + JSON inspection | ❌ Manual |
| REQ-02 | Each notice has description from detail page; cached to notices-cache.json | smoke | Check `notices-cache.json` exists in C:\Temp\RrApi after first load | ❌ Manual |

### Sampling Rate
- **Per task commit:** Publish → restart → `curl http://localhost:5000/api/notices` → verify JSON shape
- **Per wave merge:** Full scrape cycle → verify ≥10 notices with all fields populated
- **Phase gate:** `GET /api/notices` returns `{ loading: false, notices: [...] }` with ≥10 complete notices

### Wave 0 Gaps
- No test framework exists in this project — verification is manual HTTP testing
- This is consistent with the existing codebase (zero test infrastructure)
- Adding a test framework is out of scope for Phase 1

## Sources

### Primary (HIGH confidence)
- **Live Doffin.no DOM inspection** via Playwright headless Chromium — all selectors verified on 2026-04-07
- **Microsoft.Playwright 1.58.0 NuGet** — version verified via NuGet API, install and launch tested
- **Existing `Program.cs` (ReportService)** — exact patterns to mirror, read from source

### Secondary (MEDIUM confidence)
- **Playwright .NET API** — `QuerySelectorAsync`, `WaitForSelectorAsync`, `GotoAsync` with `WaitUntilState.NetworkIdle` verified by running actual code

### Tertiary (LOW confidence)
- None — all findings verified through direct testing

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Microsoft.Playwright 1.58.0 tested end-to-end with .NET 9
- Architecture: HIGH — Direct mirror of existing ReportService pattern in same codebase
- Selectors: HIGH — All selectors verified on live site with both Node.js and .NET Playwright
- Pitfalls: HIGH — Each pitfall identified from actual testing or known patterns

**Research date:** 2026-04-07
**Valid until:** 2026-05-07 (30 days — Doffin DOM may change; Playwright version stable)
