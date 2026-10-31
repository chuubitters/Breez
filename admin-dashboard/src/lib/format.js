export const MODES = ['Power Release', 'Breathe & Focus', 'Calm Flow', 'Soundscape', 'Quick Session']

/** สีประจำโหมด (จุด/แท่งกราฟ) */
export const MODE_COLOR = {
  'Power Release': 'var(--m-power)',
  'Breathe & Focus': 'var(--m-focus)',
  'Calm Flow': 'var(--m-calm)',
  'Soundscape': 'var(--m-sound)',
  'Quick Session': 'var(--m-quick)',
}

/** คำอธิบายสั้นของแต่ละโหมด */
export const MODE_INFO = {
  'Power Release': 'เป่าลูกโป่งให้แตกครบทุกลูกภายในเวลาที่กำหนด',
  'Breathe & Focus': 'ควบคุมลมหายใจให้ผึ้งลอยไปเก็บดอกไม้',
  'Calm Flow': 'หายใจออกยาวและสม่ำเสมอเพื่อให้ดอกไม้บาน',
  'Soundscape': 'หายใจเข้า 5 วินาที ออก 5 วินาที พร้อมเสียงธรรมชาติ',
  'Quick Session': 'ฝึกหายใจแบบ 4-7-8 บนอุปกรณ์ Breath Gadget',
}

export const MOODS = {
  Happy:  { th: 'มีความสุข', color: '#e3b93f' },
  Lovely: { th: 'อบอุ่นใจ', color: '#e8899e' },
  Bored:  { th: 'เบื่อ', color: '#8fb35a' },
  Angry:  { th: 'โกรธ', color: '#c9564f' },
  Stress: { th: 'เครียด', color: '#8a72c4' },
}

export function pct(part, total) {
  part = Number(part || 0); total = Number(total || 0)
  return total ? Math.round((part / total) * 100) : 0
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

/** สถานะผู้เล่นจากวันที่ใช้งานล่าสุด */
export function activity(lastActive) {
  if (!lastActive) return { cls: '', text: 'ยังไม่เคยใช้งาน' }
  const days = (Date.now() - new Date(lastActive).getTime()) / 864e5
  if (days < 7) return { cls: 'on', text: 'ใช้งานภายใน 7 วัน' }
  if (days < 30) return { cls: 'idle', text: 'ไม่ได้ใช้งาน 1–4 สัปดาห์' }
  return { cls: '', text: 'ไม่ได้ใช้งานเกิน 30 วัน' }
}

const sec1 = ms => (Number(ms || 0) / 1000).toFixed(1)

/** ผลเฉพาะของแต่ละโหมด (1 บรรทัด) */
export function sessionDetail(s) {
  switch (s.mode) {
    case 'Calm Flow':
      if (s.flowers_grown > 0) return `ดอกไม้ ${s.flowers_grown} ดอก · ใหญ่สุด ${s.largest_flower_pct}% · เป่านานสุด ${sec1(s.longest_exhale_ms)} วินาที`
      if (s.flowers_grown === 0) return 'ไม่มีดอกไม้บาน'
      return s.flame_out_count != null ? `เปลวไฟดับ ${s.flame_out_count} ครั้ง` : '—'
    case 'Power Release':
      if (s.balloons_total == null) return '—'
      return `ลูกโป่ง ${s.balloons_popped}/${s.balloons_total} ลูก${s.balloons_popped >= s.balloons_total && s.balloons_total > 0 ? ' · สำเร็จครบทุกลูก' : ''}`
    case 'Breathe & Focus':
      return s.obstacles_avoided != null ? `เก็บดอกไม้ ${s.obstacles_avoided} ดอก` : '—'
    case 'Soundscape': {
      const parts = []
      if (s.scene) parts.push(s.scene)
      if (s.breath_cycles > 0) parts.push(`หายใจครบ ${s.breath_cycles} รอบ`)
      if (s.exhale_ms > 0) parts.push(`หายใจออก ${sec1(s.exhale_ms)} วินาที`)
      return parts.length ? parts.join(' · ') : 'ฝึกหายใจ 5-5 พร้อมเสียงธรรมชาติ'
    }
    case 'Quick Session': {
      const parts = []
      if (s.rounds != null) parts.push(`${s.rounds}/4 รอบ`)
      if (s.blow_pct != null) parts.push(`หายใจออกได้ ${s.blow_pct}%`)
      return parts.length ? parts.join(' · ') : 'ฝึกหายใจแบบ 4-7-8 บนอุปกรณ์'
    }
    default:
      return '—'
  }
}

export function seconds(ms, digits = 1) {
  return (Number(ms || 0) / 1000).toLocaleString('th-TH', { maximumFractionDigits: digits })
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
