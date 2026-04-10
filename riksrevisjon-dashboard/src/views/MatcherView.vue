<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'

// ── State ────────────────────────────────────────────────────────────────
const reports    = ref([])
const matches    = ref([])
const notices    = ref([])   // raw from /api/notices
const loading    = ref(true)
const error      = ref(null)
const refreshing = ref(false)
const expandedIds = ref(new Set())
const searchQuery = ref('')

// ── Router / URL filter ───────────────────────────────────────────────────
const route  = useRoute()
const router = useRouter()

const filterReportId = computed(() => {
  const v = route.query.report
  return v ? Number(v) : null
})

// ── Severity metadata ─────────────────────────────────────────────────────
const severityOrder = {
  'Sterkt kritikkverdig':   0,
  'Kritikkverdig':          1,
  'Ikke tilfredsstillende': 2,
  'Ingen karakter':         3,
}

const severityColor = {
  'Sterkt kritikkverdig':   '#e63946',
  'Kritikkverdig':          '#f4a261',
  'Ikke tilfredsstillende': '#e9c46a',
  'Ingen karakter':         '#52796f',
}

// ── Data fetch ────────────────────────────────────────────────────────────
async function loadData() {
  loading.value = true
  error.value   = null
  try {
    const [reportsRes, matchesRes, noticesRes] = await Promise.all([
      fetch('/api/reports'),
      fetch('/api/matches'),
      fetch('/api/notices'),
    ])
    if (!reportsRes.ok) throw new Error(`/api/reports ${reportsRes.status}`)
    if (!matchesRes.ok) throw new Error(`/api/matches ${matchesRes.status}`)
    if (!noticesRes.ok) throw new Error(`/api/notices ${noticesRes.status}`)

    const reportsData = await reportsRes.json()
    const matchesData = await matchesRes.json()
    const noticesData = await noticesRes.json()
    reports.value = reportsData.reports   // API returns { loading, reports: [...] }
    matches.value = matchesData.matches   // API returns { loading, matches: [...] }
    notices.value = noticesData.notices   // API returns { loading, notices: [...] }
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  document.title = 'Anbudsmatcher – Riksrevisjonen'
  loadData()
})

onUnmounted(() => {
  document.title = 'Rapportoversikt – Riksrevisjonen'
})

// ── Data join: group matches by reportId ──────────────────────────────────
const matchesByReport = computed(() => {
  const map = new Map()
  for (const m of matches.value) {
    if (!map.has(m.reportId)) map.set(m.reportId, [])
    map.get(m.reportId).push(m)
  }
  return map
})

// ── Build report index ────────────────────────────────────────────────────
const reportMap = computed(() => {
  const map = new Map()
  for (const r of reports.value) map.set(r.id, r)
  return map
})

// Build notice index for O(1) lookup by noticeId
const noticeMap = computed(() => {
  const map = new Map()
  for (const n of notices.value) map.set(n.id, n)
  return map
})

// ── Sorted list: only reports with >= 1 match ─────────────────────────────
const matchedReports = computed(() => {
  const result = []
  for (const [reportId, reportMatches] of matchesByReport.value) {
    const report = reportMap.value.get(reportId)
    if (!report) continue
    result.push({ report, matches: reportMatches })
  }

  const sorted = result.sort((a, b) => {
    const sevDiff =
      (severityOrder[a.report.severity] ?? 99) -
      (severityOrder[b.report.severity] ?? 99)
    if (sevDiff !== 0) return sevDiff
    return b.matches.length - a.matches.length
  })

  let out = sorted
  if (filterReportId.value !== null) {
    out = sorted.filter(item => item.report.id === filterReportId.value)
  }

  const q = searchQuery.value.trim().toLowerCase()
  if (q) {
    out = out.filter(item => {
      if (item.report.title.toLowerCase().includes(q)) return true
      if (item.report.department?.toLowerCase().includes(q)) return true
      return item.matches.some(m => {
        const n = noticeMap.value.get(m.noticeId)
        return n && (n.title.toLowerCase().includes(q) || n.buyer.toLowerCase().includes(q))
      })
    })
  }
  return out
})

