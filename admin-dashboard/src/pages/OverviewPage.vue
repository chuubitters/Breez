<script setup>
import { computed, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { MODE_STYLE, MOODS, avatarColor, dayOfMonth, duration, initials, longDate, number, trend, weekdayShort, when } from '../lib/format'

const props = defineProps({ me: Object })
const emit = defineEmits(['navigate'])

const period = ref(7)
const data = ref(null)
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await rpc('dashboard_overview', { p_days: period.value })
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}
watch(period, load, { immediate: true })

const periodLabel = computed(() => period.value ? `${period.value} วันล่าสุด` : 'ทั้งหมด')
const t = computed(() => ({
  sessions: trend(data.value?.sessions, data.value?.sessions_prev),
  minutes: trend(data.value?.minutes, data.value?.minutes_prev),
  players: trend(data.value?.players_new, data.value?.players_new_prev),
}))

const chartSeries = [
  { key: 'calm', label: 'Calm Flow', cls: 'sage-bar', legend: 'legend-sage' },
  { key: 'power', label: 'Power Release', cls: 'coral-bar', legend: 'legend-coral' },
  { key: 'focus', label: 'Breathe & Focus', cls: 'lilac-bar', legend: 'legend-lilac' },
  { key: 'relax', label: 'Soundscape / Quick', cls: 'yellow-bar', legend: 'legend-blue' },
]
const daily = computed(() => data.value?.daily ?? [])
const chartMax = computed(() => {
  const m = Math.max(1, ...daily.value.flatMap(d => chartSeries.map(s => d[s.key])))
  const step = Math.ceil(m / 4)
  return step * 4
})
const yLabels = computed(() => [4, 3, 2, 1, 0].map(i => (chartMax.value / 4) * i))
const xLabel = d => daily.value.length > 10 ? dayOfMonth(d.day) : weekdayShort(d.day)

const modes = computed(() => {
  const list = data.value?.modes ?? []
  const total = list.reduce((a, m) => a + m.count, 0)
  return { total, list: list.map(m => ({ ...m, percent: total ? Math.round((m.count / total) * 100) : 0, color: MODE_STYLE[m.mode]?.color ?? '#ccc' })) }
})
const donut = computed(() => {
  if (!modes.value.total) return 'conic-gradient(#eef1ea 0 100%)'
  let acc = 0
  const stops = modes.value.list.map(m => {
    const from = acc
    acc += (m.count / modes.value.total) * 100
    return `${m.color} ${from}% ${acc}%`
  })
  return `conic-gradient(${stops.join(',')})`
})
const moodTotal = computed(() => (data.value?.moods ?? []).reduce((a, m) => a + m.count, 0))
</script>

