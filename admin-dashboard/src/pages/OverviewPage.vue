<script setup>
import { computed, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { MODE_STYLE, MOODS, TRAINING_INFO, dayOfMonth, longDate, number, pct, trend, weekdayShort } from '../lib/format'
import MoodWeather from '../components/MoodWeather.vue'

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
    data.value = await rpc('dashboard_insights', { p_days: period.value })
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}
watch(period, load, { immediate: true })

const periodLabel = computed(() => period.value ? `${period.value} วันล่าสุด` : 'ทั้งหมด')
const t = computed(() => ({
  people: trend(data.value?.practitioners, data.value?.practitioners_prev),
  sessions: trend(data.value?.sessions, data.value?.sessions_prev),
  minutes: trend(data.value?.minutes, data.value?.minutes_prev),
}))

// ---------- กราฟกิจกรรมรายวัน: แท่ง = จำนวนครั้งแต่ละโหมด, เส้น = จำนวนคน ----------
const chartSeries = [
  { key: 'calm', label: 'Calm Flow', color: MODE_STYLE['Calm Flow'].color },
  { key: 'power', label: 'Power Release', color: MODE_STYLE['Power Release'].color },
  { key: 'focus', label: 'Breathe & Focus', color: MODE_STYLE['Breathe & Focus'].color },
  { key: 'relax', label: 'Soundscape / Quick Session', color: MODE_STYLE['Soundscape'].color },
]
const daily = computed(() => data.value?.daily ?? [])
const niceMax = v => { const step = Math.max(1, Math.ceil(v / 4)); return step * 4 }
const barMax = computed(() => niceMax(Math.max(1, ...daily.value.flatMap(d => chartSeries.map(s => d[s.key])))))
const peopleMax = computed(() => niceMax(Math.max(1, ...daily.value.map(d => d.people))))
const yLabels = computed(() => [4, 3, 2, 1, 0].map(i => (barMax.value / 4) * i))
const yPeople = computed(() => [4, 3, 2, 1, 0].map(i => (peopleMax.value / 4) * i))
const xLabel = d => daily.value.length > 10 ? dayOfMonth(d.day) : weekdayShort(d.day)
const peoplePoints = computed(() => {
  const n = daily.value.length
  return daily.value.map((d, i) => ({ x: ((i + 0.5) / n) * 100, y: 100 - (d.people / peopleMax.value) * 100, v: d.people }))
})
const peoplePath = computed(() => peoplePoints.value.map(p => `${p.x},${p.y}`).join(' '))

// ---------- สัดส่วนรูปแบบการฝึก ----------
const training = computed(() => {
  const list = data.value?.training_types ?? []
  const total = list.reduce((a, m) => a + m.sessions, 0)
  return {
    total,
    list: list.map(m => ({
      ...m,
      percent: pct(m.sessions, total),
      color: MODE_STYLE[m.mode]?.color ?? '#ccc',
      info: TRAINING_INFO[m.mode],
    })),
  }
})
const donut = computed(() => {
  if (!training.value.total) return 'conic-gradient(#efece5 0 100%)'
  let acc = 0
  return 'conic-gradient(' + training.value.list.filter(m => m.sessions).map(m => {
    const from = acc
    acc += (m.sessions / training.value.total) * 100
    return `${m.color} ${from}% ${acc}%`
  }).join(',') + ')'
})

// ---------- อารมณ์ ----------
const moods = computed(() => {
  const total = data.value?.mood_people ?? 0
  return (data.value?.moods ?? []).map(m => ({ ...m, percent: pct(m.people, total) }))
})
</script>

