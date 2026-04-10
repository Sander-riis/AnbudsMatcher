---
plan: "05-01"
phase: "05-it-only-filter-for-reports-and-notices"
status: complete
wave: 1
commit: c0d023f
---

# Summary: Plan 05-01 — IT Filter Infrastructure

## What was built

All internal APIs, DTOs, cache infrastructure, and helper methods for Phase 5 IT-only filtering. Three Wave 0 stubs (IT-01, IT-03, IT-04) are now PASSING.

## Changes to Program.cs

| Change | Detail |
|--------|--------|
| `doffin-api` HttpClient | Registered with `https://api.doffin.no/webclient/api/v2/` |
| `FetchListPage` URL fix | Now uses `?q=Digitalisering/ikt&p={page}` |
| `ParseListPage` visibility | Changed from `static` → `internal static` |
| `CacheEnvelope<T>` record | `Version + Data` envelope for versioned caching |
| `DoffinSearchResult/Hit/Buyer` DTOs | Doffin REST API response deserialization |
| `DoffinApiOpts` | `PropertyNameCaseInsensitive = true` for API responses |
| `SearchTerms` + `SearchVersion` | IT-focused search terms, version fingerprint |
| `TryLoadCache` (versioned) | Rejects cache if `env.Version != SearchVersion` |
| `SaveCache` (versioned) | Wraps in `CacheEnvelope<Notice>(SearchVersion, ...)` |
| `IsServiceContract` | Handles both "Kontraktens art"/"Tjenester" and "Nature of the contract"/"Services" |
| `DeduplicateNotices` | Dedup by URL, sequential ID reassignment from 1 |
| `CheckVersionMismatch` | Internal static — checks cache version envelope |
| `PurgeAllCaches` | Internal static — deletes all 3 cache files |

## Test outcome

- **IT-01, IT-03, IT-04: PASS** (ItFilterTests fully GREEN)
- **IT-02, IT-06: FAIL** (DoffinDeduplicationTests — pending Wave 2)
- **IT-05: FAIL** (CacheVersionTests — pending Wave 2)
- **Existing 20 tests: all PASS**
- Total: 23 passed, 3 failed

## Decisions

- `CheckVersionMismatch` uses `JsonSerializer.Deserialize<CacheEnvelope<Notice>>(json)` without opts (IT-05 test uses temp files)
- Empty-URL notices skipped in `DeduplicateNotices` to avoid false collisions