// ── Expand / collapse ─────────────────────────────────────────────────────
// CRITICAL: Vue 3 does NOT react to Set.add() / Set.delete() in-place.
// Always clone-and-reassign to trigger reactivity.
function toggleExpand(id) {
  const next = new Set(expandedIds.value)
  if (next.has(id)) {
    next.delete(id)
  } else {
    next.add(id)
  }
  expandedIds.value = next
}

function scoreColor(score) {
  if (score >= 60) return '#52796f'   // green
  if (score >= 30) return '#f4a261'   // amber
  return '#e63946'                    // red
}

const stats = computed(() => {
  const m = matches.value
  if (!m.length) return null
  const scores = m.map(x => x.score)
  const uniqueReports = new Set(m.map(x => x.reportId)).size
  return {
    total: m.length,
    uniqueReports,
    avgScore: (scores.reduce((a, b) => a + b, 0) / scores.length).toFixed(1),
    topScore: Math.max(...scores),
  }
})

function fmtDate(d) {
  if (!d) return ''
  return new Date(d).toLocaleDateString('nb-NO', { day: 'numeric', month: 'short', year: 'numeric' })
}

async function refreshData() {
  refreshing.value = true
  error.value = null
  try {
    const res = await fetch('/api/matches/refresh', { method: 'POST' })
    if (!res.ok) throw new Error(`Refresh feilet: ${res.status} ${res.statusText}`)
    await loadData()
  } catch (e) {
    error.value = e.message
  } finally {
    refreshing.value = false
  }
}
</script>

