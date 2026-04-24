# Plan: Forbedret matchingsalgoritme

**Status:** Klar til implementering  
**Opprettet:** 2026-04-24  
**Konfidensestimat:** 80%

---

## Bakgrunn

Den nåværende algoritmen i `RiksrevisjonApi/Program.cs` (`MatchService`) produserer mange falske positiver. Vi analyserte 201 eksisterende matcher manuelt med GPT-5.4 og identifiserte 4 rotårsaker, og erstattet dem med 96 manuelt kuraterte matcher (commit `aa890cf`).

**Kjente falske positiver (eksempler):**
- F-35-rapport → Sceneteknikk Militær Tattoo
- EPJ-rapport → pasienttransport
- Forsvarets informasjonssystemer → Sealift Charter

**Rotårsaker:**
1. `deptScore` bruker brede domenekeywords ("helse", "forsvar") som scoringssignal → blåser opp score for urelaterte kunngjøringer
2. `ExtractKeywords` henter generiske ord (alt ≥3 tegn, ikke stoppord) → "statlig", "system", "drift" matcher alt
3. Terskel på 40 er for lavt
4. Ingen per-rapport tak på antall matcher

---

## Løsning: 6 faser i `MatchService`

### Fase 1 — IDF-filtrert nøkkelordekstraksjon
**Fil:** `Program.cs`, `ExtractKeywords()`  
**Endring:** Ta kun topp-12 ord med høyest IDF-score (IDF > 2.0).

IDF måler hvor sjelden et ord er på tvers av alle 4974 kunngjøringer. Høy IDF = spesifikt.  
Resultat: "sealift", "kryptovaluta", "fosterhjem" vinner over "statlig", "digital", "drift".

```csharp
// Nåværende
return TokenRegex.Split(text).Where(w => w.Length >= 3 && !Stopwords.Contains(w)).Distinct().ToList();

// Ny
return TokenRegex.Split(text)
    .Where(w => w.Length >= 3 && !Stopwords.Contains(w))
    .Distinct()
    .OrderByDescending(w => idf.GetValueOrDefault(Stem(w), 1.0))
    .Where(w => idf.GetValueOrDefault(Stem(w), 1.0) > 2.0)
    .Take(12)
    .ToList();
```

### Fase 2 — Hard domenefilter (erstatter deptScore)
**Fil:** `Program.cs`, `ComputeMatches()`  
**Endring:** Fjern deptScore fra formelen. Bruk domenekeywords som pre-filter gate.

```csharp
// Erstatt deptScore-beregning med:
if (deptKeywords.Count > 0 && !deptKeywords.Any(kw => text.Contains(kw)))
    continue; // hopp over kunngjøringen — feil domene
```

Eliminerer kryssdomene falske positiver uten å påvirke gode matcher.

### Fase 3 — Ny scoringsformel + høyere terskel
**Fil:** `Program.cs`, `ComputeMatches()`

| | Nåværende | Ny |
|---|---|---|
| Formel | `key×0.35 + bigram×0.10 + org×0.20 + title×0.20 + dept×0.15` | `key×0.50 + bigram×0.30 + title×0.20` |
| Terskel | `> 40` | `> 55` |
| Min keyword-treff | 3 | 2 (IDF-kvalitet kompenserer) |

Bigram-vekten øker fordi eksakte bigramtreff ("bane nor", "nav tilgang") er et sterkt presisjonssignal.

### Fase 4 — Topp 5 per rapport
**Fil:** `Program.cs`, `ComputeMatches()`

```csharp
// Etter all scoring, gruppert per rapport:
return results
    .GroupBy(m => m.ReportId)
    .SelectMany(g => g.OrderByDescending(m => m.Score).Take(5))
    .ToList();
```

### Fase 5 — Validering mot kjente matcher
Kjør ny algoritme mot de 96 manuelt kuraterte matchene (i `public/data/matches.json`).  
**Suksesskriterium:** ≥70% av kjente gode par får score > 55.  
Juster terskel/vekter ved behov.

### Fase 6 — Enhetstester
**Fil:** `RiksrevisjonApi.Tests/MatchServiceTests.cs` (sjekk om testprosjekt eksisterer)

Tester:
- Kjente gode par → score > 55
- Kjente falske positiver → score < 55 eller filtrert ut av domenefilter
- `ExtractKeywords` med IDF returnerer spesifikke ord, ikke generiske

---

## Berørte filer

| Fil | Type endring |
|---|---|
| `RiksrevisjonApi/Program.cs` | ~30 linjer endret i `MatchService` |
| `RiksrevisjonApi.Tests/` | Ny testfil (hvis testprosjekt finnes) |

---

## Konfidensestimat: 80%

**Sikkert (95%):**
- Endringene er enkle og lokaliserte
- IDF-filtrering er velprøvd NLP-teknikk
- Domenefilter-gate er deterministisk

**Risiko (20%):**
- Terskeltuning (55 kan trenge justering)
- Norsk komposittord-stemming er forenklet
- Ingen eksisterende testsuite å bygge på

**Risikomitigering:**
- Validere mot 96 kuraterte matcher som fasit
- Statiske `matches.json`/`notices.json` i dashboardet er fallback
- All logikk er i én fil og enkelt reverterbar

---

## Hvordan plukke opp planen

1. Åpne `RiksrevisjonApi/Program.cs`, finn `class MatchService` (~linje 773)
2. Følg fasene i rekkefølge: Fase 1 → 2 → 3 → 4 → 5 → 6
3. De 96 kuraterte matchene i `riksrevisjon-dashboard/public/data/matches.json` brukes som valideringsfasit
