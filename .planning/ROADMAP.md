# Roadmap: Riksrevisjonen × Doffin Matcher

## Overview

Build a procurement matching module that connects Riksrevisjonen audit reports to related Doffin.no procurement notices. The module scrapes Doffin via Playwright, computes keyword + org-name match scores, and surfaces results in a new dashboard tab with badges on existing report cards.

## Phases

- [x] **Phase 1: Doffin Scraper** - Fetch all procurement notices from Doffin.no using Playwright headless browser, cache in JSON
- [ ] **Phase 2: Matching Engine** - Score each report against notices using keyword + org-name algorithm
- [ ] **Phase 3: Matcher UI** - Add Anbudsmatcher tab to existing Vue dashboard
- [ ] **Phase 4: Dashboard Integration** - Surface match badges on existing report cards

## Phase Details

### Phase 1: Doffin Scraper
**Goal**: Fetch all procurement notices from Doffin.no search results using Playwright, store in JSON cache
**Depends on**: Nothing (first phase)
**Requirements**: [REQ-01, REQ-02]
**Success Criteria** (what must be TRUE):
  1. `GET /api/notices` returns at least 10 notices for searchString=helfo
  2. Each notice has title, buyer, date, url, and description from detail sub-page
  3. Results cached to `notices-cache.json` with 12h TTL
  4. `DoffinService` paginates all pages until no next-page button found
**Plans:** 2 plans

Plans:
- [x] 01-01-PLAN.md — Install Playwright NuGet + implement complete DoffinService class with Notice record
- [x] 01-02-PLAN.md — Wire DI, endpoints, startup load + integration verification

### Phase 2: Matching Engine
**Goal**: For each Riksrevisjonen report, compute matches against Doffin notices using keyword + org-name scoring
**Depends on**: Phase 1
**Requirements**: [REQ-03, REQ-04]
**Success Criteria** (what must be TRUE):
  1. `GET /api/matches` returns matches for all reports that have score > 15
  2. Each match includes noticeId, score, matchedKeywords, matchedOrg fields
  3. Combined score formula: keyword_score × 0.6 + org_score × 0.4
  4. `POST /api/matches/refresh` triggers re-scrape and rematch
**Plans**: 5 plans

Plans:
- [ ] 02-00-PLAN.md — Scaffold xUnit test project + InternalsVisibleTo + 20 stub test files (Wave 0)
- [ ] 02-01-PLAN.md — TDD: MatchService skeleton + internal static ExtractKeywords (Wave 1)
- [ ] 02-02-PLAN.md — TDD: internal static NormalizeDepartment + ComputeOrgScore (Wave 2)
- [ ] 02-03-PLAN.md — TDD: ComputeKeywordScore + ComputeMatches pure-function overload (Wave 3)
- [ ] 02-04-PLAN.md — Complete MatchService (LoadAsync + cache) + DI + endpoints (Wave 4)

### Phase 3: Matcher UI
**Goal**: Add an Anbudsmatcher tab to the existing Vue dashboard showing matched notices per report
**Depends on**: Phase 2
**Requirements**: [REQ-05, REQ-06]
**Success Criteria** (what must be TRUE):
  1. Vue Router installed with routes / (dashboard) and /matcher
  2. Tab navigation visible in header: Rapporter / Anbudsmatcher
  3. Matcher view lists all reports with ≥1 match, sorted by severity then match count
  4. Each report row expandable — shows matched notices with score bar, date, buyer, Doffin link
  5. Score bar colour: green ≥ 60, amber 30–59, red < 30
**Plans**: 4 plans

Plans:
- [ ] 03-01-PLAN.md — Wire vue-router into main.js + App.vue router shell + DashboardView.vue extraction + tab nav
- [ ] 03-02-PLAN.md — Build MatcherView.vue report list (fetch, data join, sort, expand toggle)
- [ ] 03-03-PLAN.md — Add expandable notice rows with score bar, keyword tags, Doffin links
- [ ] 03-04-PLAN.md — Refresh button (POST /api/matches/refresh), error states, document.title, production build

### Phase 4: Dashboard Integration
**Goal**: Surface match count badges on existing report cards in the main dashboard
**Depends on**: Phase 3
**Requirements**: [REQ-07]
**Success Criteria** (what must be TRUE):
  1. Report cards show "N anbudstreff" badge when matches exist
  2. Clicking badge navigates to /matcher filtered to that report
  3. No visual regression on existing dashboard layout
**Plans**: 2 plans

Plans:
- [ ] 04-01-PLAN.md — Fetch /api/matches in DashboardView.vue, build matchCountMap, add RouterLink badge to card-foot
- [ ] 04-02-PLAN.md — Read ?report= query param in MatcherView.vue, filter list, show filter banner with dismiss

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Doffin Scraper | 0/2 | Not started | - |
| 2. Matching Engine | 0/4 | Not started | - |
| 3. Matcher UI | 0/4 | Not started | - |
| 4. Dashboard Integration | 0/2 | Not started | - |

### Phase 5: IT-only filter for reports and notices

**Goal:** Replace broad scraping with IT-only filtering at the backend scrape layer. Riksrevisjonen reports filtered via `q=Digitalisering/ikt`; Doffin notices fetched via 6 IT search terms, deduplicated, and post-filtered to Tjenester contracts only. Non-IT data never stored in cache.
**Requirements**: D-01, D-03, D-04, D-05, D-06, D-07
**Depends on:** Phase 4
**Plans:** 3 plans

Plans:
- [ ] 05-00-PLAN.md — Wave 0: Create three failing test stub files (IT-01 through IT-06) in the test project
- [ ] 05-01-PLAN.md — Wave 1: Add CacheEnvelope/DTOs, doffin-api HttpClient, fix report URL, add IsServiceContract + DeduplicateNotices + versioned cache helpers; fill IT-01/IT-03/IT-04 stubs GREEN
- [ ] 05-02-PLAN.md — Wave 2: Rewrite DoffinService ScrapeAsync with 6-term REST API loop + Tjenester post-filter + version-aware LoadAsync; fill IT-02/IT-05/IT-06 stubs GREEN