<template>
  <section class="welcome-row">
    <div>
      <div class="eyebrow"><span class="eyebrow-dot"></span> ADMIN OVERVIEW <span class="date-label">· {{ longDate() }}</span></div>
      <h1>สวัสดี {{ me.name }} ภาพรวมที่ดี เริ่มจาก<span class="highlight">ลมหายใจ</span> <span class="wave">〰</span></h1>
      <p class="page-description">ติดตามการใช้งาน breez และการฝึกหายใจของผู้เล่นได้ในที่เดียว</p>
    </div>
    <div class="period-select">
      <span>ช่วงเวลา</span>
      <select v-model="period"><option :value="7">7 วันล่าสุด</option><option :value="30">30 วันล่าสุด</option><option :value="null">ทั้งหมด</option></select>
      <span class="calendar">▦</span>
    </div>
  </section>

  <p v-if="error" class="banner-error">{{ error }} <button class="text-button" @click="load">ลองใหม่</button></p>

  <section class="stats-grid" :class="{ loading }">
    <article class="stat-card stat-lilac">
      <div class="stat-top"><span class="stat-label">ผู้เล่นทั้งหมด</span><span class="stat-icon">♙</span></div>
      <div class="stat-value">{{ number(data?.players_total) }}</div>
      <div class="stat-foot"><span class="trend" v-if="data?.players_new">+{{ number(data.players_new) }} คน</span><span>ใหม่ · {{ periodLabel }}</span></div>
    </article>
    <article class="stat-card stat-green">
      <div class="stat-top"><span class="stat-label">จำนวนครั้งที่เล่น</span><span class="stat-icon">▷</span></div>
      <div class="stat-value">{{ number(data?.sessions) }}</div>
      <div class="stat-foot"><span v-if="t.sessions" class="trend" :class="{ down: !t.sessions.up }">{{ t.sessions.text }}</span><span>{{ period ? 'เทียบกับช่วงก่อน' : 'ทั้งหมด' }}</span></div>
    </article>
    <article class="stat-card stat-peach">
      <div class="stat-top"><span class="stat-label">เวลาฝึกหายใจรวม</span><span class="stat-icon">◷</span></div>
      <div class="stat-value">{{ data?.minutes >= 120 ? number(data.minutes / 60, 1) : number(data?.minutes, 1) }}<span class="stat-unit">{{ data?.minutes >= 120 ? 'ชม.' : 'นาที' }}</span></div>
      <div class="stat-foot"><span v-if="t.minutes" class="trend" :class="{ down: !t.minutes.up }">{{ t.minutes.text }}</span><span>ผู้เล่นที่ใช้งาน {{ number(data?.active_players) }} คน</span></div>
    </article>
    <article class="stat-card stat-yellow">
      <div class="stat-top"><span class="stat-label">อุปกรณ์ออนไลน์</span><span class="stat-icon">⌁</span></div>
      <div class="stat-value">{{ number(data?.devices_online) }}<span class="stat-unit">/ {{ number(data?.devices_total) }}</span></div>
      <div class="stat-foot"><span class="online-dot"></span><span>{{ data?.devices_total ? Math.round((data.devices_online / data.devices_total) * 100) : 0 }}% กำลังเชื่อมต่อ</span></div>
      <div class="device-progress"><span :style="{ width: (data?.devices_total ? (data.devices_online / data.devices_total) * 100 : 0) + '%' }"></span></div>
    </article>
  </section>

  <section class="dashboard-grid">
    <article class="panel activity-panel">
      <div class="panel-heading">
        <div><h2>กิจกรรมการฝึกหายใจ</h2><p>จำนวน session ต่อวัน ({{ daily.length }} วันล่าสุด)</p></div>
        <button class="text-button" @click="emit('navigate', 'sessions')">ดูประวัติทั้งหมด <span>↗</span></button>
      </div>
      <div class="chart-legend"><span v-for="s in chartSeries" :key="s.key"><i class="legend-dot" :class="s.legend"></i>{{ s.label }}</span></div>
      <div class="activity-chart">
        <div class="y-labels"><span v-for="y in yLabels" :key="y">{{ number(y) }}</span></div>
        <div class="chart-body">
          <div class="grid-lines"><i></i><i></i><i></i><i></i><i></i></div>
          <div class="bar-groups">
            <div v-for="d in daily" :key="d.day" class="bar-group" :title="`${d.day}: ${chartSeries.map(s => s.label + ' ' + d[s.key]).join(', ')}`">
              <div class="bars">
                <span v-for="s in chartSeries" :key="s.key" class="bar" :class="s.cls" :style="{ height: (d[s.key] / chartMax) * 100 + '%' }"></span>
              </div>
              <span class="x-label">{{ xLabel(d) }}</span>
            </div>
          </div>
        </div>
      </div>
    </article>

    <article class="panel modes-panel">
      <div class="panel-heading"><div><h2>โหมดที่ผู้เล่นชอบ</h2><p>สัดส่วนการเล่นแยกตามโหมด · {{ periodLabel }}</p></div></div>
      <div class="donut-wrap"><div class="donut" :style="{ background: donut }"><div class="donut-center"><strong>{{ number(modes.total) }}</strong><span>sessions</span></div></div></div>
      <div class="mode-list">
        <div v-for="m in modes.list" :key="m.mode" class="mode-row"><span class="mode-bullet" :style="{ background: m.color }"></span><span class="mode-name">{{ m.mode }}</span><strong>{{ m.percent }}%</strong></div>
        <p v-if="!modes.list.length" class="empty-inline">ยังไม่มีการเล่นในช่วงนี้</p>
      </div>
    </article>
  </section>

  <section class="lower-grid">
    <article class="panel sessions-panel">
      <div class="panel-heading"><div><h2>การเล่นล่าสุด</h2><p>กิจกรรมล่าสุดจากผู้เล่น</p></div><button class="text-button" @click="emit('navigate', 'sessions')">ดูทั้งหมด <span>↗</span></button></div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>ผู้เล่น</th><th>โหมดเกม</th><th>คะแนน</th><th>ระยะเวลา</th><th>เวลาเล่น</th></tr></thead>
          <tbody>
            <tr v-for="s in data?.recent_sessions ?? []" :key="s.session_id">
              <td><div class="player-cell"><div class="tiny-avatar" :class="avatarColor(s.user_id)">{{ initials(s.player) }}</div><div><strong>{{ s.player }}</strong><small>#{{ s.session_id }}</small></div></div></td>
              <td><span class="mode-pill" :class="MODE_STYLE[s.mode]?.pill">{{ s.mode }}</span></td>
              <td><strong class="score">{{ number(s.score) }}</strong></td>
              <td>{{ duration(s.duration) }} นาที</td>
              <td>{{ when(s.played_at) }}</td>
            </tr>
            <tr v-if="!loading && !(data?.recent_sessions ?? []).length"><td colspan="5" class="empty-state">ยังไม่มีข้อมูลการเล่น</td></tr>
          </tbody>
        </table>
      </div>
    </article>

    <article class="panel mood-panel">
      <div class="panel-heading">
        <div><h2>{{ data?.recent_moods ? 'อารมณ์ล่าสุด' : 'สัดส่วนอารมณ์' }}</h2><p>{{ data?.recent_moods ? 'บันทึกอารมณ์ของผู้เล่น' : 'ภาพรวมแบบไม่ระบุตัวตน' }}</p></div>
        <button v-if="data?.recent_moods" class="text-button" @click="emit('navigate', 'moods')">ดูทั้งหมด <span>↗</span></button>
      </div>
      <div v-if="data?.recent_moods" class="mood-feed">
        <div v-for="(m, i) in data.recent_moods" :key="i" class="mood-feed-row">
          <div class="mood-emoji" :class="MOODS[m.mood]?.cls">{{ MOODS[m.mood]?.emoji }}</div>
          <div class="mood-details"><strong>{{ m.player }}</strong><span>{{ m.note || '—' }}</span></div>
          <div class="mood-time"><span class="mood-tag" :class="MOODS[m.mood]?.cls">{{ m.mood }}</span><small>{{ when(m.recorded_at) }}</small></div>
        </div>
        <p v-if="!data.recent_moods.length" class="empty-inline">ยังไม่มีบันทึกอารมณ์</p>
      </div>
      <div v-else class="mood-bars">
        <div v-for="m in data?.moods ?? []" :key="m.mood" class="mood-bar-row">
          <span class="mood-tag" :class="MOODS[m.mood]?.cls">{{ MOODS[m.mood]?.emoji }} {{ m.mood }}</span>
          <div class="battery-track"><span :style="{ width: (m.count / moodTotal) * 100 + '%' }"></span></div>
          <strong>{{ Math.round((m.count / moodTotal) * 100) }}%</strong>
        </div>
        <p v-if="!(data?.moods ?? []).length" class="empty-inline">ยังไม่มีบันทึกอารมณ์</p>
      </div>
    </article>
  </section>
</template>