<template>
  <div class="matcher-body">

    <!-- Page nav -->
    <div class="matcher-topnav">
      <div class="matcher-brand">
        <span class="mono muted">RIKSREVISJONEN</span>
        <span class="sep">·</span>
        <span class="page-title-sm">Anbud<em>matcher</em></span>
      </div>
      <nav class="matcher-tabs">
        <RouterLink to="/" exact-active-class="active" class="tab mono">Rapporter</RouterLink>
        <RouterLink to="/matcher" active-class="active" class="tab mono">Anbudsmatcher</RouterLink>
      </nav>
    </div>

    <!-- Toolbar -->
    <div class="matcher-toolbar">
      <input
        v-model="searchQuery"
        type="text"
        class="matcher-search mono"
        placeholder="Søk i rapporter, kunngjøringer, kjøpere..."
      />
      <button
        class="chip"
        :disabled="refreshing || loading"
        @click="refreshData"
      >
        <span v-if="refreshing" class="refresh-spinner"></span>
        <span>{{ refreshing ? 'Oppdaterer...' : 'Oppdater data' }}</span>
      </button>
    </div>

    <!-- Stats bar -->
    <div v-if="stats && !loading" class="stats-bar mono">
      <span class="stat"><strong>{{ stats.total }}</strong> treff</span>
      <span class="stat-sep">·</span>
      <span class="stat"><strong>{{ stats.uniqueReports }}</strong> rapporter</span>
      <span class="stat-sep">·</span>
      <span class="stat">snitt <strong>{{ stats.avgScore }}</strong></span>
      <span class="stat-sep">·</span>
      <span class="stat">topp <strong>{{ stats.topScore }}</strong></span>
    </div>

    <!-- Active filter banner -->
    <div v-if="filterReportId" class="filter-banner mono">
      <span>Filtrert til én rapport</span>
      <button class="chip" @click="router.push('/matcher')">Vis alle</button>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="matcher-loading">
      <div class="loading-ring"></div>
      <p class="mono muted">Henter matcher-data...</p>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="matcher-error">
      <div class="error-banner mono">
        <span class="error-icon">⚠</span>
        <span>{{ error }}</span>
      </div>
      <button class="chip" @click="loadData" style="margin-top: 0.75rem;">
        Proev igjen
      </button>
    </div>

    <!-- Empty -->
    <div v-else-if="matchedReports.length === 0" class="matcher-empty mono muted">
      <template v-if="filterReportId">
        Denne rapporten har ingen anbudstreff.
        <button class="chip" style="margin-top: 0.5rem; display: block;" @click="router.push('/matcher')">Vis alle rapporter</button>
      </template>
      <template v-else>
        Ingen treff &mdash; ingen rapporter har matchet Doffin-kunngjoeringer.
      </template>
    </div>

    <!-- Report list -->
    <template v-else>
      <div class="matcher-list">
        <div
          v-for="{ report, matches: reportMatches } in matchedReports"
          :key="report.id"
          class="report-row"
        >
          <!-- Row header -->
          <button
            class="row-header"
            @click="toggleExpand(report.id)"
            :aria-expanded="expandedIds.has(report.id)"
          >
            <span
              class="sev-dot"
              :style="{ background: severityColor[report.severity] }"
              :title="report.severity"
            ></span>

            <span class="row-title">{{ report.title }}</span>

            <span v-if="report.department" class="dept-label mono">{{ report.department.replace(/ · \/ /g, ' · ') }}</span>

            <a
              v-if="report.url"
              :href="report.url"
              target="_blank"
              rel="noopener"
              class="rapport-link mono"
              @click.stop
            >Les rapport →</a>

            <span
              class="badge mono"
              :style="{ color: severityColor[report.severity], borderColor: severityColor[report.severity] }"
            >
              {{ report.severity }}
            </span>

            <span class="match-chip mono">{{ reportMatches.length }} treff</span>

            <span class="chevron mono" :class="{ open: expandedIds.has(report.id) }">&#9662;</span>
          </button>

          <!-- Expanded notice rows -->
          <div v-if="expandedIds.has(report.id)" class="notices-panel">
            <div
              v-for="m in reportMatches"
              :key="m.noticeId"
              class="notice-row"
            >
              <div class="notice-top">
                <a
                  :href="noticeMap.get(m.noticeId)?.url || '#'"
                  target="_blank"
                  rel="noopener"
                  class="notice-title link"
                >
                  {{ noticeMap.get(m.noticeId)?.title || m.noticeId }}
                  <span aria-hidden="true"> →</span>
                </a>
              </div>

              <div class="notice-meta mono muted">
                <span v-if="noticeMap.get(m.noticeId)?.buyer">
                  {{ noticeMap.get(m.noticeId).buyer }}
                </span>
                <span class="meta-sep" v-if="noticeMap.get(m.noticeId)?.publishedDate">·</span>
                <span v-if="noticeMap.get(m.noticeId)?.publishedDate">
                  {{ fmtDate(noticeMap.get(m.noticeId).publishedDate) }}
                </span>
              </div>

              <div class="score-row">
                <span class="score-label mono muted">Konfidenspoeng {{ m.score }}</span>
                <div class="score-bar">
                  <div
                    class="score-fill"
                    :style="{
                      width: Math.min(100, Math.max(0, m.score)) + '%',
                      background: scoreColor(m.score)
                    }"
                  ></div>
                </div>
              </div>

              <div v-if="m.matchedOrg" class="notice-org mono muted">
                Org: {{ m.matchedOrg }}
              </div>

              <div v-if="noticeMap.get(m.noticeId)?.description" class="notice-desc mono muted">
                {{ noticeMap.get(m.noticeId).description.substring(0, 200) }}{{ noticeMap.get(m.noticeId).description.length > 200 ? '...' : '' }}
              </div>

              <div v-if="m.matchedKeywords?.length" class="keyword-tags">
                <span
                  v-for="kw in m.matchedKeywords"
                  :key="kw"
                  class="kw-tag mono"
                >{{ kw }}</span>
              </div>

              <div v-if="m.matchedBigrams?.length" class="keyword-tags bigram-tags">
                <span class="kw-tag mono tag-label">fraser:</span>
                <span
                  v-for="bg in m.matchedBigrams"
                  :key="bg"
                  class="kw-tag mono kw-tag--bigram"
                >{{ bg }}</span>
              </div>
            </div>
          </div>

        </div>
      </div>
    </template>

  </div>
</template>

<style>
.matcher-body {
  max-width: 1280px;
  margin: 0 auto;
  padding: 3rem 2rem 4rem;
}

.matcher-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  padding: 6rem 0;
  text-align: center;
}

.matcher-error {
  padding: 3rem 0;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 1rem;
  font-size: 0.8rem;
  color: #e63946;
}

