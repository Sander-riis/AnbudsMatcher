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
**Plans**: TBD

Plans:
- [ ] 02-01: Implement keyword extractor with Norwegian stopword list
- [ ] 02-02: Implement org-name normaliser (strip AS/departement suffixes)
- [ ] 02-03: Implement scoring algorithm + threshold filter (score > 15)
- [ ] 02-04: Wire GET /api/matches and POST /api/matches/refresh endpoints

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
**Plans**: TBD

Plans:
- [ ] 03-01: Install vue-router and scaffold /matcher route
- [ ] 03-02: Build MatcherView.vue — report list with match counts
- [ ] 03-03: Build expandable notice rows with confidence score bar
- [ ] 03-04: Wire to /api/matches + polling, add Oppdater-data button, empty/error states

### Phase 4: Dashboard Integration
**Goal**: Surface match count badges on existing report cards in the main dashboard
**Depends on**: Phase 3
**Requirements**: [REQ-07]
**Success Criteria** (what must be TRUE):
  1. Report cards show "N anbudstreff" badge when matches exist
  2. Clicking badge navigates to /matcher filtered to that report
  3. No visual regression on existing dashboard layout
**Plans**: TBD

Plans:
- [ ] 04-01: Fetch /api/matches in App.vue and pass counts to report cards
- [ ] 04-02: Add badge component with deep-link to /matcher?report={id}

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Doffin Scraper | 0/2 | Not started | - |
| 2. Matching Engine | 0/4 | Not started | - |
| 3. Matcher UI | 0/4 | Not started | - |
| 4. Dashboard Integration | 0/2 | Not started | - |
