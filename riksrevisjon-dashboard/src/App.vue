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

<style>
@import url('https://fonts.googleapis.com/css2?family=Playfair+Display:ital,wght@0,700;0,900;1,700&family=Space+Mono:wght@400;700&family=Source+Serif+4:opsz,wght@8..60,300;8..60,400&display=swap');

*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

:root {
  --bg:      #f5f3f0;
  --surf:    #ffffff;
  --surf2:   #f0ede9;
  --border:  #ddd9d4;
  --text:    #1a1815;
  --muted:   #6b6560;
  --dim:     #a09b95;
}

html { scroll-behavior: smooth; }
body {
  background: var(--bg);
  color: var(--text);
  font-family: 'Source Serif 4', Georgia, serif;
  font-size: 16px;
  line-height: 1.6;
  min-height: 100vh;
}

.mono  { font-family: 'Space Mono', monospace; }
.muted { color: var(--muted); }
.small { font-size: 0.75em; }

/* ── HEADER ─────────────────────────────── */
.hdr {
  border-bottom: 1px solid var(--border);
  padding: 3.5rem 0 2.5rem;
  background: linear-gradient(180deg, rgba(230,57,70,0.04) 0%, transparent 100%);
}
.hdr-inner {
  max-width: 1280px;
  margin: 0 auto;
  padding: 0 2rem;
  margin-bottom: 2rem;
}
.hdr-eye {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  font-size: 0.65rem;
  letter-spacing: 0.18em;
  margin-bottom: 1.2rem;
}
.sep { color: var(--dim); }
.hdr-title {
  font-family: 'Playfair Display', Georgia, serif;
  font-size: clamp(3rem, 6vw, 5.5rem);
  font-weight: 900;
  line-height: 1;
  letter-spacing: -0.025em;
  margin-bottom: 1rem;
}
.hdr-title em {
  font-style: italic;
  font-weight: 700;
  color: #e63946;
}
.hdr-sub { font-size: 0.7rem; letter-spacing: 0.06em; color: var(--muted); }

/* ── TAB NAV ────────────────────────────── */
.hdr-tabs { display: flex; gap: 0.25rem; margin-top: 1.5rem; }
.tab {
  display: inline-flex; align-items: center;
  padding: 0.3rem 0.8rem;
  background: transparent; border: 1px solid var(--border); border-radius: 2px;
  color: var(--muted); font-size: 0.6rem; letter-spacing: 0.08em;
  text-decoration: none; transition: all 0.15s ease;
}
.tab:hover { border-color: var(--muted); color: var(--text); }
.tab.active { background: var(--surf2); border-color: var(--text); color: var(--text); }

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
  .hdr-title { font-size: 2.6rem; }
}
</style>
