// Fetch from API first, fall back to static JSON data (for Vercel/demo deployment)
async function fetchWithFallback(apiPath, staticPath) {
  try {
    const res = await fetch(apiPath)
    if (res.ok) return await res.json()
  } catch {
    // API not available — fall through to static data
  }
  const res = await fetch(staticPath)
  if (!res.ok) throw new Error(`Failed to load ${staticPath}: ${res.status}`)
  return await res.json()
}

export async function loadAllData() {
  const [reportsData, matchesData, noticesData] = await Promise.all([
    fetchWithFallback('/api/reports', '/data/reports.json'),
    fetchWithFallback('/api/matches', '/data/matches.json'),
    fetchWithFallback('/api/notices', '/data/notices.json'),
  ])
  return {
    reports: reportsData.reports ?? [],
    matches: matchesData.matches ?? [],
    notices: noticesData.notices ?? [],
  }
}
