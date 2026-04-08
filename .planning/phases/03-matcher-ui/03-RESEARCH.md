# Phase 3: Matcher UI — Research

**Researched:** 2026-04-08  
**Domain:** Vue 3 + vue-router 4, component architecture, CSS design system extension  
**Confidence:** HIGH

---

## Summary

This phase adds two routes (`/` and `/matcher`) to an existing single-component Vue 3 app. The technical risk is low: `vue-router 4.6.4` is already installed in `node_modules`, the Vite proxy already forwards `/api/*` to `http://localhost:5000`, and the entire design system is already established in `App.vue` via CSS custom properties and Space Mono/Playfair Display fonts.

The primary refactor is: `App.vue` shrinks to a **router shell** (header + `<RouterView>`). All existing dashboard logic moves verbatim to `src/views/DashboardView.vue`. A new `src/views/MatcherView.vue` is created for the matcher tab. Tab navigation lives in the existing `.hdr-inner` block as a `<nav>` using `<RouterLink>` elements styled identically to the existing `.chip` class.

`MatcherView.vue` fetches `/api/reports` and `/api/matches` in parallel via `Promise.all`. It builds a `Map<reportId, Report>` from reports, groups matches by `reportId`, then produces a sorted list (severityOrder first, then match count descending). Expandable rows use `ref(new Set())` toggling — no child component needed. The score bar is CSS-only, using the three colours already present in `severityMeta` (`#e63946`, `#f4a261`, `#52796f`).

