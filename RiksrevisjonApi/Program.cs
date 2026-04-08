using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHttpClient("rr", c =>
{
    c.BaseAddress = new Uri("https://www.riksrevisjonen.no");
    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; RRDashboard/1.0)");
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<ReportService>();
builder.Services.AddSingleton<DoffinService>();
builder.Services.AddSingleton<MatchService>();

var app = builder.Build();
app.UseCors();

var svc = app.Services.GetRequiredService<ReportService>();
_ = svc.LoadAsync();

var doffinSvc = app.Services.GetRequiredService<DoffinService>();
_ = doffinSvc.LoadAsync();

var matchSvc = app.Services.GetRequiredService<MatchService>();
_ = matchSvc.LoadAsync();

app.MapGet("/api/reports", (ReportService s) =>
    Results.Ok(new { loading = s.IsLoading, reports = s.Reports }));

// Force a fresh scrape (ignores cache)
app.MapPost("/api/reports/refresh", async (ReportService s) =>
{
    _ = s.LoadAsync(forceRefresh: true);
    return Results.Accepted();
});

app.MapGet("/api/notices", (DoffinService s) =>
    Results.Ok(new { loading = s.IsLoading, notices = s.Notices }));

app.MapPost("/api/notices/refresh", async (DoffinService s) =>
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

app.Run();

// ─── Service ──────────────────────────────────────────────────────────────────

class ReportService(IHttpClientFactory factory)
{
    private static readonly string CacheFile = Path.Combine(
        AppContext.BaseDirectory, "reports-cache.json");
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private List<Report> _reports = [];
    public bool IsLoading { get; private set; }
    public IReadOnlyList<Report> Reports { get { lock (_reports) return _reports.ToList(); } }

    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        try
        {
            // Serve from cache if fresh enough
            if (!forceRefresh && TryLoadCache(out var cached))
            {
                lock (_reports) { _reports.Clear(); _reports.AddRange(cached!); }
                Console.WriteLine($"[cache] Loaded {cached!.Count} reports from {CacheFile}");
                return;
            }

            await ScrapeAsync();
            SaveCache();
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
            Console.WriteLine($"[cache] Saved {Reports.Count} reports to {CacheFile}");
        }
        catch (Exception ex) { Console.WriteLine($"[cache] Save failed: {ex.Message}"); }
    }

    // ── Scrape ───────────────────────────────────────────────────────────────

    private async Task ScrapeAsync()
    {
        lock (_reports) _reports.Clear();

        var client = factory.CreateClient("rr");

        // Probe pages sequentially until empty
        int page = 1;
        var knownPages = new List<int>();
        while (true)
        {
            var probe = await FetchListPage(client, page);
            if (probe.Count == 0) break;
            knownPages.Add(page);
            page++;
        }
        Console.WriteLine($"[scrape] Found {knownPages.Count} pages");

        // Re-fetch all confirmed pages in parallel
        var pageTasks = knownPages.Select(p => FetchListPage(client, p));
        var pages = await Task.WhenAll(pageTasks);
        var items = pages.SelectMany(x => x).ToList();
        Console.WriteLine($"[scrape] Enriching {items.Count} reports...");

        // Enrich with severity in parallel, streaming as they arrive
        var sem = new SemaphoreSlim(8);
        var enrichTasks = items.Select(async item =>
        {
            await sem.WaitAsync();
            try
            {
                var enriched = await Enrich(client, item);
                lock (_reports) _reports.Add(enriched);
                return enriched;
            }
            catch { lock (_reports) _reports.Add(item); return item; }
            finally { sem.Release(); }
        });

        await Task.WhenAll(enrichTasks);
        Console.WriteLine($"[scrape] Done — {_reports.Count} reports loaded");
    }

    static async Task<List<Report>> FetchListPage(HttpClient client, int page)
    {
        try
        {
            var html = await client.GetStringAsync($"/rapporter/?p={page}");
            return ParseListPage(html, page);
        }
        catch { return []; }
    }

    static List<Report> ParseListPage(string html, int page)
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
             string[] MatchedKeywords, string? MatchedOrg);

// ─── DoffinService ────────────────────────────────────────────────────────────

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
                Console.WriteLine($"[doffin] Loaded {cached!.Count} notices from cache");
                return;
            }

            await EnsureBrowsersInstalled();
            await ScrapeAsync();
            SaveCache();
        }
        finally { IsLoading = false; }
    }

    private static Task EnsureBrowsersInstalled()
    {
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (exitCode != 0)
            Console.WriteLine($"[doffin] WARNING: browser install failed with code {exitCode}");
        return Task.CompletedTask;
    }

    private bool TryLoadCache(out List<Notice>? notices)
    {
        notices = null;
        if (!File.Exists(CacheFile)) return false;
        if (File.GetLastWriteTime(CacheFile) < DateTime.Now - CacheMaxAge) return false;
        try
        {
            var json = File.ReadAllText(CacheFile);
            notices = JsonSerializer.Deserialize<List<Notice>>(json, JsonOpts);
            return notices?.Count > 0;
        }
        catch { return false; }
    }

    private void SaveCache()
    {
        try
        {
            var json = JsonSerializer.Serialize(Notices, JsonOpts);
            File.WriteAllText(CacheFile, json);
            Console.WriteLine($"[doffin] Saved {Notices.Count} notices to {CacheFile}");
        }
        catch (Exception ex) { Console.WriteLine($"[doffin] Cache save failed: {ex.Message}"); }
    }

    private async Task ScrapeAsync()
    {
        lock (_notices) _notices.Clear();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new() { Headless = true });

        // Phase 1: Paginate list pages sequentially
        int pageNum = 1;
        while (true)
        {
            var (notices, hasNext) = await ScrapeListPage(browser, pageNum);
            lock (_notices) _notices.AddRange(notices);
            if (!hasNext) break;
            pageNum++;
        }
        Console.WriteLine($"[doffin] Found {_notices.Count} notices across {pageNum} pages");

        // Phase 2: Fetch detail descriptions in parallel with SemaphoreSlim(5)
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
        Console.WriteLine($"[doffin] Done — {_notices.Count} notices loaded");
    }

    private static async Task<(List<Notice> notices, bool hasNextPage)> ScrapeListPage(
        IBrowser browser, int pageNum)
    {
        var page = await browser.NewPageAsync();
        try
        {
            await page.GotoAsync(
                $"https://www.doffin.no/search?searchString=helfo&page={pageNum}",
                new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30_000 });

            try
            {
                await page.WaitForSelectorAsync("a[class*=card]", new() { Timeout = 10_000 });
            }
            catch
            {
                return ([], false);
            }

            var cards = await page.QuerySelectorAllAsync("a[class*=card]");
            var notices = new List<Notice>();
            int id = (pageNum - 1) * 20 + 1;

            foreach (var card in cards)
            {
                var href = await card.GetAttributeAsync("href") ?? "";
                var titleEl = await card.QuerySelectorAsync("[data-testid='notice-card-title']");
                var title = (await titleEl?.TextContentAsync()!)?.Trim() ?? "";
                var buyerEl = await card.QuerySelectorAsync("p[class*=buyer]");
                var buyer = (await buyerEl?.TextContentAsync()!)?.Trim() ?? "";
                var dateEl = await card.QuerySelectorAsync("p[class*=issue_date]");
                var dateLabel = (await dateEl?.GetAttributeAsync("aria-label")!) ?? "";

                var dateMatch = Regex.Match(dateLabel, @"(\d{2}\.\d{2}\.\d{4})");
                var pubDate = "";
                if (dateMatch.Success &&
                    DateOnly.TryParseExact(dateMatch.Groups[1].Value, "dd.MM.yyyy", out var d))
                    pubDate = d.ToString("yyyy-MM-dd");

                var noticeUrl = $"https://www.doffin.no{href}";
                notices.Add(new Notice(id++, title, buyer, pubDate, noticeUrl, ""));
            }

            var nextBtn = await page.QuerySelectorAsync("[data-cy='Neste side']");
            bool hasNext = nextBtn != null &&
                           await nextBtn.GetAttributeAsync("disabled") == null;

            return (notices, hasNext);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task<string> FetchDetailDescription(IBrowser browser, string url)
    {
        var page = await browser.NewPageAsync();
        try
        {
            await page.GotoAsync(url,
                new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30_000 });

            try
            {
                await page.WaitForSelectorAsync("h1", new() { Timeout = 10_000 });
            }
            catch { return ""; }

            var descEl = await page.QuerySelectorAsync("p[class*=content_section_description]");
            return (await descEl?.TextContentAsync()!)?.Trim() ?? "";
        }
        catch { return ""; }
        finally
        {
            await page.CloseAsync();
        }
    }
}

