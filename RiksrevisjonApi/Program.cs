using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Playwright;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHttpClient("rr", c =>
{
    c.BaseAddress = new Uri("https://www.riksrevisjonen.no");
    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; RRDashboard/1.0)");
    c.Timeout = TimeSpan.FromSeconds(30);
}).AddStandardResilienceHandler(o =>
{
    o.Retry.MaxRetryAttempts = 3;
    o.Retry.Delay = TimeSpan.FromSeconds(1);
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
});

builder.Services.AddHttpClient("doffin-api", c =>
{
    c.BaseAddress = new Uri("https://api.doffin.no/webclient/api/v2/");
    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; RRDashboard/1.0)");
    c.DefaultRequestHeaders.Add("Accept", "application/json");
    c.Timeout = TimeSpan.FromSeconds(30);
}).AddStandardResilienceHandler(o =>
{
    o.Retry.MaxRetryAttempts = 3;
    o.Retry.Delay = TimeSpan.FromSeconds(1);
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
});

builder.Services.AddSingleton<ReportService>();
builder.Services.AddSingleton<DoffinService>();
builder.Services.AddSingleton<MatchService>();

var startedAt = DateTime.UtcNow;
var app = builder.Build();
app.UseCors();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
var svc = app.Services.GetRequiredService<ReportService>();
var doffinSvc = app.Services.GetRequiredService<DoffinService>();
var matchSvc = app.Services.GetRequiredService<MatchService>();

_ = Task.Run(async () =>
{
    // Reports and notices can load in parallel — no dependency
    var reportTask = Task.Run(async () =>
    {
        try
        {
            await svc.LoadAsync();
            logger.LogInformation("Reports loaded: {Count} items", svc.Reports.Count);
        }
        catch (Exception ex) { logger.LogCritical(ex, "ReportService startup failed"); }
    });

    var noticeTask = Task.Run(async () =>
    {
        try
        {
            await doffinSvc.LoadAsync();
            logger.LogInformation("Notices loaded: {Count} items", doffinSvc.Notices.Count);
        }
        catch (Exception ex) { logger.LogCritical(ex, "DoffinService startup failed"); }
    });

    await Task.WhenAll(reportTask, noticeTask);

    try
    {
        await matchSvc.LoadAsync();
        logger.LogInformation("Matches loaded: {Count} items", matchSvc.Matches.Count);
    }
    catch (Exception ex) { logger.LogCritical(ex, "MatchService startup failed"); }
});

app.MapGet("/api/reports", (ReportService s) =>
    Results.Ok(new { loading = s.IsLoading, reports = s.Reports }));

// Force a fresh scrape (ignores cache)
app.MapPost("/api/reports/refresh", (ReportService s) =>
{
    _ = s.LoadAsync(forceRefresh: true);
    return Results.Accepted();
});

app.MapGet("/api/notices", (DoffinService s) =>
    Results.Ok(new { loading = s.IsLoading, notices = s.Notices }));

app.MapPost("/api/notices/refresh", (DoffinService s) =>
{
    _ = s.LoadAsync(forceRefresh: true);
    return Results.Accepted();
});

app.MapGet("/api/matches", (MatchService s) =>
    Results.Ok(new { loading = s.IsLoading, matches = s.Matches }));

app.MapPost("/api/matches/refresh", (
    MatchService ms, ReportService rs, DoffinService ds) =>
{
    _ = Task.Run(async () =>
    {
        await rs.LoadAsync(forceRefresh: true);
        await ds.LoadAsync(forceRefresh: true);
        await ms.LoadAsync(forceRefresh: true);
    });
    return Results.Accepted();
});

app.MapGet("/api/debug/doffin-test", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("doffin-api");
    // Test 1: search API
    var body = System.Text.Json.JsonSerializer.Serialize(new {
        numHitsPerPage = 3, page = 1, searchString = "IKT", sortBy = "RELEVANCE",
        facets = new {
            type = new { checkedItems = new[] { "COMPETITION", "PLANNING" } },
            contractNature = new { checkedItems = Array.Empty<string>() },
            cpvCodesLabel = new { checkedItems = Array.Empty<string>() },
            cpvCodesId = new { checkedItems = Array.Empty<string>() },
            status = new { checkedItems = Array.Empty<string>() },
            publicationDate = new { from = "2024-01-01", to = (string?)null },
            location = new { checkedItems = Array.Empty<string>() },
            buyer = new { checkedItems = Array.Empty<string>() },
            winner = new { checkedItems = Array.Empty<string>() }
        }
    });
    try {
        var resp = await client.PostAsync("search-api/search",
            new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        var raw = await resp.Content.ReadAsStringAsync();
        // Test 2: IsServiceContract for known Tjenester notice
        string isTjenester = "not tested";
        try {
            var detailJson = await client.GetStringAsync("notices-api/notices/2026-103994");
            using var doc = System.Text.Json.JsonDocument.Parse(detailJson);
            if (doc.RootElement.TryGetProperty("eform", out var ef))
                isTjenester = DoffinService.IsServiceContract(ef) ? "YES" : "NO";
            else isTjenester = "no eform property";
        } catch (Exception ex) { isTjenester = $"error: {ex.Message}"; }
        return Results.Ok(new { status = (int)resp.StatusCode, isServiceContract_2026_103994 = isTjenester, preview = raw[..Math.Min(300, raw.Length)] });
    } catch (Exception ex) {
        return Results.Ok(new { error = ex.Message });
    }
});

