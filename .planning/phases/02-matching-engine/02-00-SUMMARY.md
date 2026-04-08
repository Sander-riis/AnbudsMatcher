---
plan: 02-00
phase: 02-matching-engine
status: complete
completed: 2026-04-08
self_check: PASSED
---

## Summary

Scaffolded the xUnit test project that all Phase 2 plans depend on. Created 20 stub tests in RED state.

## What Was Built

- `RiksrevisjonApi.Tests/` xUnit project (net9.0) with ProjectReference to main API
- `InternalsVisibleTo` assembly attribute enables test access to internal MatchService members
- 4 test files with 20 `[Fact]` stub methods — all throw `NotImplementedException` (RED state)

## Key Files

- `RiksrevisjonApi.Tests/RiksrevisjonApi.Tests.csproj` — test project with ProjectReference
- `RiksrevisjonApi/RiksrevisjonApi.csproj` — InternalsVisibleTo attribute added
- `RiksrevisjonApi.Tests/KeywordExtractionTests.cs` — 6 stubs
- `RiksrevisjonApi.Tests/OrgNormalisationTests.cs` — 5 stubs
- `RiksrevisjonApi.Tests/ScoringTests.cs` — 6 stubs
- `RiksrevisjonApi.Tests/IntegrationTests.cs` — 3 stubs

## Verification

- `dotnet build RiksrevisjonApi.Tests` exits 0 ✓
- `dotnet test RiksrevisjonApi.Tests` → 20 Failed, 0 Passed (RED state confirmed) ✓
- UnitTest1.cs deleted ✓