**Primary recommendation:** Refactor App.vue into router shell + DashboardView.vue (move, don't rewrite), build MatcherView.vue with Promise.all data join, inline expand toggle, and CSS-only score bar. Zero additional npm packages required.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| REQ-05 | Add Vue Router with two routes: `/` (existing dashboard) and `/matcher`. Add tab navigation in the existing header: "Rapporter" and "Anbudsmatcher". Tabs use the existing light theme styling. | vue-router 4.6.4 already in node_modules. `createRouter + createWebHistory` wired in main.js. RouterLink styled as `.chip` in existing header. |
| REQ-06 | `MatcherView.vue` lists all reports with ≥1 match, sorted by severity then match count descending. Each report row expandable showing matched notices with: title, buyer, date, score bar (green ≥60, amber 30–59, red <30), and link to Doffin notice. | Promise.all join pattern, severityOrder already defined in App.vue, ref(new Set()) expand toggle, CSS-only score bar using existing palette colours. |
</phase_requirements>

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| vue | 3.5.32 | Reactivity, `<script setup>`, `ref`, `computed` | Already in project |
| vue-router | 4.6.4 | SPA routing, `RouterView`, `RouterLink` | Already installed in node_modules |
| vite | 8.0.4 | Build + dev server with `/api` proxy | Already configured |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| @vitejs/plugin-vue | 6.0.5 | SFC compilation | Already in devDependencies |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| CSS-only score bar | A progress component library | No library needed — 3 color classes + width binding is 10 lines of CSS |
| Inline expand (v-if) | Separate ExpandedRow.vue component | Component unnecessary for one-level expand; inline is simpler to read and maintain |
| Promise.all data join | Separate composable | Composable is overkill for a single view fetching 2 endpoints |

**Installation:** No new packages needed — `vue-router` is already in `node_modules`.

---

## Architecture Patterns

### Recommended Project Structure
```
riksrevisjon-dashboard/src/
├── views/
│   ├── DashboardView.vue   # Existing App.vue content moved verbatim
│   └── MatcherView.vue     # New — matcher tab
├── App.vue                 # Becomes router shell: header + <RouterView>
├── main.js                 # Add createRouter, .use(router)
├── style.css               # Unchanged
└── assets/                 # Unchanged
```

No `components/` splitting needed for this phase. Views are self-contained.

---

### Pattern 1: Router Shell in App.vue

**What:** App.vue retains only the `<header class="hdr">` block and a `<RouterView>`. All `<script setup>` state (reports, filters, etc.) moves to `DashboardView.vue`.

**When to use:** Any time a header/footer must persist across all routes.

**Example:**
```vue
<!-- App.vue after refactor -->
<script setup>
import { RouterView, RouterLink } from 'vue-router'
</script>

<template>
  <div class="shell">
    <header class="hdr">
      <div class="hdr-inner">
        <div class="hdr-eye">
          <span class="mono muted">RIKSREVISJONEN</span>
          <span class="sep">·</span>
          <span class="mono muted">{{ new Date().getFullYear() }}</span>
        </div>
        <h1 class="hdr-title">Rapport<em>oversikt</em></h1>
        <p class="hdr-sub mono">Alle offentliggjorte undersøkelser sortert etter alvorlighetsgrad</p>

        <!-- Tab nav — added below existing eyebrow/title/sub -->
        <nav class="hdr-tabs">
          <RouterLink to="/" exact-active-class="active" class="tab mono">Rapporter</RouterLink>
          <RouterLink to="/matcher" active-class="active" class="tab mono">Anbudsmatcher</RouterLink>
        </nav>
      </div>
    </header>

    <RouterView />

    <footer class="foot">
      <span class="mono">Data hentet live fra riksrevisjonen.no</span>
    </footer>
  </div>
</template>
```

**CSS for `.hdr-tabs` and `.tab`** (added to App.vue `<style>`):
```css
.hdr-tabs {
  display: flex;
  gap: 0.25rem;
  margin-top: 1.5rem;
}
.tab {
  display: inline-flex;
  align-items: center;
  padding: 0.3rem 0.8rem;
  background: transparent;
  border: 1px solid var(--border);
  border-radius: 2px;
  color: var(--muted);
  font-size: 0.6rem;
  letter-spacing: 0.08em;
  text-decoration: none;
  transition: all 0.15s ease;
}
.tab:hover { border-color: var(--muted); color: var(--text); }
.tab.active {
  background: var(--surf2);
  border-color: var(--text);
  color: var(--text);
}
```

**Important:** Use `exact-active-class="active"` on the `/` route link so it doesn't stay active when on `/matcher`. Use plain `active-class="active"` on `/matcher`.

---

### Pattern 2: main.js Router Wiring

**What:** Add `createRouter` + `createWebHistory` to main.js, wire `DashboardView` and `MatcherView`.

```js
// main.js — full replacement
import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import './style.css'
import App from './App.vue'
import DashboardView from './views/DashboardView.vue'
import MatcherView from './views/MatcherView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/',        component: DashboardView },
    { path: '/matcher', component: MatcherView   },
  ],
})

createApp(App).use(router).mount('#app')
```

---

### Pattern 3: MatcherView Data Join

**What:** Fetch both endpoints in parallel, build a Map for O(1) report lookup, group matches by reportId.

**When to use:** Any view that must join two separate API responses.

```vue
<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'

const reports   = ref([])
const matches   = ref([])
const loading   = ref(true)
const error     = ref(null)
let pollTimer   = null

const severityOrder = {
  'Sterkt kritikkverdig': 0,
  'Kritikkverdig':        1,
  'Ikke tilfredsstillende': 2,
  'Ingen karakter':       3,
}

async function fetchAll() {
  try {
    const [rRes, mRes] = await Promise.all([
      fetch('/api/reports'),
      fetch('/api/matches'),
    ])
    const rData = await rRes.json()
    const mData = await mRes.json()

    reports.value = rData.reports ?? []
    matches.value = mData.matches ?? []

    // Poll while either is still loading
    if (rData.loading || mData.loading) {
      pollTimer = setTimeout(fetchAll, 2000)
    } else {
      loading.value = false
    }
  } catch (e) {
    error.value = 'Kunne ikke hente data.'
    loading.value = false
  }
}

onMounted(() => fetchAll())
onUnmounted(() => clearTimeout(pollTimer))

// Join: group matches by reportId, then join with reports
const matchedReports = computed(() => {
  const reportMap = new Map(reports.value.map(r => [r.id, r]))

  // Group matches by reportId
  const grouped = new Map()
  for (const m of matches.value) {
    if (!grouped.has(m.reportId)) grouped.set(m.reportId, [])
    grouped.get(m.reportId).push(m)
  }

  // Build rows — only reports with ≥1 match
  const rows = []
  for (const [reportId, noticeMatches] of grouped) {
    const report = reportMap.get(reportId)
    if (!report) continue
    rows.push({ report, noticeMatches })
  }

  // Sort: severityOrder ASC, then match count DESC
  rows.sort((a, b) => {
    const sevDiff =
      (severityOrder[a.report.severity] ?? 99) -
      (severityOrder[b.report.severity] ?? 99)
    if (sevDiff !== 0) return sevDiff
    return b.noticeMatches.length - a.noticeMatches.length
  })

  return rows
})
</script>
```

---

### Pattern 4: Expandable Rows with ref(new Set())

**What:** Toggle expanded state for report rows using a reactive Set.

**Key insight:** `ref(new Set())` does not trigger reactivity on `.add()` / `.delete()` alone — you must reassign to trigger Vue's dependency tracking.

```vue
<script setup>
const expandedIds = ref(new Set())

function toggleRow(id) {
  const s = new Set(expandedIds.value)  // clone
  s.has(id) ? s.delete(id) : s.add(id)
  expandedIds.value = s                  // reassign → triggers reactivity
}
</script>

<template>
  <div
    v-for="row in matchedReports"
    :key="row.report.id"
    class="match-row"
    @click="toggleRow(row.report.id)"
  >
    <!-- Summary row -->
    <div class="match-summary">
      <span class="mono">{{ row.report.title }}</span>
      <span class="match-count mono">{{ row.noticeMatches.length }} treff</span>
      <span class="expand-icon">{{ expandedIds.has(row.report.id) ? '▲' : '▼' }}</span>
    </div>

    <!-- Expanded notices -->
    <div v-if="expandedIds.has(row.report.id)" class="match-notices">
      <div v-for="m in row.noticeMatches" :key="m.noticeId" class="notice-row">
        <!-- notice content here -->
      </div>
    </div>
  </div>
</template>
```

**Alternative:** Use a simple `expandedId = ref(null)` (only one open at a time). Use `Set` when multiple rows should stay open simultaneously.

---

### Pattern 5: CSS-Only Score Bar

**What:** A progress-bar-style score indicator using computed width + colour class. No library needed.

**Colour thresholds (REQ-06):** green ≥60, amber 30–59, red <30. Colours reuse existing `severityMeta` palette.

```vue
<script setup>
function scoreColor(score) {
  if (score >= 60) return 'score-green'
  if (score >= 30) return 'score-amber'
  return 'score-red'
}
</script>

<template>
  <div class="score-bar">
    <div
      class="score-fill"
      :class="scoreColor(m.score)"
      :style="{ width: m.score + '%' }"
    ></div>
  </div>
  <span class="mono score-label">{{ Math.round(m.score) }}</span>
</template>
```

```css
.score-bar {
  height: 6px;
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: 2px;
  overflow: hidden;
  width: 80px;
  flex-shrink: 0;
}
.score-fill {
  height: 100%;
  border-radius: 2px;
  transition: width 0.4s ease;
}
.score-green { background: #52796f; }   /* reuses severityMeta 'Ingen karakter' color */
.score-amber { background: #f4a261; }   /* reuses severityMeta 'Kritikkverdig' color */
.score-red   { background: #e63946; }   /* reuses severityMeta 'Sterkt kritikkverdig' color */
.score-label { font-size: 0.6rem; color: var(--muted); }
```

---

### Pattern 6: Oppdater-data Refresh Button

**What:** A button that calls a manual refresh endpoint and restarts polling.

```vue
<script setup>
const refreshing = ref(false)

async function refreshData() {
  refreshing.value = true
  try {
    await fetch('/api/matches/refresh', { method: 'POST' })
    loading.value = true
    matches.value = []
    clearTimeout(pollTimer)
    pollTimer = setTimeout(fetchAll, 500)
  } finally {
    refreshing.value = false
  }
}
</script>

<template>
  <button class="chip" :disabled="refreshing" @click="refreshData">
    {{ refreshing ? 'Oppdaterer…' : 'Oppdater data' }}
  </button>
</template>
```

---

### Pattern 7: Polling While Loading (established project pattern)

**What:** Same `setTimeout` pattern already used in App.vue's `fetchReports`. Mirror exactly.

```js
// Poll every 2s while backend is still computing
if (rData.loading || mData.loading) {
  pollTimer = setTimeout(fetchAll, 2000)
} else {
  loading.value = false
}
```

`onUnmounted(() => clearTimeout(pollTimer))` — already the project pattern. Use it.

---

### Anti-Patterns to Avoid

- **Don't use `<a href="/matcher">` for navigation** — bypasses Vue Router, causes full page reload. Use `<RouterLink to="/matcher">` always.
- **Don't mutate a `ref(new Set())` in-place** — `.add()` and `.delete()` don't trigger Vue reactivity. Always reassign: `expandedIds.value = new Set(expandedIds.value)`.
- **Don't fetch /api/reports again if DashboardView already fetched it** — MatcherView must fetch it independently (views are mounted/unmounted separately; there is no shared store in this phase).
- **Don't hardcode `#52796f` for green** — define `.score-green { background: #52796f }` as a named CSS class so it's readable and maintainable.
- **Don't split DashboardView logic** — move App.vue content verbatim; resist the urge to refactor the dashboard during this phase.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Client-side routing | Custom history manipulation | `vue-router` 4 (already installed) | Handles popstate, hash/history mode, active link classes |
| Score percentage bar | SVG arc or canvas element | CSS `width: score%` + coloured div | CSS is simpler, transitions work, no deps |
| Data join/group | Lodash groupBy | Native `Map` + `for...of` | No lodash in project; Map is O(1) lookup |

---

## Common Pitfalls

### Pitfall 1: `exact-active-class` on `/` route
**What goes wrong:** Without `exact-active-class`, the `/` RouterLink stays `.active` on every route (since every path starts with `/`).  
**Why it happens:** Vue Router's default `active-class` matches any route that shares the path prefix.  
**How to avoid:** Use `exact-active-class="active"` (not `active-class`) on the `/` tab link.  
**Warning signs:** Both tabs highlighted when navigating to `/matcher`.

### Pitfall 2: ref(new Set()) reactivity
**What goes wrong:** `expandedIds.value.add(id)` mutates in-place — Vue does not detect the change, rows don't expand/collapse.  
**Why it happens:** Vue 3 `ref()` wraps the Set in a Proxy, but `.add()` and `.delete()` are mutations, not replacements.  
**How to avoid:** Always clone and reassign: `expandedIds.value = new Set(expandedIds.value)` after mutation.  
**Warning signs:** Click handler runs (confirmed with console.log) but UI doesn't update.

### Pitfall 3: Footer in DashboardView duplicates shell footer
**What goes wrong:** The existing App.vue `<footer class="foot">` gets moved to DashboardView but the router shell App.vue also adds a footer — double footer.  
**Why it happens:** Moving code from App.vue to DashboardView while also keeping the footer in App.vue.  
**How to avoid:** Footer stays in `App.vue` router shell (always visible). Remove it from DashboardView. The footer content references `reports.length` — update it to a generic message or pass a slot.  
**Warning signs:** Two footers visible on the dashboard route.

### Pitfall 4: `/api/matches` returns empty array vs loading:true
**What goes wrong:** If the backend hasn't computed matches yet, it returns `{ loading: true, matches: [] }`. Treating `matches: []` as "no matches" causes a premature empty state.  
**Why it happens:** Checking `matches.value.length === 0` without also checking `loading.value`.  
**How to avoid:** Show loading state while `loading.value === true`, even if `matches.value` is already empty.  
**Warning signs:** "Ingen treff" flash before matches populate.

### Pitfall 5: DashboardView's footer reference to `reports.length`
**What goes wrong:** The current footer in App.vue references `reports.value.length` — this state moves to DashboardView. If the footer stays in App.vue, it needs its own data or a prop.  
**How to avoid:** Move the footer into DashboardView alongside its data, OR give App.vue a generic footer with no dynamic count. Simplest: move footer to DashboardView, give App.vue no footer, MatcherView gets its own footer.

---

## Vite Proxy Configuration

**Already configured — no changes needed:**

```js
// vite.config.js (current — verified)
export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': 'http://localhost:5000'
    }
  }
})
```

Both `/api/reports` and `/api/matches` are covered by the existing `/api` prefix proxy. The new `/api/matches/refresh` endpoint (POST) is also covered automatically.

---

## Code Examples

### Complete main.js after router wiring
```js
import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import './style.css'
import App from './App.vue'
import DashboardView from './views/DashboardView.vue'
import MatcherView from './views/MatcherView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/',        component: DashboardView },
    { path: '/matcher', component: MatcherView   },
  ],
})

createApp(App).use(router).mount('#app')
```

### MatcherView.vue skeleton
```vue
<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'

const reports = ref([])
const matches = ref([])
const loading = ref(true)
const error   = ref(null)
const expandedIds = ref(new Set())
let pollTimer = null

const severityOrder = {
  'Sterkt kritikkverdig': 0,
  'Kritikkverdig':        1,
  'Ikke tilfredsstillende': 2,
  'Ingen karakter':       3,
}

async function fetchAll() {
  try {
    const [rRes, mRes] = await Promise.all([fetch('/api/reports'), fetch('/api/matches')])
    const rData = await rRes.json()
    const mData = await mRes.json()
    reports.value = rData.reports ?? []
    matches.value = mData.matches ?? []
    if (rData.loading || mData.loading) {
      pollTimer = setTimeout(fetchAll, 2000)
    } else {
      loading.value = false
    }
  } catch (e) {
    error.value = 'Kunne ikke hente data.'
    loading.value = false
  }
}

function toggleRow(id) {
  const s = new Set(expandedIds.value)
  s.has(id) ? s.delete(id) : s.add(id)
  expandedIds.value = s
}

function scoreColor(score) {
  if (score >= 60) return 'score-green'
  if (score >= 30) return 'score-amber'
  return 'score-red'
}

function fmtDate(d) {
  if (!d) return ''
  return new Date(d).toLocaleDateString('nb-NO', { day: 'numeric', month: 'short', year: 'numeric' })
}

const matchedReports = computed(() => {
  const reportMap = new Map(reports.value.map(r => [r.id, r]))
  const grouped = new Map()
  for (const m of matches.value) {
    if (!grouped.has(m.reportId)) grouped.set(m.reportId, [])
    grouped.get(m.reportId).push(m)
  }
  const rows = []
  for (const [reportId, noticeMatches] of grouped) {
    const report = reportMap.get(reportId)
    if (!report) continue
    rows.push({ report, noticeMatches })
  }
  rows.sort((a, b) => {
    const sevDiff = (severityOrder[a.report.severity] ?? 99) - (severityOrder[b.report.severity] ?? 99)
    return sevDiff !== 0 ? sevDiff : b.noticeMatches.length - a.noticeMatches.length
  })
  return rows
})

onMounted(() => fetchAll())
onUnmounted(() => clearTimeout(pollTimer))
</script>
```

### App.vue hdr-tabs CSS (append to existing `<style>`)
```css
.hdr-tabs {
  display: flex;
  gap: 0.25rem;
  margin-top: 1.5rem;
}
.tab {
  display: inline-flex;
  align-items: center;
  padding: 0.3rem 0.8rem;
  background: transparent;
  border: 1px solid var(--border);
  border-radius: 2px;
  color: var(--muted);
  font-family: 'Space Mono', monospace;
  font-size: 0.6rem;
  letter-spacing: 0.08em;
  text-decoration: none;
  transition: all 0.15s ease;
}
.tab:hover { border-color: var(--muted); color: var(--text); }
.tab.active { background: var(--surf2); border-color: var(--text); color: var(--text); }
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|---|---|---|
| `createApp(App).mount('#app')` (current) | Add `.use(router)` before `.mount()` | Single line change in main.js |
| `vue-router` 3 (Vue 2 era) | `vue-router` 4 (Vue 3) — `createRouter()` API | Already on v4; composition API friendly |
| Options API `$route` / `$router` | Composition API `useRoute()` / `useRouter()` | Not needed this phase; note for Phase 4 |

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node.js | Vite dev server | ✓ | 25.8.0 | — |
| vue-router | REQ-05 routing | ✓ | 4.6.4 (in node_modules) | — |
| .NET backend | `/api/matches`, `/api/reports` | ✓ (via existing proxy) | — | Mock data in fetchAll() |
| Vite proxy `/api/*` | All API calls | ✓ | Configured in vite.config.js | — |

**Missing dependencies with no fallback:** None.  
**Missing dependencies with fallback:**
- Backend API (`/api/matches`) may return `loading: true` or empty array if Phase 2 is not yet complete — MatcherView must gracefully handle both states (empty state message).

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | None currently installed — Vitest recommended for Wave 0 |
| Config file | None — needs `vitest.config.js` or merged into `vite.config.js` |
| Quick run command | `npx vitest run --reporter=verbose` |
| Full suite command | `npx vitest run` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-05 | Routes `/` and `/matcher` exist and resolve | smoke | Manual browser check — router config verified visually | ❌ Wave 0 |
| REQ-06 | `matchedReports` computed: join + sort by severity then count | unit | `npx vitest run tests/matcherView.spec.js` | ❌ Wave 0 |
| REQ-06 | `scoreColor(score)` returns correct class | unit | `npx vitest run tests/matcherView.spec.js` | ❌ Wave 0 |
| REQ-06 | Expand toggle: `toggleRow(id)` adds/removes from Set | unit | `npx vitest run tests/matcherView.spec.js` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `npx vitest run tests/matcherView.spec.js` (pure function tests only)
- **Per wave merge:** `npx vitest run`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `tests/matcherView.spec.js` — covers REQ-06 computed logic (join, sort, scoreColor, toggleRow)
- [ ] Framework install: `npm install -D vitest` in `riksrevisjon-dashboard/`
- [ ] Add `test` script to `package.json`: `"test": "vitest run"`

**Note:** Vue component mount testing (@vue/test-utils) is NOT required for this phase. The testable logic (`matchedReports` computed, `scoreColor`, `toggleRow`) can be extracted as pure functions and tested without mounting the component, keeping the test setup minimal.

---

## Open Questions

1. **Footer ownership after refactor**  
   - What we know: Current App.vue footer shows `reports.length` — state that moves to DashboardView  
   - What's unclear: Should App.vue footer be generic (no count) or should MatcherView provide its own footer?  
   - Recommendation: Move footer entirely to DashboardView. MatcherView gets its own minimal footer. App.vue has no footer.

2. **`/api/matches` response shape confirmation**  
   - What we know: From REQUIREMENTS.md — `{loading: bool, matches: [{reportId, noticeId, score, matchedKeywords, matchedOrg}]}`  
   - What's unclear: Are `matchedKeywords` an array of strings? Is `matchedOrg` a boolean or a string?  
   - Recommendation: The UI plan should treat `matchedKeywords` as `string[]` and `matchedOrg` as `string | null`. Planner should note to verify against Phase 2 implementation.

3. **Notice title/buyer/date in matches response**  
   - What we know: The matches response includes `noticeId` — not the notice title/buyer/date  
   - What's unclear: Does `/api/matches` also return notice detail fields, or does MatcherView need to join with a `/api/notices` endpoint?  
   - Recommendation: Plan should include a `/api/notices` fetch OR confirm that `/api/matches` is enriched with notice detail fields (title, buyer, date, url). REQ-06 requires displaying these — they must come from somewhere. **This is a critical question for the planner to resolve in 03-04.**

---

## Sources

### Primary (HIGH confidence)
- Direct file read: `riksrevisjon-dashboard/package.json` — confirmed vue-router 4.6.4 in dependencies  
- Direct file read: `riksrevisjon-dashboard/node_modules/vue-router/package.json` — confirmed 4.6.4 installed  
- Direct file read: `riksrevisjon-dashboard/src/App.vue` — confirmed design system, CSS vars, polling pattern  
- Direct file read: `riksrevisjon-dashboard/src/main.js` — confirmed no router yet  
- Direct file read: `riksrevisjon-dashboard/vite.config.js` — confirmed `/api` proxy already configured  
- Direct file read: `.planning/REQUIREMENTS.md` — confirmed REQ-05, REQ-06 spec  

### Secondary (MEDIUM confidence)
- Vue Router 4 official docs (https://router.vuejs.org/) — `exact-active-class` behaviour, `createWebHistory`, RouterLink API  
- Vue 3 reactivity docs — `ref(new Set())` mutation behaviour  

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — vue-router 4.6.4 verified in node_modules, no new packages needed
- Architecture: HIGH — direct inspection of App.vue, main.js, vite.config.js; patterns are straightforward
- Pitfalls: HIGH — exact-active-class and Set reactivity are well-known Vue 3 gotchas verified against official docs
- Open Question 3 (notice detail fields): LOW — API shape for MatcherView notices not fully confirmed; depends on Phase 2 output

**Research date:** 2026-04-08  
**Valid until:** 2026-05-08 (stable stack; only risk is Phase 2 API shape change)