app.MapGet("/api/health", (ReportService rs, DoffinService ds, MatchService ms) =>
    Results.Ok(new
    {
        status = rs.IsLoading || ds.IsLoading || ms.IsLoading ? "loading" : "ready",
        uptime = (DateTime.UtcNow - startedAt).ToString(@"d\.hh\:mm\:ss"),
        services = new
        {
            reports = new { count = rs.Reports.Count, loading = rs.IsLoading, lastError = rs.LastError, lastLoadedAt = rs.LastLoadedAt },
            notices = new { count = ds.Notices.Count, loading = ds.IsLoading, lastError = ds.LastError, lastLoadedAt = ds.LastLoadedAt },
            matches = new { count = ms.Matches.Count, loading = ms.IsLoading, lastError = ms.LastError, lastLoadedAt = ms.LastLoadedAt }
        }
    }));

app.Run();

// ─── Service ──────────────────────────────────────────────────────────────────

class ReportService(IHttpClientFactory factory, ILogger<ReportService> log)
{
    private static readonly string CacheFile = Path.Combine(
        AppContext.BaseDirectory, "reports-cache.json");
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static readonly string[] SearchTerms = ["riksrevisjonen"];

    private List<Report> _reports = [];
    public bool IsLoading { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastLoadedAt { get; private set; }
    public IReadOnlyList<Report> Reports { get { lock (_reports) return _reports.ToList(); } }

    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        LastError = null;
        try
        {
            // Serve from cache if fresh enough
            if (!forceRefresh && TryLoadCache(out var cached))
            {
                lock (_reports) { _reports.Clear(); _reports.AddRange(cached!); }
                log.LogInformation("Loaded {Count} reports from cache", cached!.Count);
                LastLoadedAt = DateTime.UtcNow;
                return;
            }

            await ScrapeAsync();
            SaveCache();
            LastLoadedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            throw;
        }
        finally { IsLoading = false; }
    }

    // ── Cache ────────────────────────────────────────────────────────────────

    private bool TryLoadCache(out List<Report>? reports)
    {
        reports = null;
        if (!File.Exists(CacheFile)) return false;
        if (File.GetLastWriteTime(CacheFile) < DateTime.Now - CacheMaxAge) return false;
        try
        {
            var json = File.ReadAllText(CacheFile);
            reports = JsonSerializer.Deserialize<List<Report>>(json, JsonOpts);
            return reports?.Count > 0;
        }
        catch { return false; }
    }

    private void SaveCache()
    {
        try
        {
            var json = JsonSerializer.Serialize(Reports, JsonOpts);
            File.WriteAllText(CacheFile, json);
            log.LogInformation("Saved {Count} reports to cache", Reports.Count);
        }
        catch (Exception ex) { log.LogError(ex, "Cache save failed"); }
    }

    // ── Scrape ───────────────────────────────────────────────────────────────

    private async Task ScrapeAsync()
    {
        lock (_reports) _reports.Clear();

        var client = factory.CreateClient("rr");
        var allReports = new List<Report>();

        // Scrape one paginated search per term — sequentially
        foreach (var term in SearchTerms)
        {
            log.LogInformation("Scraping term: {Term}", term);
            int page = 1;
            while (true)
            {
                var batch = await FetchSearchPage(client, term, page);
                if (batch.Count == 0) break;
                allReports.AddRange(batch);
                page++;
            }
            log.LogInformation("Done for term {Term}", term);
        }
        log.LogInformation("Total before dedup: {Count}", allReports.Count);

        // Deduplicate by URL, re-assign sequential IDs
        var deduped = allReports
            .Where(r => !string.IsNullOrEmpty(r.Url))
            .GroupBy(r => r.Url)
            .Select(g => g.First())
            .Select((r, i) => r with { Id = i + 1 })
            .ToList();
        log.LogInformation("After dedup: {Count} unique reports", deduped.Count);

        // Enrich with severity in parallel
        log.LogInformation("Enriching {Count} reports", deduped.Count);
        var sem = new SemaphoreSlim(8);
        var enrichTasks = deduped.Select(async item =>
        {
            await sem.WaitAsync();
            try { return await Enrich(client, item); }
            catch { return item; }
            finally { sem.Release(); }
        });
        var enriched = await Task.WhenAll(enrichTasks);

        lock (_reports)
        {
            _reports.Clear();
            _reports.AddRange(enriched);
        }
        log.LogInformation("Done — {Count} reports loaded", _reports.Count);
    }