.matcher-empty {
  padding: 4rem 0;
  font-size: 0.85rem;
}

.matcher-list {
  display: flex;
  flex-direction: column;
  gap: 1px;
  background: var(--border);
  border: 1px solid var(--border);
  border-radius: 2px;
  overflow: hidden;
}

.report-row { background: var(--surf); }

.row-header {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.9rem;
  padding: 1rem 1.25rem;
  background: transparent;
  border: none;
  cursor: pointer;
  text-align: left;
  transition: background 0.15s ease;
}
.row-header:hover { background: var(--surf2); }

.sev-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.row-title {
  flex: 1;
  font-family: 'Source Serif 4', Georgia, serif;
  font-size: 0.95rem;
  font-weight: 400;
  color: var(--text);
  line-height: 1.35;
  text-align: left;
}

.rapport-link {
  font-size: 0.6rem;
  letter-spacing: 0.06em;
  color: var(--muted);
  text-decoration: none;
  border: 1px solid var(--border);
  border-radius: 2px;
  padding: 0.2rem 0.55rem;
  white-space: nowrap;
  flex-shrink: 0;
  transition: all 0.15s ease;
}
.rapport-link:hover { border-color: var(--muted); color: var(--text); }

.badge {
  font-size: 0.52rem;
  letter-spacing: 0.1em;
  border: 1px solid;
  padding: 0.2rem 0.5rem;
  border-radius: 1px;
  opacity: 0.75;
  white-space: nowrap;
  flex-shrink: 0;
}

.match-chip {
  font-size: 0.6rem;
  letter-spacing: 0.08em;
  background: var(--surf2);
  border: 1px solid var(--border);
  border-radius: 2px;
  padding: 0.2rem 0.55rem;
  color: var(--muted);
  white-space: nowrap;
  flex-shrink: 0;
}

.chevron {
  font-size: 0.75rem;
  color: var(--dim);
  transition: transform 0.2s ease;
  flex-shrink: 0;
}
.chevron.open { transform: rotate(180deg); }

.notices-panel {
  border-top: 1px solid var(--border);
  background: var(--surf2);
  padding: 1rem 1.25rem 1rem 3rem;
}

.notices-placeholder { font-size: 0.7rem; }

.loading-ring {
  width: 36px;
  height: 36px;
  border: 2px solid var(--border);
  border-top-color: #e63946;
  border-radius: 50%;
  animation: spin 0.9s linear infinite;
}

@media (max-width: 640px) {
  .row-header { flex-wrap: wrap; }
  .badge { display: none; }
}

/* ── Notice rows ───────────────────────────────────────────────────────── */
.notice-row {
  padding: 0.85rem 0;
  border-bottom: 1px solid var(--border);
}
.notice-row:last-child { border-bottom: none; }

.notice-top { margin-bottom: 0.3rem; }

.notice-title {
  font-family: 'Source Serif 4', Georgia, serif;
  font-size: 0.88rem;
  font-weight: 400;
  color: var(--text);
  text-decoration: none;
  transition: color 0.15s;
}
.notice-title:hover { color: #e63946; }

.notice-meta {
  font-size: 0.62rem;
  letter-spacing: 0.04em;
  display: flex;
  gap: 0.35rem;
  align-items: center;
  margin-bottom: 0.4rem;
}
.meta-sep { color: var(--dim); }

/* ── Score bar ─────────────────────────────────────────────────────────── */
.score-row {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  margin: 0.35rem 0;
}
.score-label {
  font-size: 0.58rem;
  letter-spacing: 0.06em;
}
.score-bar {
  height: 6px;
  background: var(--surf);
  border-radius: 3px;
  overflow: hidden;
  width: 100%;
  border: 1px solid var(--border);
}
.score-fill {
  height: 100%;
  border-radius: 3px;
  transition: width 0.3s ease;
}

/* ── Matched org + keywords ─────────────────────────────────────────────── */
.notice-org {
  font-size: 0.62rem;
  letter-spacing: 0.04em;
  margin-top: 0.3rem;
}

.notice-desc {
  font-size: 0.62rem;
  line-height: 1.5;
  margin-top: 0.3rem;
  color: var(--muted);
  opacity: 0.8;
}

.dept-label {
  font-size: 0.52rem;
  letter-spacing: 0.06em;
  color: var(--dim);
  white-space: nowrap;
  flex-shrink: 0;
}

.keyword-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
  margin-top: 0.4rem;
}
.kw-tag {
  font-size: 0.55rem;
  letter-spacing: 0.06em;
  padding: 0.15rem 0.45rem;
  background: var(--surf);
  border: 1px solid var(--border);
  border-radius: 2px;
  color: var(--muted);
}
.kw-tag--bigram {
  background: rgba(82, 121, 111, 0.08);
  border-color: rgba(82, 121, 111, 0.3);
  color: #52796f;
}
.tag-label {
  opacity: 0.5;
  font-style: italic;
}

