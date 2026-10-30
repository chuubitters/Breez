<script setup>
// หน้า ผ่อนคลาย: Soundscape (หายใจ 5-5 พร้อมเสียงธรรมชาติ) + Quick Session (4-7-8 บนตัวเครื่อง)
// ข้อมูลจาก RPC dashboard_relax_stats (supabase/migrations/20261030000001_relax_modes.sql)
import { computed, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { MODE_STYLE, dayOfMonth, duration, number, pct, weekdayShort, when } from '../lib/format'

const emit = defineEmits(['navigate'])

const period = ref(30)
const view = ref('all')          // all | Soundscape | Quick Session
const data = ref(null)
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try { data.value = await rpc('dashboard_relax_stats', { p_days: period.value }) }
  catch (e) {
    error.value = /dashboard_relax_stats|schema cache|does not exist/i.test(e.message)
      ? 'ฐานข้อมูลยังไม่มีสถิติโหมดผ่อนคลาย (ต้องรัน migration 20261030000001_relax_modes.sql ก่อน)'
      : e.message
  }
  finally { loading.value = false }
}
watch(period, load, { immediate: true })

const sound = computed(() => data.value?.soundscape ?? {})
const quick = computed(() => data.value?.quick ?? {})
const showSound = computed(() => view.value !== 'Quick Session')
const showQuick = computed(() => view.value !== 'Soundscape')

const SOUND_COLOR = MODE_STYLE['Soundscape'].color
const QUICK_COLOR = MODE_STYLE['Quick Session'].color

// ---------- กราฟรายวัน ----------
const daily = computed(() => data.value?.daily ?? [])
const series = computed(() => [
  showSound.value && { key: 'soundscape', label: 'Soundscape', color: SOUND_COLOR },
  showQuick.value && { key: 'quick', label: 'Quick Session', color: QUICK_COLOR },
].filter(Boolean))
const dayMax = computed(() => {
  const m = Math.max(1, ...daily.value.map(d => series.value.reduce((a, s) => a + (d[s.key] || 0), 0)))
  return Math.max(1, Math.ceil(m / 4)) * 4
})
const yLabels = computed(() => [4, 3, 2, 1, 0].map(i => (dayMax.value / 4) * i))
const xLabel = d => daily.value.length > 10 ? dayOfMonth(d.day) : weekdayShort(d.day)
const showX = i => daily.value.length <= 16 || i % Math.ceil(daily.value.length / 15) === 0

const scenes = computed(() => sound.value.scenes ?? [])
const sceneMax = computed(() => Math.max(1, ...scenes.value.map(s => s.sessions)))

const recent = computed(() => (data.value?.recent ?? []).filter(r => view.value === 'all' || r.mode === view.value))

function detail(r) {
  if (r.mode === 'Soundscape') {
    const p = []
    if (r.scene) p.push(r.scene)
    if (r.breath_cycles > 0) p.push(`หายใจครบ ${r.breath_cycles} รอบ`)
    if (r.exhale_ms > 0) p.push(`เป่า ${(r.exhale_ms / 1000).toFixed(1)} วิ`)
    return p.join(' · ') || 'หายใจ 5-5'
  }
  const p = ['4-7-8']
  if (r.rounds != null) p.push(`${r.rounds} รอบ`)
  if (r.blow_pct != null) p.push(`เป่าได้ ${r.blow_pct}%`)
  return p.join(' · ')
}
</script>

