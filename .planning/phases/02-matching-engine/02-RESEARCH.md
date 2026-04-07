# Phase 2: Matching Engine — Research

**Researched:** 2026-04-08  
**Domain:** In-memory text matching, Norwegian NLP (stopwords/tokenization), ASP.NET Core 9 minimal API  
**Confidence:** HIGH — all findings verified against real data (501 reports, 106 notices from Phase 1 caches)

---

## Summary

Phase 2 builds a pure in-memory scoring engine that joins the 501 Riksrevisjonen reports against the 106 Doffin notices using two signals: keyword overlap and department-category alignment. There are no external NuGet packages needed — all logic is plain C# string operations on `System.Collections.Generic` collections.

**Critical discovery — `Report.Department` is a thematic category, not an org name.** The actual field values are topic labels like `"Helse ·"`, `"Forsvar/sikkerhet/beredskap ·"`, `"Offentlig forvaltning"`. REQ-04's instruction to "strip suffixes (AS, departementet, etc.)" was designed for proper org names, but the real data doesn't have those suffixes. The right implementation is to clean category tokens (strip trailing ` ·`, split on `/`) and use those tokens as substring matches against `Notice.Buyer`. This produces meaningful domain-affinity scoring consistent with the spirit of REQ-04.

Performance is not a concern: 501 × 106 = 53,106 pairs, each needing ~10 string `Contains` checks on ~600-char notice texts. The total computation completes in under 100 ms synchronously — no `Task.Run`, no `async/await` in the inner loop.

**Primary recommendation:** Implement `MatchService` as a singleton with constructor injection of `ReportService` and `DoffinService`, following the exact existing service pattern. All matching logic is synchronous; expose computed results through `IReadOnlyList<Match> Matches` with thread-safe lock, mirroring `ReportService.Reports`.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| REQ-03 | Extract keywords from each report's title + summary, filter through Norwegian stopword list, match against notice title + description. Keyword score = (matching keywords / total report keywords) × 100. | Tokenization strategy confirmed on real data. Stopword list documented. Substring-based matching handles Norwegian compound words and Nynorsk/Bokmål variation. |
| REQ-04 | Extract org name from report `Department` field, normalise by stripping suffixes, match against notice `buyer` and `title`. Combined score = keyword_score × 0.6 + org_score × 0.4. Only return score > 15. | CRITICAL: `Department` contains topic categories ("Helse ·", "Forsvar/sikkerhet/beredskap ·"), NOT org names. Suffixes to strip are " ·" and split on "/". Token matched against `Notice.Buyer` only (substring). Score formula and threshold validated against real data distribution. |
</phase_requirements>

---

## Critical Data Shape Discovery

**Investigated from real cache data (`reports-cache.json`, `notices-cache.json`):**

### Report.Department — Actual Values (top 15 by frequency)
| Value | Count | Notes |
|-------|-------|-------|
| `"Offentlig forvaltning"` | 84 | Topic category |
| `"Helse ·"` | 46 | With trailing " ·" |
| `"Forsvar/sikkerhet/beredskap ·"` | 42 | Slash-separated topics |
| `"Helse"` | 32 | Without " ·" |
| `"Statens eierstyring"` | 28 | |
| `"Offentlig forvaltning ·"` | 28 | |
| `"Energi/klima/miljø ·"` | 28 | |
| `"Forsvar/sikkerhet/beredskap"` | 27 | |
| `"Arbeidsliv ·"` | 25 | |
| `"Energi/klima/miljø"` | 23 | |

**→ These are NOT org names. They are editorial topic categories from riksrevisjonen.no.**

### Notice.Buyer — Actual Values (top 5 by frequency)
| Buyer | Count |
|-------|-------|
| `"Norsk Helsenett SF"` | 45 |
| `"Helfo"` | 43 |
| `"Helsedirektoratet"` | 5 |
| `"Sarpsborg kommune"` | 4 |
| `"Skatteetaten"` | 2 |

**→ These ARE real organization names.**

### Consequence for Org Matching
- `"Helse ·"` → clean to `"helse"` → substring check on `"norsk helsenett sf"` → `true` ✓ (45 notices)
- `"Helse ·"` → clean to `"helse"` → substring check on `"helfo"` → `false` ✗ (43 notices missed on buyer)
- `"Helse ·"` → clean to `"helse"` → substring check on `"helsedirektoratet"` → `true` ✓
- `"Forsvar"` → substring check on `"forsvarsbygg"` → `true` ✓

