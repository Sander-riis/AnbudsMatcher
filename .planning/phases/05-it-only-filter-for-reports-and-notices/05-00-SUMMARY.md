---
plan: "05-00"
phase: "05-it-only-filter-for-reports-and-notices"
status: complete
wave: 0
commit: b0932a1
---

# Summary: Plan 05-00 — Wave 0 Test Stubs

## What was built

Three failing test stub files establishing the Nyquist contract for Phase 5.

## Files created

| File | Tests | Purpose |
|------|-------|---------|
| `RiksrevisjonApi.Tests/ItFilterTests.cs` | IT-01, IT-03, IT-04 | ParseListPage + IsServiceContract stubs |
| `RiksrevisjonApi.Tests/DoffinDeduplicationTests.cs` | IT-02, IT-06 | DeduplicateNotices stubs |
| `RiksrevisjonApi.Tests/CacheVersionTests.cs` | IT-05 | CheckVersionMismatch + PurgeAllCaches stub |

## Test outcome

- **6 FAILED stubs** (IT-01 through IT-06) — all use `Assert.Fail("Wave 0 stub — …")`
- **20 existing tests PASS** (IntegrationTests, ScoringTests, KeywordExtractionTests, OrgNormalisationTests)
- Build: `dotnet build` exits 0 — no compile errors

## Decisions

- All stubs use `Assert.Fail` only — no references to non-existent internal methods
- No `using` directives needed (xUnit globally imported via .csproj)
- Wave 1 (Plan 05-01) will replace stubs with real assertions as production code is added
