# Riksrevisjonen × Doffin Matcher

## What This Is

A matching module that cross-references Riksrevisjonen audit reports with public procurement notices (anbudsdokumenter) from Doffin.no. The goal is to surface whether entities that have received criticism in government audits are also active in public procurement — providing a transparency layer for journalists, researchers and public servants.

## Core Value

> "For every audit finding, show what that government body is currently buying."

## Context

Built on top of an existing dashboard (RiksrevisjonApi .NET 9 + Vue 3) that already scrapes and classifies Riksrevisjonen reports by severity. This module adds a second data source (Doffin.no) and a matching engine.

## Problem

Riksrevisjonen identifies mismanagement. Doffin lists what government bodies are procuring. Today there is no way to see these side by side — a ministry can receive a "Sterkt kritikkverdig" rating and simultaneously be running large procurement processes with no public visibility of that connection.

## Users

- Journalists covering public accountability
- Parliamentary researchers
- Civil servants doing due diligence
- Citizens interested in government transparency

---

## Requirements

### Validated

- ✓ Riksrevisjonen reports scraped and classified by severity — existing
- ✓ Dashboard shows reports grouped by Sterkt kritikkverdig / Kritikkverdig / Ikke tilfredsstillende / Ingen karakter — existing
- ✓ Live data via .NET scraper with JSON file cache — existing

### Active

- [ ] Doffin.no procurement notices fetched via Playwright headless browser (JS-rendered SPA)
- [ ] All sub-pages of Helfo search results scraped (`/search?searchString=helfo`)
- [ ] Keyword matching: report title/summary keywords matched against notice title/description
- [ ] Organisation matching: department/entity names matched across both datasets
- [ ] Match confidence score computed per pair (keyword hits + org name match)
- [ ] Matches visible as new tab/section in existing Vue dashboard
- [ ] Each Riksrevisjonen report card shows count of matched Doffin notices
- [ ] Doffin notices cached to JSON alongside existing reports cache
- [ ] Match results cached and refreshable via POST /api/matches/refresh

### Out of Scope

- ML/semantic matching — too slow for MVP; keyword + org-name matching is sufficient
- Historical Doffin data beyond current search results — pagination to all pages is in scope, but archive scraping is not
- User authentication — public dashboard only
- Alerts/notifications — display only, no push

---

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Playwright for Doffin scraping | Doffin is a React SPA, no public REST API | Pending implementation |
| .NET Playwright (Microsoft.Playwright) | Already a .NET backend — consistent toolchain | Pending |
| Keyword + org-name matching | Simple, auditable, fast — ML is overkill for MVP | Confirmed |
| New tab in existing dashboard | Lowest friction — reuses all existing UI chrome | Confirmed |
| JSON file cache for Doffin data | Consistent with existing report cache pattern | Confirmed |

---

## Architecture Overview

```
RiksrevisjonApi (.NET 9)
  ├── ReportService         — scrapes riksrevisjonen.no (existing)
  ├── DoffinService         — scrapes doffin.no via Playwright (NEW)
  ├── MatchingService       — keyword + org matching engine (NEW)
  └── API endpoints
        GET  /api/reports   — existing
        GET  /api/notices   — Doffin notices (NEW)
        GET  /api/matches   — matched pairs with scores (NEW)
        POST /api/matches/refresh — force re-scrape + rematch (NEW)

riksrevisjon-dashboard (Vue 3)
  ├── App.vue               — existing shell + chart
  ├── views/Dashboard.vue   — existing report grid (refactor)
  └── views/Matcher.vue     — new tab: matches per report (NEW)
```

---

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition:**
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions

---
*Last updated: 2026-04-07 after initialization*
