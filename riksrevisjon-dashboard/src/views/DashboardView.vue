<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { RouterLink } from 'vue-router'
import { loadAllData } from '../lib/dataLoader'

const reports = ref([])
const matches = ref([])
const notices = ref([])
const loading = ref(true)
const activeFilter = ref('Alle')
const searchQuery  = ref('')

const filters = ['Alle', 'Sterkt kritikkverdig', 'Kritikkverdig', 'Ikke tilfredsstillende', 'Ingen karakter']

const severityMeta = {
  'Sterkt kritikkverdig':    { color: '#e63946', dim: 'rgba(230,57,70,0.12)'  },
  'Kritikkverdig':           { color: '#f4a261', dim: 'rgba(244,162,97,0.10)' },
  'Ikke tilfredsstillende':  { color: '#e9c46a', dim: 'rgba(233,196,106,0.09)'},
  'Ingen karakter':          { color: '#52796f', dim: 'rgba(82,121,111,0.08)' },
}

const severityOrder = { 'Sterkt kritikkverdig': 0, 'Kritikkverdig': 1, 'Ikke tilfredsstillende': 2, 'Ingen karakter': 3 }

async function fetchReports() {
  try {
    const data = await loadAllData()
    reports.value = data.reports
    matches.value = data.matches
    notices.value = data.notices
  } catch {
    // silent fail — static fallback already attempted
  } finally {
    loading.value = false
  }
}

onMounted(() => fetchReports())

const filtered = computed(() => {
  let list = reports.value
  if (activeFilter.value !== 'Alle') list = list.filter(r => r.severity === activeFilter.value)
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(r =>
      r.title?.toLowerCase().includes(q) ||
      r.summary?.toLowerCase().includes(q) ||
      r.department?.toLowerCase().includes(q)
    )
  }
  return [...list].sort((a, b) => severityOrder[a.severity] - severityOrder[b.severity])
})

const grouped = computed(() => {
  const g = {}
  for (const sev of Object.keys(severityOrder)) {
    const items = filtered.value.filter(r => r.severity === sev)
    if (items.length) g[sev] = items
  }
  return g
})

const counts = computed(() => {
  const c = {}
  for (const f of filters) {
    c[f] = f === 'Alle' ? reports.value.length : reports.value.filter(r => r.severity === f).length
  }
  return c
})

const matchCountMap = computed(() => {
  const m = new Map()
  for (const match of matches.value) {
    m.set(match.reportId, (m.get(match.reportId) ?? 0) + 1)
  }
  return m
})

const chartBars = computed(() => {
  const total = reports.value.length || 1
  return Object.entries(severityMeta).map(([sev, meta]) => ({
    sev,
    color: meta.color,
    count: counts.value[sev] || 0,
    pct: Math.round(((counts.value[sev] || 0) / total) * 100),
    width: ((counts.value[sev] || 0) / total) * 100,
  }))
})

function fmtDate(d) {
  if (!d) return ''
  return new Date(d).toLocaleDateString('nb-NO', { day: 'numeric', month: 'short', year: 'numeric' })
}
</script>

