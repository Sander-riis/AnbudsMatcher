---
phase: 1
slug: doffin-scraper
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-07
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | None — no test projects exist in this codebase |
| **Config file** | none |
| **Quick run command** | `curl http://localhost:5000/api/notices` |
| **Full suite command** | `curl http://localhost:5000/api/notices` + JSON inspection |
| **Estimated runtime** | ~120–300 seconds (Playwright scrape on first run) |

---

## Sampling Rate

- **After every task commit:** Publish → restart → `curl http://localhost:5000/api/notices` → verify JSON shape
- **After every plan wave:** Full scrape cycle → verify ≥10 notices with all fields populated
- **Before `/gsd-verify-work`:** Full suite must be green (loading: false, ≥10 notices)
- **Max feedback latency:** 300 seconds (first-run Playwright + 6 pages + 106 detail pages)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 01-01-T1 | 01-01 | 1 | REQ-01, REQ-02 | build | `dotnet build RiksrevisjonApi` outputs "Build succeeded" | ✅ | ⬜ pending |
| 01-01-T2 | 01-01 | 1 | REQ-01, REQ-02 | build | `dotnet build RiksrevisjonApi` outputs "Build succeeded" with DoffinService | ✅ | ⬜ pending |
| 01-02-T1 | 01-02 | 2 | REQ-01, REQ-02 | build | `dotnet build RiksrevisjonApi` outputs "Build succeeded" | ✅ | ⬜ pending |
| 01-02-T2 | 01-02 | 2 | REQ-01, REQ-02 | smoke | `curl http://localhost:5000/api/notices` returns ≥10 notices | ❌ Manual | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

No test framework exists in this project — verification is manual HTTP testing. This is consistent with the existing codebase (zero test infrastructure). Adding a test framework is out of scope for Phase 1.

*Existing infrastructure covers all phase requirements via manual smoke testing.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| GET /api/notices returns ≥10 notices with title, buyer, date, url | REQ-01 | No test framework in project | Publish, start server, `curl http://localhost:5000/api/notices`, inspect JSON array length and fields |
| Each notice has description from detail page; cached to notices-cache.json | REQ-02 | No test framework in project | After scrape completes, `Test-Path C:\Temp\RrApi\notices-cache.json` and inspect description fields |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 300s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