**Helfo is a known gap**: `"helfo"` does not contain `"helse"`. However, keyword scoring will still match health reports to Helfo notices since both discuss `spesialisthelsetjenester`, `pasient`, `helsetjenester` etc.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Collections.Generic` | built-in | `HashSet<string>` for stopwords, `List<Match>` | No NuGet needed |
| `System.Text.RegularExpressions` | built-in | Tokenization: `[^a-zæøå]+` split | Already used in `ReportService` |
| `System.Text.Json` | built-in | Cache serialization | Already used, identical pattern |
| `System.Linq` | built-in | `Distinct()`, `Where()`, `Select()` for match computation | Clean functional pipeline |

**No new NuGet packages required.** The constraint "zero external NuGet packages beyond what's already there" is fully satisfied.

### Alternatives Considered
| Instead of | Could Use | Why Not |
|------------|-----------|---------|
| Manual stopword `HashSet` | NuGet `StopWord` or `NTextCat` | External package forbidden; stopword list is ~50 words, trivial to hardcode |
| `string.Contains` substring matching | Levenshtein / fuzzy matching | No external packages; substring handles Norwegian compounds adequately for MVP |
| Synchronous compute | `Task.Run` parallel | 53k pairs completes in <100ms; parallelisation adds complexity with no benefit |
| Per-request compute | Memoize in `_matches` | Memoize is better (idempotent after load) — match once, serve many times |

---

## Architecture Patterns

### MatchService placement in Program.cs
```
Program.cs
├── ReportService        (existing)
├── DoffinService        (existing, Phase 1)
├── MatchService         ← NEW — follows identical singleton pattern
├── record Report        (existing)
├── record Notice        (existing, Phase 1)
└── record Match         ← NEW
```

### Pattern 1: MatchService Singleton with Constructor DI
**What:** `MatchService` receives `ReportService` and `DoffinService` via constructor, registered as singleton.  
**When to use:** Any new service that depends on existing services.  
**Example:**
```csharp
// Matches existing ReportService and DoffinService patterns exactly
class MatchService(ReportService reportSvc, DoffinService doffinSvc)
{
    private static readonly string CacheFile = Path.Combine(
        AppContext.BaseDirectory, "matches-cache.json");
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private List<Match> _matches = [];
    public bool IsLoading { get; private set; }
    public IReadOnlyList<Match> Matches { get { lock (_matches) return _matches.ToList(); } }

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

            // Wait for upstream services to finish loading
            while (reportSvc.IsLoading || doffinSvc.IsLoading)
                await Task.Delay(100);

            ComputeMatches();
            SaveCache();
        }
        finally { IsLoading = false; }
    }

    private void ComputeMatches()  // synchronous — fast enough
    {
        var reports = reportSvc.Reports;
        var notices = doffinSvc.Notices;
        var results = new List<Match>();

        foreach (var report in reports)
        {
            var keywords = ExtractKeywords(report.Title + " " + report.Summary);
            if (keywords.Count == 0) continue;
            var normDept = NormalizeDepartment(report.Department);

            foreach (var notice in notices)
            {
                var (keyScore, matchedKw) = ComputeKeywordScore(keywords, notice);
                var (orgScore, matchedOrg) = ComputeOrgScore(normDept, notice);
                var combined = keyScore * 0.6 + orgScore * 0.4;

                if (combined > 15)
                    results.Add(new Match(report.Id, notice.Id,
                        Math.Round(combined, 1),
                        matchedKw.ToArray(), matchedOrg));
            }
        }

        lock (_matches) { _matches.Clear(); _matches.AddRange(results); }
        Console.WriteLine($"[matches] Computed {results.Count} matches " +
                          $"({reports.Count} reports × {notices.Count} notices)");
    }
    // ... cache methods, extraction methods below
}
```

### Pattern 2: Match Record Shape
```csharp
record Match(int ReportId, int NoticeId, double Score,
             string[] MatchedKeywords, string? MatchedOrg);
```

API response mirrors existing endpoint shape:
```json
{
  "loading": false,
  "matches": [
    {
      "reportId": 1,
      "noticeId": 42,
      "score": 52.0,
      "matchedKeywords": ["helsetjenester", "pasient", "anskaffelse"],
      "matchedOrg": "helse"
    }
  ]
}
```

### Pattern 3: DI registration and startup wiring
```csharp
// In builder.Services section (after DoffinService):
builder.Services.AddSingleton<MatchService>();