<template>
  <div class="shell">

    <!-- ── PAGE HEADER + NAV ── -->
    <div class="page-top">
      <div class="page-top-inner">
        <div class="page-brand">
          <span class="mono muted">RIKSREVISJONEN</span>
          <span class="sep">·</span>
          <h1 class="page-title">Rapport<em>oversikt</em></h1>
        </div>
        <nav class="page-tabs">
          <RouterLink to="/" exact-active-class="active" class="tab mono">Rapporter</RouterLink>
          <RouterLink to="/matcher" active-class="active" class="tab mono">Anbudsmatcher</RouterLink>
        </nav>
      </div>
    </div>

    <!-- ── DISTRIBUTION CHART ── -->
    <div class="chart-section">
      <div class="chart-inner">
        <div class="chart-head">
          <span class="mono chart-title">FORDELING AV KRITIKKVERDIGHET</span>
          <span class="mono chart-total muted">{{ reports.length }} rapporter totalt</span>
        </div>

        <!-- Stacked bar -->
        <div class="chart-stack">
          <div
            v-for="bar in chartBars" :key="bar.sev"
            class="stack-seg"
            :style="{ width: bar.width + '%', background: bar.color }"
            :title="`${bar.sev}: ${bar.count}`"
          ></div>
        </div>

        <!-- Bar rows -->
        <div class="chart-bars">
          <div v-for="bar in chartBars" :key="bar.sev" class="bar-row"
            :class="{ faded: activeFilter !== 'Alle' && activeFilter !== bar.sev }"
            @click="activeFilter = bar.sev === activeFilter ? 'Alle' : bar.sev"
          >
            <span class="bar-label mono">{{ bar.sev }}</span>
            <div class="bar-track">
              <div class="bar-fill" :style="{ width: bar.width + '%', background: bar.color }"></div>
            </div>
            <span class="bar-count mono" :style="{ color: bar.color }">{{ bar.count }}</span>
            <span class="bar-pct mono muted">{{ bar.pct }}%</span>
          </div>
        </div>
      </div>
    </div>

    <div class="toolbar">
      <div class="toolbar-inner">
        <div class="filters">
          <button v-for="f in filters" :key="f"
            class="chip"
            :class="{ active: activeFilter === f }"
            :style="activeFilter === f && f !== 'Alle' ? { '--chip-c': severityMeta[f]?.color } : {}"
            @click="activeFilter = f">
            <span>{{ f === 'Alle' ? 'Alle rapporter' : f }}</span>
            <em class="chip-n">{{ counts[f] }}</em>
          </button>
        </div>
        <label class="searchbox">
          <svg class="search-ic" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
          <input v-model="searchQuery" placeholder="Søk tittel, departement…" />
        </label>
      </div>
    </div>

    <main class="body">

      <!-- Loading shimmer -->
      <template v-if="loading && !filtered.length">
        <div class="loading-state">
          <div class="loading-ring"></div>
          <p class="mono muted">Henter rapporter fra riksrevisjonen.no…</p>
          <p class="mono muted small">Scraper {{ Object.keys(severityOrder).length > 0 ? 'alle sider' : '' }} parallelt</p>
        </div>
      </template>

      <template v-else>
        <!-- Live loading banner -->
        <div v-if="loading" class="live-banner mono">
          <span class="pulse-dot"></span> Laster inn flere rapporter…
        </div>

        <div v-if="!filtered.length" class="empty mono muted">Ingen rapporter matcher søket.</div>

        <section v-for="(items, sev) in grouped" :key="sev" class="sev-section"
          :style="{ '--c': severityMeta[sev].color, '--dim': severityMeta[sev].dim }">

          <div class="sev-head">
            <div class="sev-bar"></div>
            <h2 class="sev-label mono">{{ sev.toUpperCase() }}</h2>
            <span class="sev-count mono">{{ items.length }} rapport{{ items.length !== 1 ? 'er' : '' }}</span>
          </div>

          <div class="grid">
            <article v-for="(r, i) in items" :key="r.id" class="card"
              :style="{ animationDelay: `${i * 50}ms` }">
              <a :href="r.url" target="_blank" rel="noopener" class="card-link" :aria-label="r.title"></a>
              <div class="card-stripe"></div>
              <div class="card-content">
                <div class="card-top">
                  <span class="dept mono">{{ r.department || '—' }}</span>
                  <span class="date mono">{{ fmtDate(r.publishedDate) }}</span>
                </div>
                <h3 class="card-title">{{ r.title }}</h3>
                <p class="card-body">{{ r.summary }}</p>
              </div>
              <div class="card-foot">
                <span class="badge mono" :style="{ color: severityMeta[sev].color, borderColor: severityMeta[sev].color }">
                  {{ sev }}
                </span>
                <RouterLink
                  v-if="matchCountMap.get(r.id)"
                  :to="`/matcher?report=${r.id}`"
                  class="match-badge mono"
                >
                  {{ matchCountMap.get(r.id) }} anbudstreff
                </RouterLink>
              </div>
            </article>
          </div>
        </section>
      </template>
    </main>

    <footer class="foot">
      <span class="mono">Data hentet live fra riksrevisjonen.no</span>
      <span class="mono muted">· {{ reports.length }} rapporter totalt</span>
    </footer>
  </div>
</template>

