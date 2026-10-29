export const MODES = ['Calm Flow', 'Power Release', 'Breathe & Focus', 'Soundscape', 'Quick Session']

export const MODE_STYLE = {
  'Calm Flow':       { pill: 'mode-calm',  color: '#b5d09a', bar: 'sage-bar' },
  'Power Release':   { pill: 'mode-power', color: '#f2b6ca', bar: 'coral-bar' },
  'Breathe & Focus': { pill: 'mode-focus', color: '#c6bdf0', bar: 'lilac-bar' },
  'Soundscape':      { pill: 'mode-relax', color: '#cfe3ec', bar: 'blue-bar' },
  'Quick Session':   { pill: 'mode-quick', color: '#e2dc8f', bar: 'yellow-bar' },
}

export const MOODS = {
  Happy:  { emoji: '☀', cls: 'mood-happy', th: 'มีความสุข' },
  Lovely: { emoji: '✿', cls: 'mood-lovely', th: 'อบอุ่นใจ' },
  Bored:  { emoji: '☁', cls: 'mood-bored', th: 'เบื่อ' },
  Angry:  { emoji: '✸', cls: 'mood-angry', th: 'โกรธ' },
  Stress: { emoji: '〰', cls: 'mood-stress', th: 'เครียด' },
}

// คำอธิบายรูปแบบการฝึก
export const TRAINING_INFO = {
  'Calm Flow':       { label: 'ผ่อนลมหายใจให้ดอกไม้บาน' },
  'Power Release':   { label: 'เป่าลูกโป่งระบายพลัง' },
  'Breathe & Focus': { label: 'หายใจหลบสิ่งกีดขวาง' },
  'Soundscape':      { label: 'หายใจเข้า 5 ออก 5 พร้อมเสียง' },
  'Quick Session':   { label: 'หายใจ 4-7-8 บนตัวเครื่อง' },
}

export function pct(part, total) {
  part = Number(part || 0); total = Number(total || 0)
  return total ? Math.round((part / total) * 100) : 0
}

const AVATAR_COLORS = ['peach', 'green', 'lilac', 'yellow', 'blue', 'pink']

export function avatarColor(id = '') {
  let h = 0
  for (const c of String(id)) h = (h * 31 + c.charCodeAt(0)) >>> 0
  return AVATAR_COLORS[h % AVATAR_COLORS.length]
}

export function initials(name = '') {
  const parts = String(name).trim().split(/\s+/).filter(Boolean)
  if (!parts.length) return '?'
  return (parts[0][0] + (parts[1]?.[0] ?? '')).toUpperCase()
}

export function shortId(uuid = '') {
  return String(uuid).slice(0, 8).toUpperCase()
}

export function duration(sec = 0) {
  const s = Math.max(0, Math.round(Number(sec) || 0))
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`
}

export function number(n, digits = 0) {
  return Number(n || 0).toLocaleString('th-TH', { maximumFractionDigits: digits })
}

const tf = new Intl.DateTimeFormat('th-TH', { hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Bangkok' })
const df = new Intl.DateTimeFormat('th-TH', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'Asia/Bangkok' })
const dayKey = d => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Bangkok' }).format(d)

export function when(ts) {
  if (!ts) return '—'
  const d = new Date(ts)
  const today = dayKey(new Date())
  const yest = dayKey(new Date(Date.now() - 864e5))
  const k = dayKey(d)
  if (k === today) return `วันนี้ ${tf.format(d)}`
  if (k === yest) return `เมื่อวาน ${tf.format(d)}`
  return `${df.format(d)} ${tf.format(d)}`
}

export function longDate(d = new Date()) {
  return new Intl.DateTimeFormat('th-TH', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'Asia/Bangkok' }).format(d)
}

export function weekdayShort(isoDate) {
  return new Intl.DateTimeFormat('th-TH', { weekday: 'short', timeZone: 'UTC' }).format(new Date(isoDate + 'T00:00:00Z'))
}

export function dayOfMonth(isoDate) {
  return Number(isoDate.slice(8, 10))
}

export function trend(cur, prev) {
  cur = Number(cur || 0); prev = Number(prev || 0)
  if (!prev) return cur ? { text: 'ใหม่', up: true } : null
  const p = ((cur - prev) / prev) * 100
  return { text: `${p >= 0 ? '↗' : '↘'} ${Math.abs(p).toFixed(1)}%`, up: p >= 0 }
}

export function activityStatus(lastActive) {
  if (!lastActive) return 'inactive'
  const age = Date.now() - new Date(lastActive).getTime()
  if (age < 864e5) return 'active'
  if (age < 7 * 864e5) return 'idle'
  return 'inactive'
}

export const STATUS_LABEL = { active: 'ใช้งานวันนี้', idle: 'ใช้งานใน 7 วัน', inactive: 'ไม่ได้ใช้งาน' }

export function sessionDetail(s) {
  if (s.mode === 'Calm Flow' && s.flowers_grown > 0)
    return `ดอกไม้ ${s.flowers_grown} ดอก · ใหญ่สุด ${s.largest_flower_pct}% · เป่านานสุด ${(s.longest_exhale_ms / 1000).toFixed(1)} วิ`
  if (s.mode === 'Calm Flow' && s.flame_out_count != null) return `เปลวไฟดับ ${s.flame_out_count} ครั้ง`
  if (s.mode === 'Power Release' && s.balloons_total != null) return `เป่าลูกโป่งแตก ${s.balloons_popped}/${s.balloons_total} ลูก`
  if (s.mode === 'Breathe & Focus' && s.obstacles_avoided != null) return `หลบสิ่งกีดขวาง ${s.obstacles_avoided} ครั้ง`
  if (s.mode === 'Soundscape') return 'ฝึกหายใจ 5-5 พร้อมเสียง'
  if (s.mode === 'Quick Session') return 'ฝึกหายใจ 4-7-8 บนตัวเครื่อง'
  return '—'
}

export function downloadCsv(filename, header, rows) {
  const csv = '﻿' + [header, ...rows]
    .map(r => r.map(v => `"${String(v ?? '').replaceAll('"', '""')}"`).join(','))
    .join('\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }))
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}
