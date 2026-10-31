// =====================================================================
// โหลดผลการเล่นทั้งหมดในช่วงเวลา แล้วคำนวณตัวเลขที่ใช้ปรับปรุงเกม
// ทุกหน้าที่ใช้สถิติการเล่นใช้ข้อมูลชุดเดียวกัน (ตัวเลขตรงกันทุกหน้า)
// เกณฑ์ของ "ข้อสังเกต" ปรับได้ที่ THRESHOLDS ด้านล่าง
// =====================================================================
import { rpc } from './supabase'

export const MODES = ['Power Release', 'Breathe & Focus', 'Calm Flow', 'Soundscape', 'Quick Session']

const PAGE = 1000
const MAX_ROWS = 8000

let useV2 = true
async function fetchPage(days, offset) {
  const args = { p_mode: null, p_days: days, p_search: null, p_user_id: null, p_limit: PAGE, p_offset: offset }
  if (useV2) {
    try { return await rpc('dashboard_play_sessions_v2', args) ?? [] }
    catch (e) { if (!/dashboard_play_sessions_v2|schema cache|does not exist/i.test(e.message)) throw e; useV2 = false }
  }
  return await rpc('dashboard_play_sessions', args) ?? []
}

const cache = new Map()

/** ผลการเล่นของช่วงนี้ + ช่วงก่อนหน้า (ยาวเท่ากัน) สำหรับเทียบ */
export async function loadSessions(days, { force = false } = {}) {
  const key = String(days)
  if (!force && cache.has(key)) return cache.get(key)
  const span = days ? days * 2 : null
  const rows = []
  for (let offset = 0; offset < MAX_ROWS; offset += PAGE) {
    const page = await fetchPage(span, offset)
    rows.push(...page)
    if (page.length < PAGE) break
  }
  const cut = days ? Date.now() - days * 864e5 : -Infinity
  const result = {
    cur: rows.filter(r => new Date(r.played_at).getTime() >= cut),
    prev: days ? rows.filter(r => new Date(r.played_at).getTime() < cut) : [],
    truncated: rows.length >= MAX_ROWS,
  }
  cache.set(key, result)
  return result
}

export function clearSessionCache() { cache.clear() }

// ---------- ตัวช่วย ----------
const avg = a => (a.length ? a.reduce((x, y) => x + y, 0) / a.length : 0)
export const median = a => {
  if (!a.length) return 0
  const s = [...a].sort((x, y) => x - y)
  const m = Math.floor(s.length / 2)
  return s.length % 2 ? s[m] : (s[m - 1] + s[m]) / 2
}
const pctOf = (n, d) => (d ? Math.round((n / d) * 100) : 0)
const bkkDay = ts => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Bangkok' }).format(new Date(ts))

/** จัดกลุ่มเป็นช่วง: buckets = [{label, test(v)}] */
function histogram(values, buckets) {
  const out = buckets.map(b => ({ label: b.label, value: 0 }))
  for (const v of values) {
    const i = buckets.findIndex(b => b.test(v))
    if (i >= 0) out[i].value++
  }
  return out
}

// ---------- ภาพรวม ----------
export function summarize(rows) {
  const users = new Map()
  for (const r of rows) {
    if (!users.has(r.user_id)) users.set(r.user_id, new Set())
    users.get(r.user_id).add(bkkDay(r.played_at))
  }
  const durations = rows.map(r => r.duration || 0)
  const returning = [...users.values()].filter(d => d.size >= 2).length
  return {
    sessions: rows.length,
    players: users.size,
    minutes: durations.reduce((a, b) => a + b, 0) / 60,
    avgDuration: avg(durations),
    medianDuration: median(durations),
    returning,
    returningPct: pctOf(returning, users.size),
    perPlayer: users.size ? rows.length / users.size : 0,
  }
}

export function changePct(cur, prev) {
  if (!prev) return null
  return Math.round(((cur - prev) / prev) * 100)
}

/** จำนวนครั้งต่อวัน แยกโหมด (ล่าสุด n วัน) */
export function dailyByMode(rows, days) {
  const n = Math.min(days || 30, 60)
  const out = []
  const index = new Map()
  for (let i = n - 1; i >= 0; i--) {
    const key = bkkDay(Date.now() - i * 864e5)
    const d = { day: key, total: 0 }
    for (const m of MODES) d[m] = 0
    index.set(key, d)
    out.push(d)
  }
  for (const r of rows) {
    const d = index.get(bkkDay(r.played_at))
    if (d && d[r.mode] !== undefined) { d[r.mode]++; d.total++ }
  }
  return out
}