    static async Task<List<Report>> FetchSearchPage(HttpClient client, string term, int page)
    {
        try
        {
            var encoded = Uri.EscapeDataString(term);
            var html = await client.GetStringAsync($"/sok/?t=1&q={encoded}&p={page}");
            return ParseSearchPage(html);
        }
        catch { return []; }
    }

    /// <summary>
    /// Parses the /sok/?t=1 search results page HTML.
    /// Each result is an &lt;article&gt; containing rr-search-result-link, &lt;h3&gt;, &lt;time&gt;, and blockquote.
    /// </summary>
    internal static List<Report> ParseSearchPage(string html)
    {
        var results = new List<Report>();
        var articleRx = new Regex(
            @"<article[^>]*>(.*?)</article>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var linkRx = new Regex(
            @"<a\s+class=""rr-search-result-link""\s+href=""(/rapporter-mappe/[^""]+)""",
            RegexOptions.IgnoreCase);
        var titleRx = new Regex(
            @"<h3[^>]*>\s*([^<]+?)\s*</h3>",
            RegexOptions.IgnoreCase);
        var dateRx = new Regex(
            @"datetime=""(\d{2}\.\d{2}\.\d{4})",
            RegexOptions.IgnoreCase);
        var summaryRx = new Regex(
            @"rr-search-result__excerpt[^>]*>\s*(.*?)\s*</p>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var catRx = new Regex(
            @"rr-search-result-meta[^>]*>\s*([^<]+?)\s*</div>",
            RegexOptions.IgnoreCase);

        int id = 1;
        foreach (System.Text.RegularExpressions.Match m in articleRx.Matches(html))
        {
            var block = m.Groups[1].Value;

            var href = linkRx.Match(block).Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(href)) continue;

            var title = Decode(titleRx.Match(block).Groups[1].Value.Trim());
            if (string.IsNullOrWhiteSpace(title)) continue;

            var rawDate = dateRx.Match(block).Groups[1].Value.Trim();
            DateOnly.TryParseExact(rawDate, "dd.MM.yyyy", out var date);

            var rawSummary = summaryRx.Match(block).Groups[1].Value.Trim();
            var summary = Decode(StripTags(rawSummary));

            // Extract all category tags and join them
            var cats = catRx.Matches(block)
                .Select(c => Regex.Replace(Decode(c.Groups[1].Value.Trim().TrimEnd('·').Trim()), @"\s+", " "))
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();
            var department = cats.Count > 0 ? string.Join(" / ", cats) : "";

            results.Add(new Report(
                id++, title, summary, "Ingen karakter",
                date == default ? "" : date.ToString("yyyy-MM-dd"),
                department,
                "https://www.riksrevisjonen.no" + href));
        }
        return results;
    }

    internal static List<Report> ParseListPage(string html, int page)
    {
        var results = new List<Report>();
        var blockRx = new Regex(
            @"<a\s+class=""rr-link-wrapper""\s+href=""(/rapporter-mappe/[^""]+)"">(.*?)</a>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var dateRx = new Regex(@"<time[^>]*>([^<]+)</time>", RegexOptions.IgnoreCase);
        var titleRx = new Regex(@"<h2[^>]*>\s*([^<]+?)\s*</h2>", RegexOptions.IgnoreCase);
        var summaryRx = new Regex(@"<blockquote[^>]*>.*?<p[^>]*>\s*(.*?)\s*(?:</p>|\.\.\.)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var catRx = new Regex(@"rr-search-result-meta[^>]*>\s*([^<]+?)\s*</div>", RegexOptions.IgnoreCase);

        int id = (page - 1) * 20 + 1;
        foreach (System.Text.RegularExpressions.Match m in blockRx.Matches(html))
        {
            var href = m.Groups[1].Value.Trim();
            var block = m.Groups[2].Value;

            var rawDate = dateRx.Match(block).Groups[1].Value.Trim();
            var title = Decode(titleRx.Match(block).Groups[1].Value.Trim());
            var summary = Decode(StripTags(summaryRx.Match(block).Groups[1].Value.Trim()));
            var cat = Decode(catRx.Match(block).Groups[1].Value.Trim());

            if (string.IsNullOrWhiteSpace(title)) continue;

            DateOnly.TryParseExact(rawDate, "dd.MM.yyyy", out var date);
            results.Add(new Report(
                id++, title, summary, "Ingen karakter",
                date == default ? "" : date.ToString("yyyy-MM-dd"),
                cat, "https://www.riksrevisjonen.no" + href));
        }
        return results;
    }

    static async Task<Report> Enrich(HttpClient client, Report r)
    {
        var path = new Uri(r.Url).AbsolutePath;
        var html = await client.GetStringAsync(path);
        return r with { Severity = ExtractSeverity(html) };
    }

    static string ExtractSeverity(string html)
    {
        // The selected severity label has class rr-font--space-mono-bold
        var labelRx = new Regex(
            @"rr-font--space-mono-bold[^>]*>\s*<div>\s*([^<]+?)\s*</div>",
            RegexOptions.IgnoreCase);
        var lm = labelRx.Match(html);
        if (lm.Success)
        {
            var label = lm.Groups[1].Value.Trim().ToLowerInvariant();
            if (label.Contains("sterkt kritikkverdig")) return "Sterkt kritikkverdig";
            if (label.Contains("kritikkverdig")) return "Kritikkverdig";
            if (label.Contains("ikke tilfredsstillende")) return "Ikke tilfredsstillende";
        }

        // Fallback: <strong> tags
        var strongRx = new Regex(@"<strong[^>]*>(.*?)</strong>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (System.Text.RegularExpressions.Match sm in strongRx.Matches(html))
        {
            var t = sm.Groups[1].Value.ToLowerInvariant().Trim().TrimEnd('.');
            if (t.Contains("sterkt kritikkverdig")) return "Sterkt kritikkverdig";
            if (t.Contains("kritikkverdig")) return "Kritikkverdig";
            if (t.Contains("ikke tilfredsstillende")) return "Ikke tilfredsstillende";
        }

        return "Ingen karakter";
    }

    static string Decode(string s) => WebUtility.HtmlDecode(s);
    static string StripTags(string s) => Regex.Replace(s, @"<[^>]+>", "");
}

record Report(int Id, string Title, string Summary, string Severity,
              string PublishedDate, string Department, string Url);

record Notice(int Id, string Title, string Buyer, string PublishedDate,
              string Url, string Description);

record Match(int ReportId, int NoticeId, double Score,
             string[] MatchedKeywords, string? MatchedOrg, string[]? MatchedBigrams = null,
             string[]? MatchedTitleWords = null);

// ─── Cache + Doffin API records ───────────────────────────────────────────────

record CacheEnvelope<T>(string Version, List<T> Data);

// Doffin REST API response DTOs (api.doffin.no/webclient/api/v2/search-api/search)
// Deserialised with PropertyNameCaseInsensitive = true (see DoffinApiOpts in DoffinService)
record DoffinSearchResult(int NumHitsTotal, int NumHitsAccessible, List<DoffinHit> Hits);
record DoffinHit(string Id, string Heading, List<DoffinBuyer> Buyer, string Description, string PublicationDate);
record DoffinBuyer(string Id, string Name);

// ─── DoffinService ────────────────────────────────────────────────────────────

class DoffinService(IHttpClientFactory factory, ILogger<DoffinService> log)
{
    private static readonly string CacheFile = Path.Combine(
        AppContext.BaseDirectory, "notices-cache.json");
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static readonly string[] SearchTerms = [""];  // empty = all notices

    private static readonly string SearchVersion = "all-tjenester-v1|COMPETITION,PLANNING|contractNature:SERVICES";

    // Paths to the other two cache files — needed for atomic version-mismatch cleanup
    private static readonly string ReportsCacheFile = Path.Combine(
        AppContext.BaseDirectory, "reports-cache.json");
    private static readonly string MatchesCacheFile = Path.Combine(
        AppContext.BaseDirectory, "matches-cache.json");
    private static readonly JsonSerializerOptions DoffinApiOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private List<Notice> _notices = [];
    public bool IsLoading { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastLoadedAt { get; private set; }
    public IReadOnlyList<Notice> Notices { get { lock (_notices) return _notices.ToList(); } }

    /// Extract rare words (≥8 chars, no stopwords) from report titles —
    /// used as additional Doffin search terms to find report-specific procurements.
    /// Keeps only terms appearing in ≤2 report titles (truly distinctive), capped at 20.
    internal static List<string> ExtractEntityTerms(IReadOnlyList<Report> reports)
    {
        var termFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in reports)
        {
            if (string.IsNullOrWhiteSpace(r.Title)) continue;
            var words = MatchService.TokenRegex
                .Split(r.Title.ToLowerInvariant())
                .Where(w => w.Length >= 8 && !MatchService.Stopwords.Contains(w))
                .Distinct();
            foreach (var w in words)
                termFreq[w] = termFreq.GetValueOrDefault(w, 0) + 1;
        }
        // Keep only rare terms (appear in ≤2 reports) — most likely proper nouns / entities
        return termFreq
            .Where(kv => kv.Value <= 2)
            .OrderBy(kv => kv.Value)
            .ThenByDescending(kv => kv.Key.Length)
            .Select(kv => kv.Key)
            .Take(20)
            .ToList();
    }

    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        LastError = null;
        try
        {
            // Version mismatch check: if cache file exists but has a stale version,
            // purge all three caches before loading to prevent cross-cache ID mismatches.
            if (!forceRefresh && CheckVersionMismatch(CacheFile, SearchVersion))
            {
                log.LogWarning("Cache version mismatch — purging all three caches");
                PurgeAllCaches(CacheFile, ReportsCacheFile, MatchesCacheFile);
            }

            if (!forceRefresh && TryLoadCache(out var cached))
            {
                lock (_notices) { _notices.Clear(); _notices.AddRange(cached!); }
                log.LogInformation("Loaded {Count} notices from cache", cached!.Count);
                LastLoadedAt = DateTime.UtcNow;
                return;
            }

            await ScrapeAsync();
            SaveCache();
            LastLoadedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            throw;
        }
        finally { IsLoading = false; }
    }

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

    private void SaveCache()
    {
        try
        {
            var envelope = new CacheEnvelope<Notice>(SearchVersion, Notices.ToList());
            var json = JsonSerializer.Serialize(envelope, JsonOpts);
            File.WriteAllText(CacheFile, json);
            log.LogInformation("Saved {Count} notices to cache", Notices.Count);
        }
        catch (Exception ex) { log.LogError(ex, "Cache save failed"); }
    }

    /// <summary>
    /// Returns true if the notice eForm JSON contains a "Kontraktens art" = "Tjenester" entry
    /// (new Norwegian eForm format, 2023+) OR "Nature of the contract" = "Services" (old format).
    /// Recurses through: blocks → sections (level 1) → sections (level 2) → sections (level 3/leaf).
    /// </summary>
    internal static bool IsServiceContract(System.Text.Json.JsonElement eform)
    {
        if (eform.ValueKind != System.Text.Json.JsonValueKind.Array) return false;
        foreach (var block in eform.EnumerateArray())
        {
            if (!block.TryGetProperty("sections", out var lvl1) ||
                lvl1.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
            foreach (var s1 in lvl1.EnumerateArray())
            {
                if (!s1.TryGetProperty("sections", out var lvl2) ||
                    lvl2.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                foreach (var s2 in lvl2.EnumerateArray())
                {
                    if (!s2.TryGetProperty("sections", out var lvl3) ||
                        lvl3.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                    foreach (var leaf in lvl3.EnumerateArray())
                    {
                        var label = leaf.TryGetProperty("label", out var lv) ? lv.GetString() ?? "" : "";
                        var value = leaf.TryGetProperty("value", out var vv) ? vv.GetString() ?? "" : "";
                        if ((label == "Kontraktens art" && value == "Tjenester") ||
                            (label == "Nature of the contract" && value == "Services"))
                            return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Merges notices from multiple search terms, deduplicates by URL (exact string match),
    /// skips notices with empty/null URL to avoid false collisions, and re-assigns sequential IDs
    /// starting at 1. Keeps the first occurrence when a URL appears more than once.
    /// </summary>
    internal static List<Notice> DeduplicateNotices(List<Notice> all)
    {
        return all
            .Where(n => !string.IsNullOrEmpty(n.Url))
            .GroupBy(n => n.Url)
            .Select(g => g.First())
            .Select((n, i) => n with { Id = i + 1 })
            .ToList();
    }

    /// <summary>
    /// Returns true if the cache file exists but its version envelope does not match expectedVersion.
    /// Returns false if the file doesn't exist (no mismatch — just absent).
    /// </summary>
    internal static bool CheckVersionMismatch(string cacheFile, string expectedVersion)
    {
        if (!File.Exists(cacheFile)) return false;
        try
        {
            var json = File.ReadAllText(cacheFile);
            var env = JsonSerializer.Deserialize<CacheEnvelope<Notice>>(json);
            return env?.Version != expectedVersion;
        }
        catch { return true; }  // corrupt → treat as mismatch
    }

    /// <summary>
    /// Deletes the three cache files atomically. Used when version mismatch is detected.
    /// </summary>
    internal static void PurgeAllCaches(string noticesFile, string reportsFile, string matchesFile)
    {
        foreach (var f in new[] { noticesFile, reportsFile, matchesFile })
            if (File.Exists(f)) File.Delete(f);
    }

    private async Task ScrapeAsync()
    {
        lock (_notices) _notices.Clear();

        var apiClient = factory.CreateClient("doffin-api");
        var allNotices = new List<Notice>();

        // Fetch Tjenester notices year by year to work around the ~1000 result API cap
        var years = Enumerable.Range(2022, DateTime.Now.Year - 2022 + 1);
        foreach (var year in years)
        {
            log.LogInformation("Scraping Tjenester for {Year}", year);
            try
            {
                var yearNotices = await ScrapeYearAsync(apiClient, year);
                log.LogInformation("{Count} notices for {Year}", yearNotices.Count, year);
                allNotices.AddRange(yearNotices);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Error for {Year}", year);
            }
        }
        log.LogInformation("Total before dedup: {Count}", allNotices.Count);

        // Deduplicate by URL — keep first occurrence, re-assign sequential IDs
        var deduped = DeduplicateNotices(allNotices);

        lock (_notices) { _notices.Clear(); _notices.AddRange(deduped); }
        log.LogInformation("Done — {Count} Tjenester notices loaded", _notices.Count);
    }

    private static readonly int MaxPagesPerTerm = 250;  // 250 × 100 = 25000 notices max

    /// <summary>
    /// Fetches all Tjenester notices for a given year. Paginates until 400 or empty.
    /// </summary>
    private async Task<List<Notice>> ScrapeYearAsync(HttpClient apiClient, int year)
    {
        var notices = new List<Notice>();
        int page = 1;
        int globalId = 1;

        while (true)
        {
            var requestBody = new
            {
                numHitsPerPage = 100,
                page,
                searchString = "",
                sortBy = "RELEVANCE",
                facets = new
                {
                    type           = new { checkedItems = new[] { "COMPETITION", "PLANNING" } },
                    contractNature = new { checkedItems = new[] { "SERVICES" } },
                    cpvCodesLabel  = new { checkedItems = Array.Empty<string>() },
                    cpvCodesId     = new { checkedItems = Array.Empty<string>() },
                    status         = new { checkedItems = Array.Empty<string>() },
                    publicationDate= new { from = $"{year}-01-01", to = $"{year}-12-31" },
                    location       = new { checkedItems = Array.Empty<string>() },
                    buyer          = new { checkedItems = Array.Empty<string>() },
                    winner         = new { checkedItems = Array.Empty<string>() }
                }
            };

            var body    = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var resp    = await apiClient.PostAsync("search-api/search", content);

            // Doffin API returns 400 when page exceeds its internal limit (~10 pages)
            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("Page {Page} returned {StatusCode} — stopping pagination", page, (int)resp.StatusCode);
                break;
            }

            var result = System.Text.Json.JsonSerializer.Deserialize<DoffinSearchResult>(
                await resp.Content.ReadAsStringAsync(), DoffinApiOpts);

            if (result == null || result.Hits.Count == 0 || page > MaxPagesPerTerm) break;

            log.LogInformation("Page {Page} — {HitCount} hits (total: {TotalHits})", page, result.Hits.Count, result.NumHitsTotal);

            foreach (var hit in result.Hits)
            {
                var buyer  = hit.Buyer?.FirstOrDefault()?.Name ?? "";
                var url    = $"https://www.doffin.no/notices/{hit.Id}";
                notices.Add(new Notice(globalId++, hit.Heading, buyer, hit.PublicationDate, url, hit.Description ?? ""));
            }

            page++;
        }

        return notices;
    }

    /// <summary>
    /// Fetches the notice detail JSON from api.doffin.no and checks if it is a Tjenester contract.
    /// Extracts the Doffin notice ID from the www.doffin.no URL:
    ///   https://www.doffin.no/notices/2026-103994  →  id = "2026-103994"
    /// Then calls GET https://api.doffin.no/webclient/api/v2/notices-api/notices/{id}
    /// Returns false if the detail call fails or eform is absent.
    /// </summary>
    private static async Task<bool> FetchAndCheckTjenester(HttpClient apiClient, string noticeUrl)
    {
        // Extract Doffin notice ID from the www.doffin.no URL
        var lastSlash = noticeUrl.LastIndexOf('/');
        if (lastSlash < 0) return false;
        var noticeId = noticeUrl[(lastSlash + 1)..];
        if (string.IsNullOrWhiteSpace(noticeId)) return false;

        // Call the REST API detail endpoint — NOT www.doffin.no (that returns SPA shell)
        var detailJson = await apiClient.GetStringAsync($"notices-api/notices/{noticeId}");
        using var doc  = System.Text.Json.JsonDocument.Parse(detailJson);

        if (!doc.RootElement.TryGetProperty("eform", out var eform)) return false;
        return IsServiceContract(eform);
    }
}

// ─── MatchService ─────────────────────────────────────────────────────────────

class MatchService(ReportService reportSvc, DoffinService doffinSvc, ILogger<MatchService> log)
{
    private List<Match> _matches = [];
    public bool IsLoading { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastLoadedAt { get; private set; }
    public IReadOnlyList<Match> Matches { get { lock (_matches) return _matches.ToList(); } }

    private static readonly string CacheFile = Path.Combine(
        AppContext.BaseDirectory, "matches-cache.json");
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        LastError = null;
        try
        {
            if (!forceRefresh && TryLoadCache(out var cached))
            {
                lock (_matches) { _matches.Clear(); _matches.AddRange(cached!); }
                log.LogInformation("Loaded {Count} matches from cache", cached!.Count);
                LastLoadedAt = DateTime.UtcNow;
                return;
            }

            // Wait for upstream services to finish their own loading
            while (reportSvc.IsLoading || doffinSvc.IsLoading)
                await Task.Delay(100);

            RunComputeMatches();
            SaveCache();
            LastLoadedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            throw;
        }
        finally { IsLoading = false; }
    }

    private bool TryLoadCache(out List<Match>? matches)
    {
        matches = null;
        if (!File.Exists(CacheFile)) return false;
        if (File.GetLastWriteTime(CacheFile) < DateTime.Now - CacheMaxAge) return false;
        try
        {
            var json = File.ReadAllText(CacheFile);
            matches = JsonSerializer.Deserialize<List<Match>>(json, JsonOpts);
            return matches?.Count > 0;
        }
        catch { return false; }
    }

    private void SaveCache()
    {
        try
        {
            var json = JsonSerializer.Serialize(Matches, JsonOpts);
            File.WriteAllText(CacheFile, json);
            log.LogInformation("Saved {Count} matches to cache", Matches.Count);
        }
        catch (Exception ex) { log.LogError(ex, "Cache save failed"); }
    }

    // ── Scoring Algorithm (REQ-03 + REQ-04) ─────────────────────────────────────

    /// Pure-function overload — testable without real services (InternalsVisibleTo).
    internal static List<Match> ComputeMatches(
        IReadOnlyList<Report> reports,
        IReadOnlyList<Notice> notices)
    {
        var results = new List<Match>();
        var idf = ComputeIDF(notices);

        foreach (var report in reports)
        {
            var keywords = ExtractKeywords(report.Title + " " + report.Summary);
            if (keywords.Count == 0) continue;

            var bigrams    = ExtractBigrams(report.Title);
            var titleWords = ExtractTitleWords(report.Title);
            var normDept   = NormalizeDepartment(report.Department);
            DateOnly.TryParse(report.PublishedDate, out var reportDate);

            foreach (var notice in notices)
            {
                var (keyScore, matchedKw) = ComputeKeywordScore(keywords, notice, idf);
                var (orgScore, matchedOrg) = ComputeOrgScore(normDept, notice);
                var (titleScore, matchedTw) = ComputeTitleWordScore(titleWords, notice);

                // Gate: require at least one signal — org, keyword, or title-word match
                if (orgScore == 0 && keyScore < 40 && titleScore == 0) continue;

                var (bigramScore, matchedBg) = ComputeBigramScore(bigrams, notice);

                // Bonus when notice is published after the report (procurement follows audit)
                double dateFactor = 1.0;
                if (reportDate != default &&
                    DateOnly.TryParse(notice.PublishedDate, out var noticeDate) &&
                    noticeDate >= reportDate)
                    dateFactor = 1.15;

                var combined = (keyScore * 0.40 + bigramScore * 0.15 + orgScore * 0.25 + titleScore * 0.20) * dateFactor;

                if (combined > 35)
                    results.Add(new Match(
                        report.Id, notice.Id,
                        Math.Round(combined, 1),
                        matchedKw.ToArray(),
                        matchedOrg,
                        matchedBg.Count > 0 ? matchedBg.ToArray() : null,
                        matchedTw.Count > 0 ? matchedTw.ToArray() : null));
            }
        }

        return results;
    }

    /// Compute inverse document frequency over the notices corpus.
    /// Rare, domain-specific terms get higher weight than common words.
    internal static Dictionary<string, double> ComputeIDF(IReadOnlyList<Notice> notices)
    {
        var df = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var notice in notices)
        {
            var tokens = TokenRegex
                .Split((notice.Title + " " + notice.Description).ToLowerInvariant())
                .Where(w => w.Length >= 3 && !Stopwords.Contains(w))
                .Select(Stem)
                .Distinct();
            foreach (var w in tokens)
                df[w] = df.GetValueOrDefault(w, 0) + 1;
        }
        int n = notices.Count;
        return df.ToDictionary(
            kv => kv.Key,
            kv => Math.Log((1.0 + n) / (1.0 + kv.Value)) + 1.0,
            StringComparer.OrdinalIgnoreCase);
    }

    /// Extract adjacent-word bigrams from a report title (no stopwords).
    internal static List<string> ExtractBigrams(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return [];
        var words = TokenRegex
            .Split(title.ToLowerInvariant())
            .Where(w => w.Length >= 3 && !Stopwords.Contains(w))
            .ToList();
        var bigrams = new List<string>();
        for (int i = 0; i < words.Count - 1; i++)
            bigrams.Add(words[i] + " " + words[i + 1]);
        return bigrams;
    }

    /// Score how many bigrams from the report title appear verbatim in the notice.
    internal static (double score, List<string> matched) ComputeBigramScore(
        List<string> bigrams, Notice notice)
    {
        if (bigrams.Count == 0) return (0.0, []);
        var noticeText = (notice.Title + " " + notice.Description).ToLowerInvariant();
        var matched = bigrams.Where(bg => noticeText.Contains(bg)).ToList();
        return ((matched.Count / (double)bigrams.Count) * 100.0, matched);
    }

    /// Simplified Norwegian suffix stemmer (Snowball-inspired).
    internal static string Stem(string word)
    {
        string[] suffixes = [
            "igheter", "ighet", "inger", "elsen", "elser", "else",
            "ingen", "eten", "eter", "ene", "ing", "lig", "het", "er", "en", "es"
        ];
        foreach (var suffix in suffixes)
            if (word.Length > suffix.Length + 3 && word.EndsWith(suffix))
                return word[..^suffix.Length];
        return word;
    }

    private void RunComputeMatches()
    {
        var results = ComputeMatches(reportSvc.Reports, doffinSvc.Notices);
        lock (_matches) { _matches.Clear(); _matches.AddRange(results); }
        log.LogInformation("Computed {Count} matches ({ReportCount} reports x {NoticeCount} notices)",
            results.Count, reportSvc.Reports.Count, doffinSvc.Notices.Count);
    }

    internal static (double score, List<string> matched) ComputeKeywordScore(
        List<string> keywords, Notice notice, Dictionary<string, double>? idf = null)
    {
        if (keywords.Count == 0) return (0.0, []);

        var stemmedNoticeTokens = new HashSet<string>(
            TokenRegex
                .Split((notice.Title + " " + notice.Description).ToLowerInvariant())
                .Where(w => w.Length >= 3)
                .Select(Stem),
            StringComparer.OrdinalIgnoreCase);

        var matched = keywords.Where(kw => stemmedNoticeTokens.Contains(Stem(kw))).ToList();

        // Require at least 3 keyword hits to avoid single-word false positives
        if (matched.Count < 3) return (0.0, matched);

        var idfMap = idf ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double totalWeight   = keywords.Sum(kw => idfMap.GetValueOrDefault(Stem(kw), 1.0));
        double matchedWeight = matched.Sum(kw => idfMap.GetValueOrDefault(Stem(kw), 1.0));
        double score = totalWeight > 0 ? (matchedWeight / totalWeight) * 100.0 : 0.0;

        return (score, matched);
    }

    // ── Org-Name Normalisation (REQ-04) ────────────────────────────────────────

    internal static string NormalizeDepartment(string? department)
    {
        if (string.IsNullOrWhiteSpace(department)) return "";

        // Strip trailing " ·" (e.g. "Helse ·" → "Helse", "Forsvar/sikkerhet/beredskap ·" → "Forsvar/...")
        var cleaned = department.TrimEnd().TrimEnd('·').Trim();
        if (string.IsNullOrEmpty(cleaned)) return "";

        // Take the first segment before "/" (e.g. "Forsvar/sikkerhet/beredskap" → "Forsvar")
        var firstSegment = cleaned.Split('/')[0].Trim();

        // Take the first word (e.g. "Offentlig forvaltning" → "offentlig", "Statens eierstyring" → "statens")
        var firstWord = firstSegment.Split(' ')[0].Trim();

        return firstWord.ToLowerInvariant();
    }

    internal static (double score, string? matchedOrg) ComputeOrgScore(
        string normDept, Notice notice)
    {
        if (string.IsNullOrEmpty(normDept)) return (0.0, null);

        if (notice.Buyer.ToLowerInvariant().Contains(normDept))
            return (100.0, normDept);

        return (0.0, null);
    }

    // ── Keyword Extraction (REQ-03) ─────────────────────────────────────────

    /// Extract significant words (≥6 chars, no stopwords) from a report title
    /// for title-to-title matching against notice titles.
    internal static List<string> ExtractTitleWords(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return [];
        return TokenRegex
            .Split(title.ToLowerInvariant())
            .Where(w => w.Length >= 6 && !Stopwords.Contains(w))
            .Distinct()
            .ToList();
    }

    /// Score how many title words from the report appear verbatim in the notice title.
    /// Title-to-title match is a strong signal — a single hit is valuable.
    internal static (double score, List<string> matched) ComputeTitleWordScore(
        List<string> titleWords, Notice notice)
    {
        if (titleWords.Count == 0) return (0.0, []);
        var noticeTitleLower = notice.Title.ToLowerInvariant();
        var matched = titleWords.Where(tw => noticeTitleLower.Contains(tw)).ToList();
        if (matched.Count == 0) return (0.0, []);
        return ((matched.Count / (double)titleWords.Count) * 100.0, matched);
    }

    internal static readonly HashSet<string> Stopwords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "og", "i", "på", "er", "til", "av", "som", "med", "at", "har", "de", "vi",
            "men", "fra", "dem", "seg", "sin", "ikke", "han", "hun", "det", "den",
            "en", "et", "for", "over", "under", "ut", "inn", "om", "når", "hvor",
            "hvem", "hva", "alle", "noen", "ingen", "andre", "disse", "dette", "da",
            "så", "her", "der", "nå", "var", "vil", "kan", "skal", "bør", "må",
            "også", "samt", "eller", "etter", "før", "uten", "mot", "hos", "ved",
            "jo", "ja", "nei", "alt", "mange", "mye", "lite", "ble", "blitt", "blir",
            "vært", "være", "sa", "si", "sier", "sitt", "sine", "hans",
            "hennes", "deres", "vår", "vårt", "våre", "din", "ditt", "dine",
            "fordi", "siden", "enten", "verken", "hverken", "både", "mens", "enn",
            "dog", "likevel", "imidlertid", "dermed", "altså", "dessuten",
            "riksrevisjonens", "riksrevisjonen", "rapport", "undersøkelse",
            "dokument", "bilag", "stortinget", "norsk", "norske", "norge",
            "å", "dei", "me", "ho"
        };

    internal static readonly System.Text.RegularExpressions.Regex TokenRegex =
        new(@"[^a-zæøåA-ZÆØÅ]+",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static List<string> ExtractKeywords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        return TokenRegex
            .Split(text.ToLowerInvariant())
            .Where(w => w.Length >= 3)
            .Where(w => !Stopwords.Contains(w))
            .Distinct()
            .ToList();
    }
}