// ─── MatchService ─────────────────────────────────────────────────────────────

class MatchService(ReportService reportSvc, DoffinService doffinSvc)
{
    private List<Match> _matches = [];
    public bool IsLoading { get; private set; }
    public IReadOnlyList<Match> Matches { get { lock (_matches) return _matches.ToList(); } }

    private static readonly string CacheFile = Path.Combine(
        AppContext.BaseDirectory, "matches-cache.json");
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        try
        {
            if (!forceRefresh && TryLoadCache(out var cached))
            {
                lock (_matches) { _matches.Clear(); _matches.AddRange(cached!); }
                Console.WriteLine($"[matches] Loaded {cached!.Count} matches from cache");
                return;
            }

            // Wait for upstream services to finish their own loading
            while (reportSvc.IsLoading || doffinSvc.IsLoading)
                await Task.Delay(100);

            RunComputeMatches();
            SaveCache();
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
            Console.WriteLine($"[matches] Saved {Matches.Count} matches to {CacheFile}");
        }
        catch (Exception ex) { Console.WriteLine($"[matches] Cache save failed: {ex.Message}"); }
    }

    // ── Scoring Algorithm (REQ-03 + REQ-04) ─────────────────────────────────────

    /// Pure-function overload — testable without real services (InternalsVisibleTo).
    internal static List<Match> ComputeMatches(
        IReadOnlyList<Report> reports,
        IReadOnlyList<Notice> notices)
    {
        var results = new List<Match>();

        foreach (var report in reports)
        {
            var keywords = ExtractKeywords(report.Title + " " + report.Summary);
            if (keywords.Count == 0) continue;  // guard: avoids division-by-zero in keyword_score

            var normDept = NormalizeDepartment(report.Department);

            foreach (var notice in notices)
            {
                var (keyScore, matchedKw) = ComputeKeywordScore(keywords, notice);
                var (orgScore, matchedOrg) = ComputeOrgScore(normDept, notice);
                var combined = keyScore * 0.6 + orgScore * 0.4;

                if (combined > 15)
                    results.Add(new Match(
                        report.Id, notice.Id,
                        Math.Round(combined, 1),
                        matchedKw.ToArray(),
                        matchedOrg));
            }
        }

        return results;
    }

    private void RunComputeMatches()
    {
        var results = ComputeMatches(reportSvc.Reports, doffinSvc.Notices);
        lock (_matches) { _matches.Clear(); _matches.AddRange(results); }
        Console.WriteLine($"[matches] Computed {results.Count} matches " +
                          $"({reportSvc.Reports.Count} reports x {doffinSvc.Notices.Count} notices)");
    }

    internal static (double score, List<string> matched) ComputeKeywordScore(
        List<string> keywords, Notice notice)
    {
        if (keywords.Count == 0) return (0.0, []);

        var noticeText = (notice.Title + " " + notice.Description).ToLowerInvariant();
        var matched = keywords.Where(kw => noticeText.Contains(kw)).ToList();
        var score = (matched.Count / (double)keywords.Count) * 100.0;
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

    private static readonly HashSet<string> Stopwords =
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

    private static readonly System.Text.RegularExpressions.Regex TokenRegex =
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