export function modeTable(rows) {
  const total = rows.length
  return MODES.map(mode => {
    const rs = rows.filter(r => r.mode === mode)
    return {
      mode,
      sessions: rs.length,
      players: new Set(rs.map(r => r.user_id)).size,
      avgDuration: avg(rs.map(r => r.duration || 0)),
      share: pctOf(rs.length, total),
      last: rs.reduce((a, r) => (!a || r.played_at > a ? r.played_at : a), null),
    }
  })
}

// ---------- รายละเอียดแต่ละโหมด ----------
export function modeDetail(mode, all) {
  const rs = all.filter(r => r.mode === mode)
  const durations = rs.map(r => r.duration || 0)
  const base = {
    sessions: rs.length,
    players: new Set(rs.map(r => r.user_id)).size,
    avgDuration: avg(durations),
    medianDuration: median(durations),
    metrics: [],
    charts: [],
  }
  if (!rs.length) return base

  if (mode === 'Power Release') {
    const g = rs.filter(r => r.balloons_total != null)
    const done = g.filter(r => r.balloons_total > 0 && r.balloons_popped >= r.balloons_total)
    const maxTotal = Math.max(1, ...g.map(r => r.balloons_total))
    base.completionPct = pctOf(done.length, g.length)
    base.metrics = [
      { label: 'เล่นจบครบทุกลูก', value: `${base.completionPct}%`, hint: `${done.length} จาก ${g.length} ครั้ง` },
      { label: 'ลูกโป่งที่เป่าแตก (เฉลี่ย)', value: `${avg(g.map(r => r.balloons_popped)).toFixed(1)} / ${avg(g.map(r => r.balloons_total)).toFixed(1)}` },
      { label: 'เวลาที่ใช้ตอนเล่นจบ (เฉลี่ย)', value: done.length ? secText(avg(done.map(r => r.duration))) : '—' },
      { label: 'คะแนนเฉลี่ย', value: avg(rs.map(r => r.score || 0)).toFixed(1) },
    ]
    base.charts = [{
      title: 'จำนวนลูกโป่งที่เป่าแตกต่อครั้ง',
      note: 'ถ้ากองอยู่ฝั่งน้อย แปลว่าเกมยากหรือเวลาไม่พอ',
      data: histogram(g.map(r => r.balloons_popped), Array.from({ length: maxTotal + 1 }, (_, i) => ({ label: `${i} ลูก`, test: v => v === i }))),
    }]
  }

  if (mode === 'Breathe & Focus') {
    const ob = rs.map(r => r.obstacles_avoided ?? 0)
    const early = rs.filter(r => (r.duration || 0) < 15)
    base.earlyPct = pctOf(early.length, rs.length)
    base.metrics = [
      { label: 'ข้ามสิ่งกีดขวาง (เฉลี่ย / มัธยฐาน)', value: `${avg(ob).toFixed(1)} / ${median(ob)}` },
      { label: 'สถิติสูงสุด', value: `${Math.max(...ob)} ชิ้น` },
      { label: 'อยู่รอดเฉลี่ย', value: secText(avg(durations)) },
      { label: 'ชนภายใน 15 วินาทีแรก', value: `${base.earlyPct}%`, hint: `${early.length} ครั้ง` },
    ]
    base.charts = [
      {
        title: 'จำนวนสิ่งกีดขวางที่ข้ามได้ต่อครั้ง',
        note: 'ช่วงที่คนส่วนใหญ่ชน = จุดที่ความยากเพิ่มเร็วเกินไป',
        data: histogram(ob, [
          { label: '0', test: v => v === 0 }, { label: '1–2', test: v => v >= 1 && v <= 2 },
          { label: '3–5', test: v => v >= 3 && v <= 5 }, { label: '6–10', test: v => v >= 6 && v <= 10 },
          { label: '11–20', test: v => v >= 11 && v <= 20 }, { label: '21+', test: v => v >= 21 },
        ]),
      },
      { title: 'อยู่รอดนานเท่าไร', data: histogram(durations, secBuckets([15, 30, 60, 120])) },
    ]
  }

  if (mode === 'Calm Flow') {
    const g = rs.filter(r => r.flowers_grown != null && r.flowers_grown >= 0)
    const full = g.filter(r => (r.largest_flower_pct ?? 0) >= 100)
    const exhale = g.map(r => (r.longest_exhale_ms ?? 0) / 1000)
    base.fullBloomPct = pctOf(full.length, g.length)
    base.metrics = [
      { label: 'ดอกไม้ต่อครั้ง (เฉลี่ย)', value: avg(g.map(r => r.flowers_grown)).toFixed(1) },
      { label: 'บานเต็ม 100% อย่างน้อย 1 ดอก', value: `${base.fullBloomPct}%`, hint: `${full.length} จาก ${g.length} ครั้ง` },
      { label: 'หายใจออกนานสุด (เฉลี่ย)', value: `${avg(exhale).toFixed(1)} วิ` },
      { label: 'ไม่มีดอกไม้บานเลย', value: `${pctOf(g.filter(r => r.flowers_grown === 0).length, g.length)}%` },
    ]
    base.charts = [
      {
        title: 'หายใจออกนานสุดต่อครั้ง',
        note: 'ใช้ตั้งค่าเวลาที่ต้องเป่าให้ดอกไม้บานเต็ม',
        data: histogram(exhale, [
          { label: '< 2 วิ', test: v => v < 2 }, { label: '2–4 วิ', test: v => v >= 2 && v < 4 },
          { label: '4–6 วิ', test: v => v >= 4 && v < 6 }, { label: '6–8 วิ', test: v => v >= 6 && v < 8 },
          { label: '8+ วิ', test: v => v >= 8 },
        ]),
      },
      {
        title: 'ระยะเวลาที่เล่นจริง',
        data: histogram(durations, [
          { label: '< 1 นาที', test: v => v < 55 }, { label: '≈ 1 นาที', test: v => v >= 55 && v < 100 },
          { label: '≈ 2 นาที', test: v => v >= 100 && v < 160 }, { label: '≈ 3 นาที+', test: v => v >= 160 },
        ]),
      },
    ]
  }

  if (mode === 'Soundscape') {
    const scenes = new Map()
    for (const r of rs) {
      const k = r.scene || 'ไม่ระบุฉาก'
      if (!scenes.has(k)) scenes.set(k, [])
      scenes.get(k).push(r)
    }
    const early = rs.filter(r => (r.duration || 0) < 60)
    base.earlyPct = pctOf(early.length, rs.length)
    base.metrics = [
      { label: 'เลิกภายใน 1 นาที', value: `${base.earlyPct}%`, hint: `${early.length} ครั้ง` },
      { label: 'รอบหายใจต่อครั้ง (เฉลี่ย)', value: avg(rs.map(r => Math.max(0, r.breath_cycles ?? 0))).toFixed(1) },
      { label: 'เวลาที่เป่าต่อครั้ง (เฉลี่ย)', value: `${(avg(rs.map(r => Math.max(0, r.exhale_ms ?? 0))) / 1000).toFixed(1)} วิ` },
      { label: 'เวลาต่อครั้ง (มัธยฐาน)', value: secText(base.medianDuration) },
    ]
    base.charts = [
      {
        title: 'แยกตามฉาก (จำนวนครั้ง)',
        data: [...scenes.entries()].sort((a, b) => b[1].length - a[1].length)
          .map(([k, v]) => ({ label: k, value: v.length, extra: `เฉลี่ย ${secText(avg(v.map(r => r.duration || 0)))}` })),
      },
      { title: 'ฝึกนานเท่าไรต่อครั้ง', note: 'ถ้าส่วนใหญ่สั้นมาก ลองดูว่าเสียง/ภาพทำให้อยู่ต่อได้หรือไม่', data: histogram(durations, secBuckets([60, 180, 300, 600])) },
    ]
  }

  if (mode === 'Quick Session') {
    const g = rs.filter(r => r.rounds != null)
    const done = g.filter(r => r.rounds >= 4)
    const blow = rs.filter(r => r.blow_pct != null).map(r => r.blow_pct)
    base.completionPct = pctOf(done.length, g.length)
    base.metrics = [
      { label: 'ฝึกครบ 4 รอบ', value: g.length ? `${base.completionPct}%` : '—', hint: g.length ? `${done.length} จาก ${g.length} ครั้ง` : '' },
      { label: 'เป่าได้ตอนหายใจออก (เฉลี่ย)', value: blow.length ? `${Math.round(avg(blow))}%` : '—', hint: 'สัดส่วนเวลาที่เป่าจริงในช่วงหายใจออก 8 วิ' },
      { label: 'ครั้งต่อผู้เล่น', value: (rs.length / Math.max(1, base.players)).toFixed(1) },
      { label: 'เวลาต่อครั้ง (เฉลี่ย)', value: secText(base.avgDuration) },
    ]
    base.charts = [{
      title: 'เป่าได้กี่ % ตอนหายใจออก',
      note: 'ต่ำ = ผู้ใช้อาจไม่รู้ว่าต้องเป่าใส่ไมค์ หรือไมค์ไม่ไวพอ',
      data: histogram(blow, [
        { label: '0–24%', test: v => v < 25 }, { label: '25–49%', test: v => v >= 25 && v < 50 },
        { label: '50–74%', test: v => v >= 50 && v < 75 }, { label: '75–100%', test: v => v >= 75 },
      ]),
    }]
  }
  return base
}

