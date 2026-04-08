import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import './style.css'
import App from './App.vue'
import DashboardView from './views/DashboardView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: DashboardView },
    {
      path: '/matcher',
      component: () => import('./views/MatcherView.vue')
    }
  ]
})

const app = createApp(App)
app.use(router)
app.mount('#app')