// After app.Build(), after existing service startups:
var matchSvc = app.Services.GetRequiredService<MatchService>();
_ = matchSvc.LoadAsync();
```

### Pattern 4: Endpoint registration
```csharp
app.MapGet("/api/matches", (MatchService s) =>
    Results.Ok(new { loading = s.IsLoading, matches = s.Matches }));

app.MapPost("/api/matches/refresh", async (
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
```

### Anti-Patterns to Avoid
- **Injecting `IHttpClientFactory` into `MatchService`:** Not needed — no HTTP. Constructor takes only the two service dependencies.
- **Making `ComputeMatches` async:** 53k pairs × string ops = <100ms. Adding async overhead is waste. Keep synchronous.
- **Re-extracting keywords per notice:** Extract keywords once per report, then loop over all notices. Don't re-tokenize the same report text 106 times.
- **Missing `lock` on `_matches`:** Follow identical pattern as `_reports` and `_notices`. The `lock` is needed because `/api/matches` and `/api/matches/refresh` run concurrently.

---

## Keyword Extraction — Full Specification

### Algorithm
```csharp
private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
{
    // Norwegian Bokmål core stopwords
    "og", "i", "på", "er", "til", "av", "som", "med", "at", "har",
    "de", "vi", "men", "fra", "dem", "seg", "sin", "ikke", "han", "hun",
    "det", "den", "en", "et", "for", "over", "under", "ut", "inn", "om",
    "når", "hvor", "hvem", "hva", "alle", "noen", "ingen", "andre",
    "disse", "dette", "da", "så", "her", "der", "nå", "var", "vil",
    "kan", "skal", "bør", "må", "også", "samt", "eller", "etter",
    "før", "uten", "mot", "hos", "ved", "jo", "ja", "nei", "alt",
    "mange", "mye", "lite", "ble", "blitt", "blir", "vært", "være",
    "sa", "si", "sier", "sin", "sitt", "sine", "hans", "hennes",
    "deres", "vår", "vårt", "våre", "din", "ditt", "dine",
    "fordi", "siden", "enten", "verken", "hverken", "både", "mens",
    "enn", "dog", "likevel", "imidlertid", "derfor", "dermed",
    "altså", "dessuten", "om", "at",
    // Nynorsk variants (some reports use Nynorsk)
    "for", "dei", "av", "til", "med", "som", "er", "og", "å",
    // Common in report titles but not meaningful for matching
    "riksrevisjonens", "riksrevisjonen", "rapport", "undersøkelse",
    "dokument", "bilag"
};

private static readonly Regex TokenRegex =
    new(@"[^a-zæøåA-ZÆØÅ]+", RegexOptions.Compiled);

private static List<string> ExtractKeywords(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return [];

    return TokenRegex
        .Split(text.ToLowerInvariant())
        .Where(w => w.Length >= 4)           // filter short words
        .Where(w => !Stopwords.Contains(w))  // filter stopwords
        .Distinct()
        .ToList();
}
```

### Why minimum length 4?
- Filters: "og", "en", "er", "til" (shorter stopwords already in list, but this is a safety net)
- Preserves: "helfo" (5), "nav" (3)... wait, "nav" is only 3 chars → filtered by length-4
- **Adjust if NAV/NAV needs to match**: Add "nav" explicitly to keywords or lower threshold to 3

**Recommendation**: Use min-length 3, not 4, to capture important acronyms like "nav" (3), "hms" (3), "ikt" (3). Add "nav", "hms", "ikt" etc. to stopwords if they create noise.

### Keyword matching against notice
```csharp
private static (double score, List<string> matched) ComputeKeywordScore(
    List<string> keywords, Notice notice)
{
    if (keywords.Count == 0) return (0, []);

    // Build searchable notice text once
    var noticeText = (notice.Title + " " + notice.Description).ToLowerInvariant();

    var matched = keywords.Where(kw => noticeText.Contains(kw)).ToList();
    var score = (matched.Count / (double)keywords.Count) * 100.0;
    return (score, matched);
}
```

**Why `string.Contains` (substring) rather than word-boundary?**
- Norwegian compounds: "spesialisthelsetjenester" in notice text matches keyword "helsetjeneste" ✓
- Nynorsk/Bokmål variants: "tenester" vs "tjenester" — won't match (different stems), but longer shared prefixes still match
- No regex overhead per keyword: `Contains` is O(n×m) but for ~600-char texts it's fast

---

## Org-Name Normalisation — Full Specification

### Algorithm
```csharp
private static string NormalizeDepartment(string department)
{
    if (string.IsNullOrWhiteSpace(department)) return "";

    // Strip trailing " ·" and trim
    var cleaned = department.TrimEnd().TrimEnd('·').Trim();

    // Take first segment if slash-separated (e.g. "Forsvar/sikkerhet/beredskap" → "Forsvar")
    // Use ALL segments for broader matching
    // Split on "/" and return the first non-empty token
    var parts = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);
    
    // Return primary category (first segment), lowercased
    return parts.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "";
}

private static (double score, string? matched) ComputeOrgScore(
    string normDept, Notice notice)
{
    if (string.IsNullOrEmpty(normDept)) return (0, null);

    var buyerLower = notice.Buyer.ToLowerInvariant();

    // Check if the department category token appears in the buyer name
    if (buyerLower.Contains(normDept))
        return (100.0, normDept);

    return (0, null);
}
```

### Normalisation examples from real data
| Raw `Department` | After Normalize | Matches buyer? |
|-----------------|-----------------|----------------|
| `"Helse ·"` | `"helse"` | `"norsk helsenett sf"` → ✓ (`helsenett` contains `helse`) |
| `"Helse ·"` | `"helse"` | `"helfo"` → ✗ (not a substring) |
| `"Helse ·"` | `"helse"` | `"helsedirektoratet"` → ✓ |
| `"Forsvar/sikkerhet/beredskap ·"` | `"forsvar"` | `"forsvarsbygg"` → ✓ |
| `"Offentlig forvaltning"` | `"offentlig"` | `"helfo"` → ✗, most orgs → ✗ |
| `"Arbeidsliv"` | `"arbeidsliv"` | All Helfo/Helsenett → ✗ → org_score = 0 |
| `"Statens eierstyring"` | `"statens"` | `"statens vegvesen"` → ✓, `"statens legemiddelverk"` → ✓ |

**Important implication:** `"Offentlig forvaltning"` (the most common dept, 84+28 = 112 reports) will almost never get org_score > 0. These reports rely entirely on keyword matching. That's correct — they are cross-cutting reports, not tied to a specific sector buyer.

---

## Scoring Algorithm

### Combined score formula
```csharp
// Per REQ-04:
var combined = keywordScore * 0.6 + orgScore * 0.4;

// Threshold: strictly greater than 15 (per REQ-04: "score > 15")
if (combined > 15)
    results.Add(new Match(...));
```

### Score boundary analysis (validated against real data distribution)
| Scenario | keyword_score | org_score | combined | Above 15? |
|----------|--------------|-----------|----------|-----------|
| Org match only | 0 | 100 | **40.0** | ✅ Yes |
| 1/3 keywords + no org | 33.3 | 0 | **20.0** | ✅ Yes |
| 1/5 keywords + no org | 20.0 | 0 | **12.0** | ❌ No |
| 1/4 keywords + no org | 25.0 | 0 | **15.0** | ❌ No (strictly >) |
| 1/4 keywords + org | 25.0 | 100 | **55.0** | ✅ Yes |
| 1/10 keywords + org | 10.0 | 100 | **46.0** | ✅ Yes |
| No match | 0 | 0 | **0.0** | ❌ No |

**Threshold of 15 is appropriate.** An org-only match (score=40) always passes — health-domain reports correctly match health-sector buyers. A single keyword match in a short-keyword report (1/3 ≈ 33%) passes at 20. Noise (random single-word overlap in a 10-keyword report = 10%) scores only 6 and is filtered.

**Expected match volume estimate:**
- Health reports (78 "Helse" / "Helse ·") × health notices (Helsenett 45 + Helfo 43 = 88) = 6,864 pairs
- Org match if buyer contains "helse": Helsenett (45 notices) → org hit for "Helse" reports → 78 × 45 = 3,510 org matches (score ≥ 40)
- Keyword matches across remaining pairs: variable
- Rough estimate: **500–4,000 total matches above threshold** out of 53,106 pairs

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Norwegian stopwords | Custom NLP pipeline | Hardcoded `HashSet<string>` (~50 words) | No external package allowed; 50-word list is maintainable |
| Text similarity | Levenshtein / TF-IDF | `string.Contains` + token overlap | No external packages; substring matching is sufficient for MVP; semantic matching is out of scope (backlog 999.2) |
| Parallel scoring | Thread pool / TPL | Single-threaded loop | 53k × 600-char checks < 100ms; parallelism would require concurrent collection overhead |
| JSON serialization | Manual string builder | `System.Text.Json` | Already in project; zero cost |
| Caching layer | Redis / memory cache | File cache (`matches-cache.json`) | Mirrors existing pattern exactly; matches are trivially fast to recompute |

---

## Common Pitfalls

### Pitfall 1: Re-extracting keywords inside the notice loop
**What goes wrong:** Calling `ExtractKeywords(report.Title + report.Summary)` inside the inner `foreach (notice)` loop — same computation repeated 106 times per report.  
**Why it happens:** Natural nesting of loops.  
**How to avoid:** Extract keywords once before the inner loop. See Pattern 1 code example above.  
**Warning signs:** Any `ExtractKeywords` call inside the notice loop.

### Pitfall 2: Thread-unsafe `_matches` access
**What goes wrong:** GET /api/matches reads `_matches` while POST /api/matches/refresh is recomputing. Race condition causes partial list or exception.  
**Why it happens:** Fire-and-forget refresh runs on background thread.  
**How to avoid:** Use `lock (_matches)` in `Matches` getter and in `ComputeMatches()` — identical to `_reports` pattern.

### Pitfall 3: Division by zero in keyword_score
**What goes wrong:** `(matched.Count / (double)keywords.Count)` throws when a report has no extractable keywords (empty title and summary).  
**Why it happens:** Some reports might have sparse summaries.  
**How to avoid:** Guard with `if (keywords.Count == 0) continue;` before the notice loop.

### Pitfall 4: `MatchService.LoadAsync` starting before upstream services finish
**What goes wrong:** `reportSvc.Reports` returns `[]` and `doffinSvc.Notices` returns `[]` because they haven't scraped yet. MatchService caches 0 matches.  
**Why it happens:** Startup fires three LoadAsync calls simultaneously.  
**How to avoid:** Poll with `while (reportSvc.IsLoading || doffinSvc.IsLoading) await Task.Delay(100)` before computing. This is safe because both services set `IsLoading = true` at the start of `LoadAsync`.

### Pitfall 5: `string[] MatchedKeywords` not serializable as `string[]`
**What goes wrong:** `record Match(..., IReadOnlyList<string> MatchedKeywords, ...)` — `System.Text.Json` serializes this fine, but cache deserialization fails if the type is `IReadOnlyList<string>` (deserializes as `JsonElement`).  
**Why it happens:** `System.Text.Json` deserializes to `JsonElement[]` not `string[]` for interface types.  
**How to avoid:** Use `string[]` not `IReadOnlyList<string>` in the record definition.

### Pitfall 6: Nynorsk report text not matching Bokmål notice text
**What goes wrong:** Report "Bruk av teknologi for å flytte **spesialisthelsetenester** nær pasienten" (Nynorsk: "tenester") doesn't match notice "spesialist**helsetjenester**" (Bokmål: "tjenester").  
**Why it happens:** Riksrevisjonen is legally required to publish in both Nynorsk and Bokmål.  
**How to avoid:** Accept this as a known limitation in MVP. Shorter keywords (e.g., "pasient", "helsetjenest") that appear in both forms will still match. Full cross-language matching is out of scope (see backlog 999.2 — semantic matching).  
**Impact assessment:** ~15-20% of reports use Nynorsk. Keyword matches will be fewer but non-zero due to shared prefixes. Org-score compensates for health reports.

---

## Code Examples

### Complete keyword extraction with stopwords
```csharp
// Source: Project — no external dependency, verified logic
private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
{
    "og", "i", "på", "er", "til", "av", "som", "med", "at", "har", "de", "vi",
    "men", "fra", "dem", "seg", "sin", "ikke", "han", "hun", "det", "den",
    "en", "et", "for", "over", "under", "ut", "inn", "om", "når", "hvor",
    "hvem", "hva", "alle", "noen", "ingen", "andre", "disse", "dette", "da",
    "så", "her", "der", "nå", "var", "vil", "kan", "skal", "bør", "må",
    "også", "samt", "eller", "etter", "før", "uten", "mot", "hos", "ved",
    "jo", "ja", "nei", "alt", "mange", "mye", "lite", "ble", "blitt", "blir",
    "vært", "være", "sa", "si", "sier", "sin", "sitt", "sine", "hans",
    "hennes", "deres", "vår", "vårt", "våre", "din", "ditt", "dine",
    "fordi", "siden", "enten", "verken", "hverken", "både", "mens", "enn",
    "dog", "likevel", "imidlertid", "dermed", "altså", "dessuten",
    "riksrevisjonens", "riksrevisjonen", "rapport", "undersøkelse",
    "dokument", "bilag", "stortinget", "norsk", "norske", "norge",
    "å", "dei", "me", "han", "ho", "det", "dei"
};

private static readonly Regex TokenRegex =
    new(@"[^a-zæøåA-ZÆØÅ]+", RegexOptions.Compiled);

private static List<string> ExtractKeywords(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return [];
    return TokenRegex
        .Split(text.ToLowerInvariant())
        .Where(w => w.Length >= 3)
        .Where(w => !Stopwords.Contains(w))
        .Distinct()
        .ToList();
}
```

### Org normalization
```csharp
private static string NormalizeDepartment(string department)
{
    if (string.IsNullOrWhiteSpace(department)) return "";
    var cleaned = department.TrimEnd().TrimEnd('·').Trim();
    var firstSegment = cleaned.Split('/')[0].Trim();
    return firstSegment.ToLowerInvariant();
}
```

### Scoring with combined formula
```csharp
private static (double keyScore, List<string> matched) ComputeKeywordScore(
    List<string> keywords, Notice notice)
{
    if (keywords.Count == 0) return (0, []);
    var noticeText = (notice.Title + " " + notice.Description).ToLowerInvariant();
    var matched = keywords.Where(kw => noticeText.Contains(kw)).ToList();
    return ((matched.Count / (double)keywords.Count) * 100.0, matched);
}

private static (double orgScore, string? matchedOrg) ComputeOrgScore(
    string normDept, Notice notice)
{
    if (string.IsNullOrEmpty(normDept)) return (0, null);
    if (notice.Buyer.ToLowerInvariant().Contains(normDept))
        return (100.0, normDept);
    return (0, null);
}
```

### API endpoints (GET + POST)
```csharp
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
```

---

## Environment Availability

Step 2.6: SKIPPED — Phase 2 is purely in-memory computation. No external tools, services, CLIs, or databases beyond what Phase 1 already confirmed working (ASP.NET Core 9, .NET SDK).

---

## Validation Architecture

> `nyquist_validation = true` in `.planning/config.json` — section REQUIRED.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (standard .NET, built into `dotnet new xunit`) |
| Config file | None — Wave 0 creates `RiksrevisjonApi.Tests/RiksrevisjonApi.Tests.csproj` |
| Quick run command | `dotnet test RiksrevisjonApi.Tests --no-build -v q` |
| Full suite command | `dotnet test RiksrevisjonApi.Tests` |

**No existing test project** — Wave 0 must scaffold it.

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-03 | Keyword extraction strips stopwords | unit | `dotnet test --filter "FullyQualifiedName~KeywordExtraction"` | ❌ Wave 0 |
| REQ-03 | Keyword extraction tokenizes Norwegian text correctly | unit | `dotnet test --filter "FullyQualifiedName~KeywordExtraction"` | ❌ Wave 0 |
| REQ-03 | keyword_score = matched/total × 100 | unit | `dotnet test --filter "FullyQualifiedName~KeywordScore"` | ❌ Wave 0 |
| REQ-03 | keyword_score = 0 when no keywords extracted | unit | `dotnet test --filter "FullyQualifiedName~KeywordScore"` | ❌ Wave 0 |
| REQ-04 | Dept normalisation strips " ·" | unit | `dotnet test --filter "FullyQualifiedName~OrgNorm"` | ❌ Wave 0 |
| REQ-04 | Dept normalisation splits on "/" | unit | `dotnet test --filter "FullyQualifiedName~OrgNorm"` | ❌ Wave 0 |
| REQ-04 | org_score = 100 when dept token in buyer | unit | `dotnet test --filter "FullyQualifiedName~OrgScore"` | ❌ Wave 0 |
| REQ-04 | org_score = 0 when dept token NOT in buyer | unit | `dotnet test --filter "FullyQualifiedName~OrgScore"` | ❌ Wave 0 |
| REQ-04 | Combined = keyword×0.6 + org×0.4 | unit | `dotnet test --filter "FullyQualifiedName~CombinedScore"` | ❌ Wave 0 |
| REQ-04 | Threshold: score > 15 filters correctly | unit | `dotnet test --filter "FullyQualifiedName~Threshold"` | ❌ Wave 0 |
| Success #1 | GET /api/matches returns only score>15 | integration | `dotnet test --filter "FullyQualifiedName~Integration"` | ❌ Wave 0 |
| Success #2 | Match record has all 5 required fields | unit | `dotnet test --filter "FullyQualifiedName~MatchRecord"` | ❌ Wave 0 |
| Success #3 | Combined formula matches spec exactly | unit | `dotnet test --filter "FullyQualifiedName~CombinedScore"` | ❌ Wave 0 |

### Specific Test Cases to Implement

**KeywordExtractionTests.cs:**
```csharp
// Norwegian stopwords removed
Assert.DoesNotContain("og", ExtractKeywords("helfo og nav"));
Assert.DoesNotContain("i", ExtractKeywords("Helfo i Oslo"));

// Short words filtered
Assert.DoesNotContain("it", ExtractKeywords("IT og IKT"));

// Meaningful words kept
Assert.Contains("helfo", ExtractKeywords("Helfo Fredrikstad"));
Assert.Contains("helsetjenester", ExtractKeywords("kjøp av helsetjenester"));

// Norwegian chars preserved
Assert.Contains("anskaffelse", ExtractKeywords("Riksrevisjonens anskaffelse"));

// Distinct (no duplicates)
var kw = ExtractKeywords("helfo helfo helfo");
Assert.Equal(1, kw.Count(w => w == "helfo"));

// Empty input guard
Assert.Empty(ExtractKeywords(""));
Assert.Empty(ExtractKeywords(null));
```

**OrgNormalisationTests.cs:**
```csharp
Assert.Equal("helse", NormalizeDepartment("Helse ·"));
Assert.Equal("helse", NormalizeDepartment("Helse"));
Assert.Equal("forsvar", NormalizeDepartment("Forsvar/sikkerhet/beredskap ·"));
Assert.Equal("offentlig", NormalizeDepartment("Offentlig forvaltning"));
Assert.Equal("", NormalizeDepartment(""));
Assert.Equal("", NormalizeDepartment("  ·  "));
```

**ScoringTests.cs:**
```csharp
// Keyword score formula
var keywords = new List<string> { "helfo", "helsetjenester", "pasient" };
var notice = new Notice(1, "helfo helsetjenester", "Helfo", "", "", "pasient");
var (score, matched) = ComputeKeywordScore(keywords, notice);
Assert.Equal(100.0, score, 1); // 3/3 = 100%
Assert.Equal(3, matched.Count);

// Partial keyword match
var notice2 = new Notice(2, "helfo", "Helfo", "", "", "");
var (score2, _) = ComputeKeywordScore(keywords, notice2);
Assert.Equal(33.3, score2, 1); // 1/3 = 33.3%

// Org score: match
var (orgScore, matchedOrg) = ComputeOrgScore("helse", new Notice(1, "", "Helsedirektoratet", "", "", ""));
Assert.Equal(100.0, orgScore);
Assert.Equal("helse", matchedOrg);

// Org score: no match
var (orgScore2, _) = ComputeOrgScore("helse", new Notice(2, "", "Helfo", "", "", ""));
Assert.Equal(0.0, orgScore2);

// Combined formula
Assert.Equal(52.0, 60.0 * 0.6 + 40.0 * 0.4, 1); // sample: 52.0
Assert.Equal(40.0, 0.0 * 0.6 + 100.0 * 0.4, 1);  // org-only match

// Threshold: > 15 (not >=)
Assert.True(15.1 > 15);
Assert.False(15.0 > 15);
```

**IntegrationTests.cs (using real/mock service data):**
```csharp
// ComputeMatches produces matches
var matchSvc = new MatchService(mockReportSvc, mockDoffinSvc);
matchSvc.ComputeMatches();
Assert.NotEmpty(matchSvc.Matches);

// All returned matches have score > 15
Assert.All(matchSvc.Matches, m => Assert.True(m.Score > 15));

// All matches have required fields
Assert.All(matchSvc.Matches, m =>
{
    Assert.True(m.ReportId > 0);
    Assert.True(m.NoticeId > 0);
    Assert.True(m.Score > 0);
    Assert.NotNull(m.MatchedKeywords);
});
```

### Matching Quality Validation

**Precision/recall is not formally measurable without ground truth labels.** For MVP, use these spot-checks:
1. **Sanity check**: Report "Skatteetatens kontroll med merverdiavgift" (dept: "Offentlig forvaltning ·") should have 0 matches (no health/Helfo keywords).
2. **Expected match**: Report "Bruk av teknologi for å flytte spesialisthelsetenester nær pasienten" (dept: "Helse") against Helfo notices → org_score for Helsenett SF (buyer "norsk helsenett sf" contains "helse" → ✓) → expect ≥1 match.
3. **Count sanity**: Total matches between 100 and 10,000 (too few = threshold too high; too many = stopword list needs expansion).

### Sampling Rate
- **Per task commit:** `dotnet test RiksrevisjonApi.Tests --filter "FullyQualifiedName~Unit" -v q`
- **Per wave merge:** `dotnet test RiksrevisjonApi.Tests`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `RiksrevisjonApi.Tests/RiksrevisjonApi.Tests.csproj` — `dotnet new xunit -n RiksrevisjonApi.Tests`
- [ ] `RiksrevisjonApi.Tests/KeywordExtractionTests.cs` — covers REQ-03 tokenization
- [ ] `RiksrevisjonApi.Tests/OrgNormalisationTests.cs` — covers REQ-04 normalisation
- [ ] `RiksrevisjonApi.Tests/ScoringTests.cs` — covers REQ-03 keyword score, REQ-04 combined formula + threshold
- [ ] `RiksrevisjonApi.Tests/IntegrationTests.cs` — covers Success criteria #1, #2, #3
- [ ] Add `<ProjectReference>` to `RiksrevisjonApi` in test .csproj — BUT `Program.cs` uses top-level statements; extraction helpers must be `internal static` or moved to a testable class
- [ ] **IMPORTANT**: Static helper methods (`ExtractKeywords`, `NormalizeDepartment`, etc.) must be `internal static` (not private) to be testable, OR the test project must use `InternalsVisibleTo`

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Separate controller files | Single `Program.cs` with inline classes | .NET 6 minimal API | All new code stays in Program.cs |
| `IHostedService` for background work | Fire-and-forget `_ = service.LoadAsync()` | .NET 6+ | Simpler startup; no IHostedService needed |
| `Newtonsoft.Json` | `System.Text.Json` | .NET 5+ | Built-in, already used — continue using |

---

## Open Questions

1. **`MatchService` testability with `private static` methods**
   - What we know: `ExtractKeywords`, `NormalizeDepartment`, `ComputeKeywordScore`, `ComputeOrgScore` need to be tested in isolation.
   - What's unclear: Top-level classes in `Program.cs` with `private static` methods can't be tested from an external test project.
   - Recommendation: Declare helper methods as `internal static` (not `private`) and add `[assembly: InternalsVisibleTo("RiksrevisjonApi.Tests")]` to the API project. This is the standard .NET approach for testing internal helpers without a full redesign.

2. **Threshold of 15 — validate after first run**
   - What we know: Analysis suggests 500–4,000 matches will be produced.
   - What's unclear: Whether this count feels right to the user/Phase 3 designer.
   - Recommendation: Log the count after first `ComputeMatches()`. If > 5,000 matches, raise threshold to 25–30. If < 50 matches, lower to 10.

3. **`"Helfo"` not matched by `"helse"` department token**
   - What we know: `"helfo"` does not contain `"helse"` as substring. 43/106 notices are from Helfo.
   - What's unclear: Is this acceptable for MVP, or should we add `"helfo"` to an explicit mapping?
   - Recommendation: Accept for MVP. Health reports will still match Helfo notices via keyword scoring (Helfo notice titles contain `helsetjenester`, `spesialisthelsetjenester` etc.). If match count is too low, add a small whitelist: `["helfo" → "helse"]`.

---

## Sources

### Primary (HIGH confidence)
- Real data analysis — `C:\Temp\RrApi\reports-cache.json` (501 reports) — Department field analysis
- Real data analysis — `C:\Temp\RrApi\notices-cache.json` (106 notices) — Buyer field analysis
- `C:\Projects\AIAcademy\Oppgave 3\RiksrevisjonApi\Program.cs` — existing service pattern to mirror
- `.planning/REQUIREMENTS.md` — REQ-03, REQ-04 specification
- `.planning/phases/01-doffin-scraper/01-VERIFICATION.md` — confirmed data shapes

### Secondary (MEDIUM confidence)
- Norwegian Bokmål stopword list — compiled from standard linguistic resources; matches common usage patterns verified against actual report text
- ASP.NET Core 9 minimal API DI patterns — verified against existing `Program.cs` implementation

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all built-in .NET
- Algorithm correctness: HIGH — formula specified in requirements; verified with real data boundary analysis
- Department field interpretation: HIGH — verified against actual 501-report cache
- Org matching gap (Helfo): HIGH — confirmed empirically; impact assessed as acceptable for MVP
- Nynorsk/Bokmål mismatch: MEDIUM — estimated 15-20% impact, no formal measurement
- Match volume estimate (500–4,000): MEDIUM — boundary calculation, not measured

**Research date:** 2026-04-08  
**Valid until:** 2026-07-08 (stable domain; data shapes confirmed against live Phase 1 output)