<style>
/* ── PAGE TOP ───────────────────────────── */
.page-top {
  background: var(--surf);
  border-bottom: 1px solid var(--border);
  padding: 1.25rem 0;
}
.page-top-inner {
  max-width: 1280px;
  margin: 0 auto;
  padding: 0 2rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1.5rem;
  flex-wrap: wrap;
}
.page-brand {
  display: flex;
  align-items: baseline;
  gap: 0.6rem;
}
.page-brand .mono { font-size: 0.62rem; letter-spacing: 0.16em; }
.page-title {
  font-family: 'Playfair Display', Georgia, serif;
  font-size: 1.4rem;
  font-weight: 900;
  letter-spacing: -0.01em;
  line-height: 1;
}
.page-title em { font-style: italic; font-weight: 700; color: #e63946; }
.page-tabs { display: flex; gap: 0.25rem; }

/* ── CHART ──────────────────────────────── */
.chart-section {
  border-bottom: 1px solid var(--border);
  background: var(--surf);
  padding: 2rem 0;
}
.chart-inner {
  max-width: 1280px;
  margin: 0 auto;
  padding: 0 2rem;
}
.chart-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.25rem;
}
.chart-title { font-size: 0.62rem; letter-spacing: 0.16em; color: var(--muted); }
.chart-total { font-size: 0.62rem; }

/* Stacked bar */
.chart-stack {
  display: flex;
  height: 10px;
  border-radius: 2px;
  overflow: hidden;
  margin-bottom: 1.5rem;
  gap: 2px;
}
.stack-seg {
  transition: flex 0.4s ease;
  border-radius: 1px;
}

/* Bar rows */
.chart-bars { display: flex; flex-direction: column; gap: 0.6rem; }
.bar-row {
  display: grid;
  grid-template-columns: 200px 1fr 2.5rem 2.5rem;
  align-items: center;
  gap: 1rem;
  cursor: pointer;
  transition: opacity 0.2s;
}
.bar-row.faded { opacity: 0.35; }
.bar-row:hover { opacity: 1 !important; }
.bar-label { font-size: 0.65rem; letter-spacing: 0.04em; color: var(--text); }
.bar-track {
  height: 8px;
  background: var(--bg);
  border-radius: 2px;
  overflow: hidden;
  border: 1px solid var(--border);
}
.bar-fill {
  height: 100%;
  border-radius: 2px;
  transition: width 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}
.bar-count { font-size: 0.65rem; text-align: right; font-weight: 700; }
.bar-pct   { font-size: 0.6rem; text-align: right; }

/* ── TOOLBAR ────────────────────────────── */
.toolbar {
  position: sticky;
  top: 0;
  z-index: 100;
  background: rgba(245,243,240,0.92);
  backdrop-filter: blur(14px);
  border-bottom: 1px solid var(--border);
  padding: 0.85rem 0;
}
.toolbar-inner {
  max-width: 1280px;
  margin: 0 auto;
  padding: 0 2rem;
  display: flex;
  align-items: center;
  gap: 1.25rem;
  flex-wrap: wrap;
}
.filters { display: flex; gap: 0.4rem; flex-wrap: wrap; flex: 1; }
.toolbar-right { display: flex; align-items: center; gap: 0.5rem; flex-shrink: 0; }
.year-chip.active { --chip-c: #52796f; }
.chip {
  display: flex;
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
.chip:hover { border-color: var(--muted); color: var(--text); }
.chip.active {
  background: var(--surf2);
  border-color: var(--chip-c, var(--text));
  color: var(--chip-c, var(--text));
}
.chip-n {
  font-style: normal;
  background: var(--surf);
  border-radius: 2px;
  padding: 0.1rem 0.35rem;
  font-size: 0.55rem;
  color: var(--dim);
}

.searchbox {
  position: relative;
  min-width: 260px;
  display: flex;
  align-items: center;
}
.search-ic {
  position: absolute;
  left: 0.7rem;
  width: 14px;
  height: 14px;
  color: var(--muted);
  flex-shrink: 0;
}
.searchbox input {
  width: 100%;
  background: var(--surf);
  border: 1px solid var(--border);
  border-radius: 2px;
  padding: 0.4rem 0.75rem 0.4rem 2.1rem;
  color: var(--text);
  font-family: 'Space Mono', monospace;
  font-size: 0.7rem;
  outline: none;
  transition: border-color 0.15s;
}
.searchbox input:focus { border-color: var(--muted); }
.searchbox input::placeholder { color: var(--dim); }

/* ── BODY ───────────────────────────────── */
.body {
  max-width: 1280px;
  margin: 0 auto;
  padding: 3rem 2rem 4rem;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  padding: 6rem 0;
  text-align: center;
}
.loading-ring {
  width: 36px;
  height: 36px;
  border: 2px solid var(--border);
  border-top-color: #e63946;
  border-radius: 50%;
  animation: spin 0.9s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }

.live-banner {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 0.65rem;
  letter-spacing: 0.08em;
  color: var(--muted);
  padding: 0.6rem 1rem;
  background: var(--surf);
  border: 1px solid var(--border);
  border-radius: 2px;
  margin-bottom: 2rem;
}
.pulse-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #e9c46a;
  animation: pulse 1.5s ease infinite;
}
@keyframes pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50%       { opacity: 0.4; transform: scale(0.7); }
}