<template>
  <section class="welcome-row">
    <div>
      <div class="eyebrow"><span class="eyebrow-dot"></span> ADMIN OVERVIEW <span class="date-label">· {{ longDate() }}</span></div>
      <h1>สวัสดี {{ me.name }} ภาพรวมที่ดี เริ่มจาก<span class="highlight">ลมหายใจ</span></h1>
      <p class="page-description">ภาพรวมการฝึกหายใจและอารมณ์ของชาว breez</p>
    </div>
    <div class="period-select">
      <span>ช่วงเวลา</span>
      <select v-model="period"><option :value="7">7 วันล่าสุด</option><option :value="30">30 วันล่าสุด</option><option :value="null">ทั้งหมด</option></select>
    </div>
  </section>

  <p v-if="error" class="banner-error">{{ error }} <button class="text-button" @click="load">ลองใหม่</button></p>

  <section class="stats-grid" :class="{ loading }">
    <article class="stat-card stat-peach">
      <div class="stat-top"><span class="stat-label">คนที่มาฝึกหายใจ</span><span class="stat-icon">♙</span></div>
      <div class="stat-value">{{ number(data?.practitioners) }}<span class="stat-unit">คน</span></div>
      <div class="stat-foot"><span v-if="t.people" class="trend" :class="{ down: !t.people.up }">{{ t.people.text }}</span><span>{{ period ? 'เทียบกับช่วงก่อน' : 'ทั้งหมด' }}</span></div>
    </article>
    <article class="stat-card stat-green">
      <div class="stat-top"><span class="stat-label">จำนวนครั้งที่ฝึก</span><span class="stat-icon">▷</span></div>
      <div class="stat-value">{{ number(data?.sessions) }}<span class="stat-unit">ครั้ง</span></div>
      <div class="stat-foot"><span v-if="t.sessions" class="trend" :class="{ down: !t.sessions.up }">{{ t.sessions.text }}</span><span>เฉลี่ย {{ data?.practitioners ? number(data.sessions / data.practitioners, 1) : 0 }} ครั้ง/คน</span></div>
    </article>
    <article class="stat-card stat-lilac">
      <div class="stat-top"><span class="stat-label">เวลาฝึกหายใจรวม</span><span class="stat-icon">◷</span></div>
      <div class="stat-value">{{ data?.minutes >= 120 ? number(data.minutes / 60, 1) : number(data?.minutes, 1) }}<span class="stat-unit">{{ data?.minutes >= 120 ? 'ชม.' : 'นาที' }}</span></div>
      <div class="stat-foot"><span v-if="t.minutes" class="trend" :class="{ down: !t.minutes.up }">{{ t.minutes.text }}</span><span>เฉลี่ย {{ number(data?.avg_minutes_per_person, 1) }} นาที/คน</span></div>
    </article>
    <article class="stat-card stat-yellow">
      <div class="stat-top"><span class="stat-label">คนที่บันทึกอารมณ์</span><span class="stat-icon">♡</span></div>
      <div class="stat-value">{{ number(data?.mood_people) }}<span class="stat-unit">คน</span></div>
      <div class="stat-foot"><span>{{ number(data?.mood_logs) }} บันทึก · {{ periodLabel }}</span></div>
    </article>
  </section>

  <!-- ===== การฝึกหายใจ ===== -->
  <section class="dashboard-grid">
    <article class="panel activity-panel">
      <div class="panel-heading">
        <div><h2>กิจกรรมการฝึกหายใจ</h2><p>แท่ง = จำนวนครั้งแต่ละรูปแบบ · เส้น = จำนวนคนที่มาฝึกในแต่ละวัน</p></div>
        <button class="text-button" @click="emit('navigate', 'modes')">ดูแยกตามโหมด <span>↗</span></button>
      </div>
      <div class="chart-legend">
        <span v-for="s in chartSeries" :key="s.key"><i class="legend-dot" :style="{ background: s.color }"></i>{{ s.label }}</span>
        <span><i class="legend-line"></i>จำนวนคน</span>
      </div>
      <div class="activity-chart">
        <div class="y-labels"><span v-for="y in yLabels" :key="'b' + y">{{ number(y) }}</span></div>
        <div class="chart-body">
          <div class="grid-lines"><i></i><i></i><i></i><i></i><i></i></div>
          <div class="bar-groups">
            <div v-for="d in daily" :key="d.day" class="bar-group" :title="`${d.day}: ${d.people} คน · ` + chartSeries.map(s => s.label + ' ' + d[s.key]).join(', ')">
              <div class="bars"><span v-for="s in chartSeries" :key="s.key" class="bar" :style="{ height: (d[s.key] / barMax) * 100 + '%', background: s.color }"></span></div>
              <span class="x-label">{{ xLabel(d) }}</span>
            </div>
          </div>
          <div class="people-layer">
            <svg viewBox="0 0 100 100" preserveAspectRatio="none"><polyline :points="peoplePath" /></svg>
            <span v-for="(p, i) in peoplePoints" :key="i" class="people-dot" :style="{ left: p.x + '%', top: p.y + '%' }" :title="`${p.v} คน`"></span>
          </div>
        </div>
        <div class="y-labels right"><span v-for="y in yPeople" :key="'p' + y">{{ number(y) }}</span></div>
      </div>
    </article>

    <article class="panel modes-panel">
      <div class="panel-heading"><div><h2>รูปแบบการฝึก</h2><p>สัดส่วนจากจำนวนครั้ง · {{ periodLabel }}</p></div></div>
      <div class="donut-wrap"><div class="donut" :style="{ background: donut }"><div class="donut-center"><strong>{{ number(training.total) }}</strong><span>ครั้ง</span></div></div></div>
      <div class="type-list">
        <div v-for="m in training.list" :key="m.mode" class="type-row">
          <span class="mode-bullet" :style="{ background: m.color }"></span>
          <div class="type-name"><strong>{{ m.mode }}</strong><small>{{ m.info?.label }}</small></div>
          <div class="type-num"><strong>{{ m.percent }}%</strong><small>{{ number(m.people) }} คน</small></div>
        </div>
      </div>
    </article>
  </section>

  <!-- ===== อารมณ์ ===== -->
  <section class="lower-grid mood-grid">
    <MoodWeather :data="data" :period-label="periodLabel" />

    <article class="panel mood-panel">
      <div class="panel-heading"><div><h2>บันทึกอารมณ์</h2><p>จำนวนคนตามอารมณ์ล่าสุดของแต่ละคน · {{ number(data?.mood_people) }} คน</p></div></div>
      <div class="mood-people">
        <div v-for="m in moods" :key="m.mood" class="mood-people-row">
          <span class="mood-emoji" :class="MOODS[m.mood]?.cls">{{ MOODS[m.mood]?.emoji }}</span>
          <div class="mood-people-main">
            <div class="mood-people-top"><strong>{{ MOODS[m.mood]?.th }}</strong><span>{{ m.mood }}</span><b>{{ number(m.people) }} คน</b></div>
            <div class="mood-track"><span :class="MOODS[m.mood]?.cls" :style="{ width: m.percent + '%' }"></span></div>
          </div>
          <div class="mood-pct">{{ m.percent }}%</div>
        </div>
      </div>
      <p v-if="!data?.mood_people" class="empty-inline">ยังไม่มีการบันทึกอารมณ์ในช่วงนี้</p>
    </article>
  </section>
</template>
