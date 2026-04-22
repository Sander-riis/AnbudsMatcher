<script setup>
import { ref } from 'vue'
import { RouterView, RouterLink } from 'vue-router'

const PASS_KEY = '__anbud_auth'
const authenticated = ref(sessionStorage.getItem(PASS_KEY) === '1')
const input = ref('')
const error = ref(false)

function login() {
  if (input.value === 'AndbudDemo') {
    sessionStorage.setItem(PASS_KEY, '1')
    authenticated.value = true
  } else {
    error.value = true
    setTimeout(() => (error.value = false), 1500)
  }
}
</script>

<template>
  <!-- Password gate -->
  <div v-if="!authenticated" class="gate">
    <div class="gate-box">
      <h1 class="gate-title">AnbudsMatcher</h1>
      <p class="gate-sub mono">Skriv inn passord for å fortsette</p>
      <form @submit.prevent="login" class="gate-form">
        <input
          v-model="input"
          type="password"
          placeholder="Passord"
          class="gate-input mono"
          :class="{ shake: error }"
          autofocus
        />
        <button type="submit" class="gate-btn mono">Logg inn</button>
      </form>
      <p v-if="error" class="gate-error mono">Feil passord</p>
    </div>
  </div>

  <!-- App -->
  <div v-else class="shell">
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

/* ── PASSWORD GATE ────────────────────────── */
.gate {
  display: flex; align-items: center; justify-content: center;
  min-height: 100vh;
}
.gate-box { text-align: center; max-width: 320px; }
.gate-title {
  font-family: 'Playfair Display', serif;
  font-size: 2rem; font-weight: 900; margin-bottom: 0.25rem;
}
.gate-sub { font-size: 0.7rem; color: var(--muted); letter-spacing: 0.06em; margin-bottom: 1.5rem; }
.gate-form { display: flex; flex-direction: column; gap: 0.6rem; }
.gate-input {
  padding: 0.6rem 0.8rem; font-size: 0.85rem;
  border: 1px solid var(--border); border-radius: 2px;
  background: var(--surf); color: var(--text);
  text-align: center; outline: none;
  transition: border-color 0.15s;
}
.gate-input:focus { border-color: var(--text); }
.gate-btn {
  padding: 0.55rem; font-size: 0.65rem; letter-spacing: 0.1em;
  border: 1px solid var(--text); border-radius: 2px;
  background: var(--text); color: var(--bg);
  cursor: pointer; transition: opacity 0.15s;
}
.gate-btn:hover { opacity: 0.85; }
.gate-error { color: #c44; font-size: 0.7rem; margin-top: 0.5rem; }
.shake { animation: shake 0.3s ease; }
@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-6px); }
  75% { transform: translateX(6px); }
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
</style>