<template>
  <section class="welcome-row">
    <div>
      <div class="eyebrow"><span class="eyebrow-dot"></span> RELAX MODES</div>
      <h1>Soundscape <span class="highlight">&amp; Quick Session</span></h1>
      <p class="page-description">โหมดฝึกหายใจแบบผ่อนคลาย · ไม่รวมบัญชีผู้ดูแล</p>
    </div>
    <div class="period-select">
      <span>แสดง</span>
      <select v-model="view"><option value="all">ทั้งสองโหมด</option><option value="Soundscape">Soundscape</option><option value="Quick Session">Quick Session</option></select>
      <span>ช่วงเวลา</span>
      <select v-model="period"><option :value="7">7 วันล่าสุด</option><option :value="30">30 วันล่าสุด</option><option :value="null">ทั้งหมด</option></select>
    </div>
  </section>

  <p v-if="error" class="banner-error">{{ error }} <button class="text-button" @click="load">ลองใหม่</button></p>

  <!-- การ์ดสรุป -->
  <section class="detail-grid relax-grid" :class="{ loading }">
    <article v-if="showSound" class="panel detail-panel" :style="{ '--mode': SOUND_COLOR }">
      <div class="panel-heading"><div><h2>♪ Soundscape</h2><p>หายใจเข้า 5 ออก 5 พร้อมเสียงธรรมชาติ (Rainy Forest / Windy Mountain)</p></div></div>
      <div class="metric-grid">
        <div><b>{{ number(sound.sessions) }}</b><span>ครั้งที่ฝึก</span></div>
        <div><b>{{ number(sound.players) }}</b><span>ผู้เล่น</span></div>
        <div><b>{{ number(sound.minutes, 1) }}</b><span>นาทีรวม</span></div>
        <div><b>{{ number(sound.avg_minutes, 1) }}</b><span>นาทีเฉลี่ยต่อครั้ง</span></div>
        <div><b>{{ number(sound.avg_cycles, 1) }}</b><span>รอบหายใจเฉลี่ย</span></div>
        <div><b>{{ number(sound.avg_exhale_s, 1) }} วิ</b><span>เวลาเป่าเฉลี่ย</span></div>
      </div>
      <div class="mini-bars">
        <small>แยกตามฉาก</small>
        <div v-for="s in scenes" :key="s.scene" class="mini-row scene-row">
          <span>{{ s.scene }}</span>
          <div class="mini-track"><i :style="{ width: (s.sessions / sceneMax) * 100 + '%' }"></i></div>
          <b>{{ number(s.sessions) }}</b>
          <em>{{ number(s.minutes, 1) }} นาที · {{ number(s.avg_cycles, 1) }} รอบ</em>
        </div>
        <p v-if="!scenes.length" class="empty-inline">ยังไม่มีการฝึก Soundscape ในช่วงนี้</p>
      </div>
    </article>

    <article v-if="showQuick" class="panel detail-panel" :style="{ '--mode': QUICK_COLOR }">
      <div class="panel-heading"><div><h2>◷ Quick Session</h2><p>หายใจ 4-7-8 บนตัวเครื่อง Breath Gadget (บันทึกเมื่อเสียบสายกับเกม)</p></div></div>
      <div class="metric-grid">
        <div><b>{{ number(quick.sessions) }}</b><span>ครั้งที่ฝึก</span></div>
        <div><b>{{ number(quick.players) }}</b><span>ผู้เล่น</span></div>
        <div><b>{{ number(quick.minutes, 1) }}</b><span>นาทีรวม</span></div>
        <div><b>{{ number(quick.avg_rounds, 1) }}</b><span>รอบเฉลี่ยต่อครั้ง</span></div>
        <div><b>{{ pct(quick.completed, quick.sessions) }}%</b><span>ฝึกครบ 4 รอบ</span></div>
        <div><b>{{ quick.avg_blow_pct != null ? number(quick.avg_blow_pct) + '%' : '—' }}</b><span>เป่าได้ดีเฉลี่ย</span></div>
      </div>
      <p v-if="!quick.sessions" class="empty-inline">ยังไม่มีการฝึก Quick Session ในช่วงนี้</p>
    </article>
  </section>

  <section class="panel full-panel">
    <div class="panel-heading">
      <div><h2>จำนวนครั้งต่อวัน</h2><p>{{ period ? period + ' วันล่าสุด' : '30 วันล่าสุด' }}</p></div>
    </div>
    <div class="chart-legend"><span v-for="s in series" :key="s.key"><i class="legend-dot" :style="{ background: s.color }"></i>{{ s.label }}</span></div>
    <div class="activity-chart stacked-chart">
      <div class="y-labels"><span v-for="y in yLabels" :key="y">{{ number(y) }}</span></div>
      <div class="chart-body">
        <div class="grid-lines"><i></i><i></i><i></i><i></i><i></i></div>
        <div class="bar-groups">
          <div v-for="(d, i) in daily" :key="d.day" class="bar-group" :title="d.day + ' · ' + series.map(s => s.label + ' ' + (d[s.key] || 0)).join(', ') + ' · ' + d.minutes + ' นาที'">
            <div class="stack">
              <span v-for="s in series" :key="s.key" :style="{ height: ((d[s.key] || 0) / dayMax) * 100 + '%', background: s.color }"></span>
            </div>
            <span class="x-label">{{ showX(i) ? xLabel(d) : '' }}</span>
          </div>
        </div>
      </div>
    </div>
  </section>

  <section class="panel full-panel">
    <div class="panel-heading">
      <div><h2>การฝึกล่าสุด</h2><p>20 ครั้งล่าสุด{{ view !== 'all' ? ' · ' + view : '' }}</p></div>
      <button class="text-button" @click="emit('navigate', 'sessions')">ดูประวัติทั้งหมด <span>↗</span></button>
    </div>
    <div class="table-wrap">
      <table>
        <thead><tr><th>ผู้เล่น</th><th>โหมด</th><th>ระยะเวลา</th><th>รายละเอียด</th><th>เวลา</th></tr></thead>
        <tbody>
          <tr v-for="r in recent" :key="r.session_id">
            <td><strong>{{ r.player }}</strong></td>
            <td><span class="mode-pill" :class="MODE_STYLE[r.mode]?.pill">{{ r.mode }}</span></td>
            <td>{{ duration(r.duration) }} นาที</td>
            <td>{{ detail(r) }}</td>
            <td>{{ when(r.played_at) }}</td>
          </tr>
          <tr v-if="!recent.length && !loading"><td colspan="5" class="empty-inline">ยังไม่มีข้อมูลในช่วงนี้</td></tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.relax-grid { grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)) }
.scene-row { grid-template-columns: 110px 1fr 34px; row-gap: 2px }
.scene-row em { grid-column: 1 / -1; font-style: normal; font-size: 10px; color: var(--faint); margin-top: -4px }
.period-select select + span { margin-left: 8px }
</style>
