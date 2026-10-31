// ข้อมูลจำลองสำหรับดูหน้าตา (npm run dev -- --mode mock หรือ VITE_MOCK=1)
const MODES = ['Power Release', 'Breathe & Focus', 'Calm Flow', 'Soundscape', 'Quick Session']
const NAMES = ['chuu', 'pm', 'mint', 'beam', 'ploy', 'nut', 'fah', 'tan', 'gift', 'bank', 'jane', 'pond']
const MOODS = ['Happy', 'Lovely', 'Bored', 'Angry', 'Stress']
let seed = 7
const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647)
const pick = a => a[Math.floor(rnd() * a.length)]
const users = NAMES.map((n, i) => ({ user_id: `u-${i}`, name: n, email: `${n}@mail.com`, created_at: new Date(Date.now() - (40 - i * 3) * 864e5).toISOString() }))

const sessions = []
for (let i = 0; i < 420; i++) {
  const u = users[Math.floor(Math.pow(rnd(), 1.6) * users.length)]
  const mode = pick(['Power Release', 'Power Release', 'Breathe & Focus', 'Breathe & Focus', 'Calm Flow', 'Soundscape', 'Quick Session'])
  const t = Date.now() - Math.pow(rnd(), 1.3) * 55 * 864e5
  const s = { session_id: 1000 + i, user_id: u.user_id, player: u.name, mode, score: 0, duration: 30, played_at: new Date(t).toISOString(), device_name: 'Breath Gadget' }
  if (mode === 'Power Release') { s.balloons_total = 5; s.balloons_popped = Math.min(5, Math.floor(rnd() * 6.5)); s.score = s.balloons_popped * 2; s.duration = 20 + Math.floor(rnd() * 40) }
  if (mode === 'Breathe & Focus') { s.obstacles_avoided = Math.floor(Math.pow(rnd(), 2) * 25); s.score = s.obstacles_avoided; s.duration = 6 + s.obstacles_avoided * 3 }
  if (mode === 'Calm Flow') { s.flowers_grown = Math.floor(rnd() * 6); s.largest_flower_pct = Math.min(100, 40 + Math.floor(rnd() * 70)); s.longest_exhale_ms = 1500 + Math.floor(rnd() * 8000); s.score = s.flowers_grown * 10; s.duration = pick([60, 60, 120, 180]) }
  if (mode === 'Soundscape') { s.scene = pick(['Rainy Forest', 'Windy Mountain']); s.duration = 20 + Math.floor(Math.pow(rnd(), 1.5) * 500); s.breath_cycles = Math.floor(s.duration / 10); s.exhale_ms = s.duration * 300 }
  if (mode === 'Quick Session') { s.rounds = rnd() < 0.75 ? 4 : Math.floor(rnd() * 4); s.blow_pct = Math.floor(rnd() * 100); s.duration = 25 + s.rounds * 19; s.source = 'gadget' }
  sessions.push(s)
}
sessions.sort((a, b) => b.played_at.localeCompare(a.played_at))
const moods = Array.from({ length: 90 }, (_, i) => {
  const u = pick(users)
  return { mood_id: i + 1, user_id: u.user_id, player: u.name, mood: pick(MOODS), note: rnd() < 0.3 ? 'วันนี้งานเยอะ' : '', recorded_at: new Date(Date.now() - rnd() * 40 * 864e5).toISOString() }
}).sort((a, b) => b.recorded_at.localeCompare(a.recorded_at))

const within = (ts, days) => !days || new Date(ts).getTime() >= Date.now() - days * 864e5
const page = (rows, a) => rows.slice(a.p_offset || 0, (a.p_offset || 0) + (a.p_limit || 50)).map(r => ({ ...r, total_count: rows.length }))

export function mockRpc(name, a = {}) {
  switch (name) {
    case 'dashboard_me': return { user_id: 'u-0', name: 'chuu', email: 'chuu@mail.com', role: 'owner' }
    case 'dashboard_play_sessions_v2':
    case 'dashboard_play_sessions': {
      let rows = sessions.filter(s => within(s.played_at, a.p_days))
      if (a.p_mode) rows = rows.filter(s => s.mode === a.p_mode)
      if (a.p_user_id) rows = rows.filter(s => s.user_id === a.p_user_id)
      if (a.p_search) rows = rows.filter(s => s.player.includes(a.p_search))
      return page(rows, a)
    }
    case 'dashboard_players': {
      let rows = users.map(u => {
        const ss = sessions.filter(s => s.user_id === u.user_id)
        const ms = moods.filter(m => m.user_id === u.user_id)
        return { ...u, sessions: ss.length, minutes: Math.round(ss.reduce((x, s) => x + s.duration, 0) / 6) / 10, best_score: Math.max(0, ...ss.map(s => s.score)), last_mood: ms[0]?.mood ?? null, last_active: ss[0]?.played_at ?? null, battery_level: 70 }
      })
      if (a.p_search) rows = rows.filter(r => r.name.includes(a.p_search))
      rows.sort((x, y) => (y.last_active || '').localeCompare(x.last_active || ''))
      return page(rows, a)
    }
    case 'dashboard_moods': {
      let rows = moods.filter(m => within(m.recorded_at, a.p_days))
      if (a.p_search) rows = rows.filter(m => m.player.includes(a.p_search))
      return page(rows, a)
    }
    case 'dashboard_devices':
      return users.slice(0, 7).map((u, i) => ({ device_id: `d-${i}`, device_name: i ? `${u.name}'s Gadget` : 'Breath Gadget', device_icon: 'cat|#A9C77E', owner_name: u.name, user_id: u.user_id, battery_level: [92, 15, 64, null, 48, 8, 77][i], connection_status: i % 3 ? 'disconnected' : 'connected', is_online: i % 3 === 0, updated_at: new Date(Date.now() - i * 7200e3).toISOString(), sessions: 10 + i * 4 }))
    case 'staff_list':
      return [{ user_id: 'u-0', email: 'chuu@mail.com', name: 'chuu', role: 'owner', created_at: users[0].created_at, last_sign_in_at: new Date().toISOString() }, { user_id: 'u-1', email: 'gift@mail.com', name: 'gift', role: 'admin', created_at: users[1].created_at, last_sign_in_at: new Date(Date.now() - 864e5).toISOString() }]
    default: return null
  }
}
