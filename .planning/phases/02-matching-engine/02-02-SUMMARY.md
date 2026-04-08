---
plan: 02-02
phase: 02-matching-engine
status: complete
completed: 2026-04-08
self_check: PASSED
---

## Summary

TDD implementation of `NormalizeDepartment` and `ComputeOrgScore` — org-name normalizer for REQ-04. All 5 tests GREEN.

## What Was Built

- `NormalizeDepartment(string? department)`: strips trailing `·`, splits on `/`, splits on space (takes first word), lowercases
- `ComputeOrgScore(string normDept, Notice notice)`: returns (100.0, normDept) if buyer contains normDept, else (0.0, null)
- OrgNormalisationTests: 5 stubs replaced with real assertions

## Deviation

Algorithm spec said "split on '/'" only, but test data showed multi-word categories ("Offentlig forvaltning" → "offentlig", "Statens eierstyring" → "statens"). Added space-splitting to extract the primary category token.

## Verification

- `dotnet test --filter OrgNorm` → 5 Passed, 0 Failed ✓
- `dotnet test --filter KeywordExtraction` → 6 Passed, 0 Failed (no regression) ✓
- `dotnet build RiksrevisjonApi.Tests` exits 0 ✓