function secBuckets(edges) {
  const fmt = s => (s >= 60 ? `${s / 60} นาที` : `${s} วิ`)
  const out = [{ label: `< ${fmt(edges[0])}`, test: v => v < edges[0] }]
  for (let i = 0; i < edges.length - 1; i++) {
    const a = edges[i], b = edges[i + 1]
    out.push({ label: `${fmt(a)}–${fmt(b)}`, test: v => v >= a && v < b })
  }
  out.push({ label: `${fmt(edges[edges.length - 1])}+`, test: v => v >= edges[edges.length - 1] })
  return out
}

export function secText(s) {
  s = Math.round(s || 0)
  if (s < 60) return `${s} วิ`
  return `${Math.floor(s / 60)} นาที ${s % 60 ? (s % 60) + ' วิ' : ''}`.trim()
}

// ---------- ข้อสังเกต (เรียงสำคัญก่อน) ----------
export const THRESHOLDS = {
  minSessions: 5,              // ต่ำกว่านี้ยังไม่สรุป
  powerCompletionLow: 40,      // % เล่นจบครบ ต่ำกว่านี้ = ยากไป
  powerCompletionHigh: 95,     // สูงกว่านี้ = ง่ายไป
  focusEarlyHigh: 40,          // % ชนใน 15 วิแรก
  calmFullBloomLow: 25,        // % บานเต็ม
  soundEarlyHigh: 50,          // % เลิกใน 1 นาที
  quickCompletionLow: 60,      // % ครบ 4 รอบ
  quickBlowLow: 40,            // % เป่าได้
  returningLow: 30,            // % กลับมาเล่นซ้ำ
}