.empty { padding: 4rem 0; font-size: 0.85rem; }

/* ── SEVERITY SECTION ───────────────────── */
.sev-section { margin-bottom: 3.5rem; }
.sev-head {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding-bottom: 0.75rem;
  margin-bottom: 1.25rem;
  border-bottom: 1px solid var(--border);
}
.sev-bar {
  width: 3px;
  height: 1.4rem;
  background: var(--c);
  border-radius: 1px;
  flex-shrink: 0;
}
.sev-label { font-size: 0.65rem; letter-spacing: 0.14em; color: var(--text); }
.sev-count { font-size: 0.6rem; color: var(--muted); margin-left: auto; }

/* ── GRID ───────────────────────────────── */
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 1px;
  background: var(--border);
}

/* ── CARD ───────────────────────────────── */
.card {
  display: flex;
  flex-direction: column;
  background: var(--surf);
  overflow: hidden;
  animation: rise 0.35s ease both;
  transition: background 0.2s;
  position: relative;
  cursor: pointer;
}
.card:hover { background: var(--surf2); }
@keyframes rise {
  from { opacity: 0; transform: translateY(10px); }
  to   { opacity: 1; transform: none; }
}
.card-link {
  position: absolute;
  inset: 0;
  z-index: 0;
}
.card-stripe {
  height: 2px;
  background: var(--c);
  opacity: 0.55;
  transition: opacity 0.2s;
}
.card:hover .card-stripe { opacity: 1; }

.card-content { padding: 1.4rem 1.4rem 1rem; flex: 1; }
.card-top {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
  margin-bottom: 0.7rem;
}
.dept { font-size: 0.6rem; letter-spacing: 0.05em; color: var(--muted); flex: 1; line-height: 1.4; }
.date { font-size: 0.58rem; color: var(--muted); white-space: nowrap; flex-shrink: 0; }

.card-title {
  font-family: 'Playfair Display', Georgia, serif;
  font-size: 1.05rem;
  font-weight: 700;
  line-height: 1.3;
  letter-spacing: -0.01em;
  margin-bottom: 0.7rem;
  color: var(--text);
}
.card-body {
  font-size: 0.82rem;
  color: var(--muted);
  line-height: 1.7;
  font-weight: 300;
}

.card-foot {
  padding: 0.9rem 1.4rem;
  border-top: 1px solid var(--border);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  position: relative;
  z-index: 1;
}
.badge {
  font-size: 0.52rem;
  letter-spacing: 0.1em;
  border: 1px solid;
  padding: 0.2rem 0.5rem;
  border-radius: 1px;
  opacity: 0.65;
}
.match-badge {
  font-size: 0.52rem;
  letter-spacing: 0.08em;
  padding: 0.2rem 0.55rem;
  background: rgba(82, 121, 111, 0.08);
  border: 1px solid rgba(82, 121, 111, 0.35);
  border-radius: 2px;
  color: #52796f;
  text-decoration: none;
  white-space: nowrap;
  flex-shrink: 0;
  transition: all 0.15s ease;
  cursor: pointer;
}
.match-badge:hover {
  background: rgba(82, 121, 111, 0.15);
  border-color: #52796f;
}

/* ── FOOTER ─────────────────────────────── */
.foot {
  border-top: 1px solid var(--border);
  padding: 1.75rem 2rem;
  text-align: center;
  font-size: 0.63rem;
  letter-spacing: 0.1em;
  color: var(--dim);
  display: flex;
  justify-content: center;
  gap: 0.5rem;
}

@media (max-width: 640px) {
  .hdr-title  { font-size: 2.6rem; }
  .toolbar-inner { flex-direction: column; align-items: stretch; }
  .searchbox  { min-width: unset; }
  .grid       { grid-template-columns: 1fr; }
}
</style>
