---
phase: 02
slug: matching-engine
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-08
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (dotnet new xunit) |
| **Config file** | None — Wave 0 creates `RiksrevisjonApi.Tests/RiksrevisjonApi.Tests.csproj` |
| **Quick run command** | `dotnet test RiksrevisjonApi.Tests --no-build -v q` |
| **Full suite command** | `dotnet test RiksrevisjonApi.Tests` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test RiksrevisjonApi.Tests --no-build -v q`
- **After every plan wave:** Run `dotnet test RiksrevisjonApi.Tests`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-T1 | 01 | 0 | REQ-03/04 | scaffold | `dotnet build RiksrevisjonApi.Tests` | ❌ W0 | ⬜ pending |
| 02-01-T2 | 01 | 1 | REQ-03 | unit | `dotnet test --filter "FullyQualifiedName~KeywordExtraction"` | ❌ W0 | ⬜ pending |
| 02-02-T1 | 02 | 1 | REQ-04 | unit | `dotnet test --filter "FullyQualifiedName~OrgNorm"` | ❌ W0 | ⬜ pending |
| 02-03-T1 | 03 | 2 | REQ-03/04 | unit | `dotnet test --filter "FullyQualifiedName~Scoring"` | ❌ W0 | ⬜ pending |
| 02-03-T2 | 03 | 2 | REQ-04 | unit | `dotnet test --filter "FullyQualifiedName~Threshold"` | ❌ W0 | ⬜ pending |
| 02-04-T1 | 04 | 3 | REQ-03/04 | integration | `dotnet test --filter "FullyQualifiedName~Integration"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `RiksrevisjonApi.Tests/RiksrevisjonApi.Tests.csproj` — xUnit project referencing main project
- [ ] `RiksrevisjonApi.Tests/KeywordExtractionTests.cs` — stubs for REQ-03
- [ ] `RiksrevisjonApi.Tests/OrgNormalisationTests.cs` — stubs for REQ-04
- [ ] `RiksrevisjonApi.Tests/ScoringTests.cs` — stubs for combined formula + threshold
- [ ] `RiksrevisjonApi.Tests/IntegrationTests.cs` — stub for GET /api/matches
- [ ] `InternalsVisibleTo("RiksrevisjonApi.Tests")` added to RiksrevisjonApi.csproj

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Match results "make sense" for a known report | REQ-03 | Subjective relevance judgment | Pick a report about "Helfo" — verify top matches are Helfo procurement notices |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
