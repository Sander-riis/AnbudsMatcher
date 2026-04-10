<script setup>
import { RouterView, RouterLink } from 'vue-router'
</script>

<template>
  <div class="shell">
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
.sep   { color: var(--dim); }

/* ── TAB NAV (shared) ───────────────────── */
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
</style>