/* ── Matcher topnav ────────────────────────────────────────────────────── */
.matcher-topnav {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1.5rem;
  flex-wrap: wrap;
  background: var(--surf);
  border-bottom: 1px solid var(--border);
  padding: 1.25rem 2rem;
  margin: -3rem -2rem 2.5rem;
}
.matcher-brand {
  display: flex;
  align-items: baseline;
  gap: 0.6rem;
}
.matcher-brand .mono { font-size: 0.62rem; letter-spacing: 0.16em; }
.page-title-sm {
  font-family: 'Source Serif 4', Georgia, serif;
  font-size: 1.4rem;
  font-weight: 400;
  letter-spacing: -0.01em;
  line-height: 1;
}
.page-title-sm em { font-style: italic; color: #e63946; }
.matcher-tabs { display: flex; gap: 0.25rem; }

/* ── Matcher toolbar ───────────────────────────────────────────────────── */
.matcher-toolbar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
}
.matcher-search {
  flex: 1;
  padding: 0.4rem 0.8rem;
  background: var(--surf);
  border: 1px solid var(--border);
  border-radius: 2px;
  color: var(--text);
  font-size: 0.7rem;
  letter-spacing: 0.04em;
  outline: none;
  transition: border-color 0.15s;
}
.matcher-search::placeholder { color: var(--dim); }
.matcher-search:focus { border-color: var(--muted); }

/* ── Stats bar ─────────────────────────────────────────────────────────── */
.stats-bar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  background: var(--surf2);
  border: 1px solid var(--border);
  border-radius: 2px;
  font-size: 0.6rem;
  letter-spacing: 0.06em;
  color: var(--muted);
  margin-bottom: 1rem;
}
.stats-bar strong { color: var(--text); }
.stat-sep { color: var(--dim); }
.chip {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.3rem 0.8rem;
  background: transparent;
  border: 1px solid var(--border);
  border-radius: 2px;
  color: var(--muted);
  font-family: 'Space Mono', monospace;
  font-size: 0.6rem;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: all 0.15s ease;
}
.chip:disabled { opacity: 0.5; cursor: not-allowed; }
.chip:not(:disabled):hover { border-color: var(--muted); color: var(--text); }
.chip.active { background: var(--surf2); border-color: var(--chip-c, var(--text)); color: var(--chip-c, var(--text)); }
.chip-n { font-style: normal; background: var(--surf); border-radius: 2px; padding: 0.1rem 0.35rem; font-size: 0.55rem; color: var(--dim); }
.year-chip.active { --chip-c: #52796f; }

.filter-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.6rem 1rem;
  background: var(--surf2);
  border: 1px solid var(--border);
  border-radius: 2px;
  font-size: 0.65rem;
  letter-spacing: 0.08em;
  color: var(--muted);
  margin-bottom: 1rem;
}

.refresh-spinner {
  display: inline-block;
  width: 10px;
  height: 10px;
  border: 1.5px solid var(--border);
  border-top-color: var(--muted);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  flex-shrink: 0;
}

/* ── Error banner ──────────────────────────────────────────────────────── */
.error-banner {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  background: rgba(230, 57, 70, 0.06);
  border: 1px solid rgba(230, 57, 70, 0.3);
  border-radius: 2px;
  font-size: 0.72rem;
  color: #e63946;
  letter-spacing: 0.04em;
}
.error-icon { font-size: 0.9rem; flex-shrink: 0; }
</style>
