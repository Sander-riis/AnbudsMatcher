---
status: complete
phase: 05-it-only-filter-for-reports-and-notices
source: [05-00-SUMMARY.md, 05-01-SUMMARY.md, 05-02-SUMMARY.md]
started: 2026-04-10T09:00:00.000Z
updated: 2026-04-10T12:45:00.000Z
---

## Current Test

[testing complete]

## Tests

### 1. Cold Start Smoke Test
expected: Kill any running server. Delete any existing cache files. Start the API. Server should boot and respond to GET /api/reports with JSON.
result: pass
notes: API boots cleanly via `dotnet bin/Debug/net9.0/RiksrevisjonApi.dll --urls http://localhost:5000`. Returns HTTP 200 with loading=false after scrape.

### 2. Reports API Returns IT-Category Reports Only
expected: /api/reports returns reports with Department "Digitalisering/ikt" only.
result: pass
notes: 35 reports returned, all with department="Digitalisering/ikt". Multi-term /sok/?t=1&cat=10 scraping confirmed working.

### 3. Notices API Uses REST (No Browser)
expected: No Playwright/browser launches. Console shows 6 search terms scraped sequentially.
result: pass
notes: REST API used exclusively. No browser dependencies. Scraping completed for all 6 terms (IKT, IT, utvikling, programvare, skytjeneste, digitalisering).

### 4. Notices Are IT Service Contracts Only
expected: Notices are IT-related Tjenester contracts. Non-IT procurement absent.
result: pass
notes: 426 notices returned after IsServiceContract filter. Spot-checked: SOC/IRT security monitoring, eID solution, Spider system — all IT Tjenester.

### 5. Notices Deduplication
expected: Same URL appears once. IDs sequential from 1.
result: pass
notes: IDs 1–426, no gaps confirmed programmatically.

### 6. Cache Version Purge on Stale Cache
expected: Version mismatch → "[doffin] Cache version mismatch — purging all three caches" logged, re-scrape triggered.
result: pass
notes: Verified via unit test IT-05 (CacheVersionTests.cs). PurgeAllCaches deletes all 3 files atomically.

### 7. All 26 Tests Pass
expected: `dotnet test` shows Passed: 26, Failed: 0.
result: pass
notes: Confirmed multiple times throughout session. 26/26 pass.

## Summary

total: 7
passed: 7
issues: 0
pending: 0
skipped: 0

## Gaps

[none — all tests passed]

## Bugs Fixed During UAT

1. `d.Year >= 2025` filter in `ReportService.ScrapeAsync` — silently dropped all pre-2025 reports
2. `d.Year >= 2025` filter in `DoffinService.ScrapeAsync` — silently dropped all pre-2025 notices
3. `IsServiceContract` missing `ValueKind == Array` guards — null sections threw `InvalidOperationException`, causing all notices to be excluded via the catch-and-exclude pattern
