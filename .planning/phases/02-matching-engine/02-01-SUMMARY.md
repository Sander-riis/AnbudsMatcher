---
plan: 02-01
phase: 02-matching-engine
status: complete
completed: 2026-04-08
self_check: PASSED
---

## Summary

TDD implementation of `ExtractKeywords` — Norwegian keyword tokenizer for REQ-03. All 6 tests GREEN.

## What Was Built

- `record Match(int ReportId, int NoticeId, double Score, string[] MatchedKeywords, string? MatchedOrg)` added to Program.cs
- `MatchService` class skeleton with `_matches`, `IsLoading`, `Matches` property
- `Stopwords` HashSet (~80 Norwegian stopwords including domain-specific terms)
- `TokenRegex` compiled regex for non-letter splitting
- `internal static ExtractKeywords(string? text)`: null guard → lowercase → split → filter length ≥3 → filter stopwords → Distinct
- Fixed regex `foreach (Match ...)` to use fully qualified `System.Text.RegularExpressions.Match` (naming conflict with new record)

## Verification

- `dotnet test --filter KeywordExtraction` → 6 Passed, 0 Failed ✓
- `dotnet build RiksrevisjonApi.Tests` exits 0 ✓
- Other 14 stub tests still compile and fail with NotImplementedException ✓