export function findings(cur, prev) {
  const T = THRESHOLDS
  const out = []
  const s = summarize(cur), p = summarize(prev)
  if (!cur.length) return [{ level: 'info', text: 'ยังไม่มีการเล่นในช่วงเวลานี้' }]

  const enough = m => m.sessions >= T.minSessions
  const d = Object.fromEntries(MODES.map(m => [m, modeDetail(m, cur)]))

  const pr = d['Power Release']
  if (enough(pr) && pr.completionPct < T.powerCompletionLow)
    out.push({ level: 'warn', mode: 'Power Release', text: `เล่นจบครบทุกลูกเพียง ${pr.completionPct}% — อาจยากเกินไป ลองลดจำนวนลูกโป่งหรือเพิ่มเวลา` })
  if (enough(pr) && pr.completionPct > T.powerCompletionHigh)
    out.push({ level: 'info', mode: 'Power Release', text: `เล่นจบครบ ${pr.completionPct}% — อาจง่ายเกินไป ลองเพิ่มลูกโป่งหรือลดเวลา` })

  const bf = d['Breathe & Focus']
  if (enough(bf) && bf.earlyPct > T.focusEarlyHigh)
    out.push({ level: 'warn', mode: 'Breathe & Focus', text: `${bf.earlyPct}% ชนภายใน 15 วินาทีแรก — ช่วงเริ่มเกมอาจยากเกินไป` })

  const cf = d['Calm Flow']
  if (enough(cf) && cf.fullBloomPct < T.calmFullBloomLow)
    out.push({ level: 'warn', mode: 'Calm Flow', text: `บานเต็ม 100% ได้เพียง ${cf.fullBloomPct}% ของครั้งที่เล่น — ลองลดเวลาที่ต้องเป่าให้บานเต็ม` })

  const ss = d['Soundscape']
  if (enough(ss) && ss.earlyPct > T.soundEarlyHigh)
    out.push({ level: 'warn', mode: 'Soundscape', text: `${ss.earlyPct}% เลิกภายใน 1 นาที — ผู้เล่นอาจไม่เข้าใจจังหวะหายใจ หรือยังไม่อิน` })

  const qs = d['Quick Session']
  if (enough(qs) && qs.completionPct < T.quickCompletionLow)
    out.push({ level: 'warn', mode: 'Quick Session', text: `ฝึกครบ 4 รอบเพียง ${qs.completionPct}% — ลองดูว่าหยุดกลางคันช่วงไหน` })

  const unused = MODES.filter(m => d[m].sessions === 0)
  if (unused.length && unused.length < MODES.length)
    out.push({ level: 'info', text: `ไม่มีใครเล่น ${unused.join(', ')} ในช่วงนี้` })

  if (s.players >= T.minSessions && s.returningPct < T.returningLow)
    out.push({ level: 'warn', text: `ผู้เล่นกลับมาเล่นซ้ำ (มากกว่า 1 วัน) เพียง ${s.returningPct}%` })

  const ch = changePct(s.sessions, p.sessions)
  if (ch !== null && p.sessions >= T.minSessions && ch <= -30)
    out.push({ level: 'warn', text: `จำนวนครั้งที่เล่นลดลง ${Math.abs(ch)}% จากช่วงก่อนหน้า` })

  if (!out.length) out.push({ level: 'ok', text: 'ยังไม่พบจุดที่ต้องปรับในช่วงนี้' })
  return out
}
