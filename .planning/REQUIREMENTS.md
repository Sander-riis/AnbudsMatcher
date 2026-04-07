# Requirements: Riksrevisjonen × Doffin Matcher

## Functional Requirements

### REQ-01: Doffin List Page Scraping
**Phase**: 1
Scrape all procurement notices from `doffin.no/search?searchString=helfo` using Playwright headless Chromium. Paginate all result pages until no next-page button is found. Each notice must include: title, buyer, publication date, notice URL.

### REQ-02: Doffin Notice Detail Scraping
**Phase**: 1
For each notice from list pages, fetch the detail sub-page to extract full description text. Fetch in parallel with `SemaphoreSlim(5)` to avoid overloading the server. Cache results to `notices-cache.json` (12h TTL, same pattern as `reports-cache.json`).

### REQ-03: Keyword Matching
**Phase**: 2
Extract keywords from each Riksrevisjonen report's title and summary, filtered through a Norwegian stopword list. Match against Doffin notice titles and descriptions. Keyword score = (matching keywords / total report keywords) × 100.

### REQ-04: Organisation Name Matching
**Phase**: 2
Extract organisation name from report `Department` field. Normalise by stripping suffixes (AS, departementet, direktoratet, etc.). Match against notice `buyer` and `title` fields. Combined score = keyword_score × 0.6 + org_score × 0.4. Only return matches with combined score > 15.

### REQ-05: Matcher Tab Navigation
**Phase**: 3
Add Vue Router with two routes: `/` (existing dashboard) and `/matcher`. Add tab navigation in the existing header: "Rapporter" and "Anbudsmatcher". Tabs use the existing light theme styling.

### REQ-06: Matcher View
**Phase**: 3
`MatcherView.vue` lists all reports with ≥1 match, sorted by severity (sterkt kritikkverdig first) then match count descending. Each report row is expandable showing matched notices with: title, buyer, date, score bar (green ≥60, amber 30–59, red <30), and link to Doffin notice.

### REQ-07: Dashboard Badge Integration
**Phase**: 4
Existing report cards in `App.vue` show a "N anbudstreff" badge when the report has ≥1 Doffin match. Clicking the badge navigates to `/matcher` with the report pre-filtered. No visual regression on existing card layout.

---

## Out of Scope (Post-MVP Backlog)

- Configurable search terms beyond "helfo" (999.1)
- Semantic / embedding-based matching (999.2)
- Historical match trend tracking (999.3)
- CSV export (999.4)
- Authentication / access control
- Persistent database (SQLite / PostgreSQL)
