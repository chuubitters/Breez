<script setup>
// หน้า ผ่อนคลาย: Soundscape (เกม) + Quick Session (บนตัวเครื่อง)  ข้อมูลจาก dashboard_relax_stats
import { computed, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { MODE_STYLE, dayOfMonth, duration, number, pct, weekdayShort, when } from '../lib/format'

const emit = defineEmits(['navigate'])

const period = ref(30)
const data = ref(null)
const loading = ref(true)
const error = ref('')
const tab = ref('all')   // all | Soundscape | Quick Session

async function load() {
  loading.value = true
  error.value = ''
  try { data.value = await rpc('dashboard_relax_stats', { p_days: period.value }) }
  catch (e) {
    error.value = /dashboard_relax_stats|schema cache|does not exist/i.test(e.message)
      ? 'ฐานข้อมูลยังไม่มีสถิติโหมดผ่อนคลาย (ต้องรัน migration relax_modes ก่อน)'
      : e.message
  }
  finally { loading.value = false }
}
watch(period, load, { immediate: true })

const periodLabel = computed(() => period.value ? `${period.value} วันล่าสุด` : 'ทั้งหมด')
const sound = computed(() => data.value?.soundscape ?? {})
const quick = computed(() => data.value?.quick ?? {})
const SOUND = MODE_STYLE['Soundscape'].color
const QUICK = MODE_STYLE['Quick Session'].color

// กราฟรายวัน
const daily = computed(() => data.value?.daily ?? [])
const showSound = computed(() => tab.value !== 'Quick Session')
const showQuick = computed(() => tab.value !== 'Soundscape')
const dayTotal = d => (showSound.value ? d.soundscape || 0 : 0) + (showQuick.value ? d.quick || 0 : 0)
const dayMax = computed(() => Math.max(1, Math.ceil(Math.max(1, ...daily.value.map(dayTotal)) / 4)) * 4)
const yLabels = computed(() => [4, 3, 2, 1, 0].map(i => (dayMax.value / 4) * i))
const xLabel = d => daily.value.length > 10 ? dayOfMonth(d.day) : weekdayShort(d.day)
const showX = i => daily.value.length <= 16 || i % Math.ceil(daily.value.length / 15) === 0

const scenes = computed(() => sound.value.scenes ?? [])
const sceneMax = computed(() => Math.max(1, ...scenes.value.map(s => s.sessions)))
const completedPct = computed(() => pct(quick.value.completed, quick.value.sessions))

const recent = computed(() => (data.value?.recent ?? []).filter(r => tab.value === 'all' || r.mode === tab.value))
function recentDetail(r) {
  if (r.mode === 'Soundscape') {
    const parts = []
    if (r.scene) parts.push(r.scene)
    if (r.breath_cycles > 0) parts.push(`หายใจครบ ${r.breath_cycles} รอบ`)
    if (r.exhale_ms > 0) parts.push(`เป่า ${(r.exhale_ms / 1000).toFixed(1)} วิ`)
    return parts.join(' · ') || 'หายใจ 5-5 พร้อมเสียงธรรมชาติ'
  }
  const parts = []
  if (r.rounds != null) parts.push(`${r.rounds}/4 รอบ`)
  if (r.blow_pct != null) parts.push(`เป่าได้ ${r.blow_pct}%`)
  return parts.join(' · ') || 'หายใจ 4-7-8 บนตัวเครื่อง'
}
</script>

<template>
  <section class="welcome-row">
    <div>
      <div class="eyebrow"><span class="eyebrow-dot"></span> RELAX MODES</div>
      <h1>โหมด<span class="highlight">ผ่อนคลาย</span></h1>
      <p class="page-description">Soundscape ในเกม และ Quick Session บนตัวเครื่อง · ไม่รวมบัญชีผู้ดูแล</p>
    </div>
    <div class="period-select">
      <span>ช่วงเวลา</span>
      <select v-model="period"><option :value="7">7 วันล่าสุด</option><option :value="30">30 วันล่าสุด</option><option :value="null">ทั้งหมด</option></select>
    </div>
  </section>

  <p v-if="error" class="banner-error">{{ error }} <button class="text-button" @click="load">ลองใหม่</button></p>

  <div class="relax-tabs">
    <button :class="{ active: tab === 'all' }" @click="tab = 'all'">ทั้งหมด</button>
    <button :class="{ active: tab === 'Soundscape' }" @click="tab = 'Soundscape'" :style="{ '--mode': SOUND }">♪ Soundscape</button>
    <button :class="{ active: tab === 'Quick Session' }" @click="tab = 'Quick Session'" :style="{ '--mode': QUICK }">◷ Quick Session</button>
  </div>

  <section class="relax-grid" :class="{ loading }">
    <article v-if="showSound" class="panel detail-panel relax-card" :style="{ '--mode': SOUND }">
      <div class="panel-heading"><div><h2>♪ Soundscape</h2><p>หายใจเข้า 5 ออก 5 พร้อมเสียงธรรมชาติ · Rainy Forest / Windy Mountain</p></div></div>
      <div class="relax-hero">
        <div><b>{{ number(sound.sessions) }}</b><span>ครั้งที่ฝึก</span></div>
        <div><b>{{ number(sound.players) }}</b><span>ผู้เล่น</span></div>
        <div><b>{{ number(sound.minutes, 1) }}</b><span>นาทีรวม</span></div>
      </div>
      <div class="metric-grid">
        <div><b>{{ number(sound.avg_minutes, 1) }} นาที</b><span>เวลาเฉลี่ยต่อครั้ง</span></div>
        <div><b>{{ number(sound.avg_cycles, 1) }}</b><span>รอบหายใจเฉลี่ยต่อครั้ง</span></div>
        <div><b>{{ number(sound.total_cycles) }}</b><span>รอบหายใจรวม</span></div>
        <div><b>{{ number(sound.avg_exhale_s, 1) }} วิ</b><span>เวลาเป่าเฉลี่ยต่อครั้ง</span></div>
      </div>
      <div class="mini-bars">
        <small>แยกตามฉาก</small>
        <div v-for="s in scenes" :key="s.scene" class="mini-row scene-row">
          <span>{{ s.scene }}</span>
          <div class="mini-track"><i :style="{ width: (s.sessions / sceneMax) * 100 + '%' }"></i></div>
          <b>{{ number(s.sessions) }}</b>
          <em>{{ number(s.minutes, 1) }} นาที · {{ number(s.avg_cycles, 1) }} รอบ/ครั้ง</em>
        </div>
        <p v-if="!scenes.length" class="empty-inline">ยังไม่มีข้อมูลในช่วงนี้</p>
      </div>
    </article>

    <article v-if="showQuick" class="panel detail-panel relax-card" :style="{ '--mode': QUICK }">
      <div class="panel-heading"><div><h2>◷ Quick Session</h2><p>หายใจ 4-7-8 × 4 รอบ บนตัวเครื่อง Breath Gadget</p></div></div>
      <div class="relax-hero">
        <div><b>{{ number(quick.sessions) }}</b><span>ครั้งที่ฝึก</span></div>
        <div><b>{{ number(quick.players) }}</b><span>ผู้เล่น</span></div>
        <div><b>{{ number(quick.minutes, 1) }}</b><span>นาทีรวม</span></div>
      </div>
      <div class="metric-grid">
        <div><b>{{ number(quick.avg_minutes, 1) }} นาที</b><span>เวลาเฉลี่ยต่อครั้ง</span></div>
        <div><b>{{ number(quick.avg_rounds, 1) }}<small>/4</small></b><span>รอบเฉลี่ยต่อครั้ง</span></div>
        <div><b>{{ completedPct }}%</b><span>ฝึกครบ 4 รอบ ({{ number(quick.completed) }} ครั้ง)</span></div>
        <div><b>{{ quick.avg_blow_pct != null ? number(quick.avg_blow_pct) + '%' : '—' }}</b><span>เป่าได้ตอนหายใจออก (เฉลี่ย)</span></div>
      </div>
      <div class="ring-row">
        <div class="ring" :style="{ '--p': completedPct }"><span>{{ completedPct }}%</span></div>
        <p>สัดส่วนครั้งที่ฝึกครบ 4 รอบ<br><small>บันทึกเมื่อตัวเครื่องเสียบกับเกมตอนฝึกจบ</small></p>
      </div>
    </article>
  </section>

  <section class="panel full-panel">
    <div class="panel-heading">
      <div><h2>จำนวนครั้งต่อวัน</h2><p>{{ period ? periodLabel : '30 วันล่าสุด' }}</p></div>
      <button class="text-button" @click="emit('navigate', 'sessions')">ดูประวัติทั้งหมด <span>↗</span></button>
    </div>
    <div class="chart-legend">
      <span v-if="showSound"><i class="legend-dot" :style="{ background: SOUND }"></i>Soundscape</span>
      <span v-if="showQuick"><i class="legend-dot" :style="{ background: QUICK }"></i>Quick Session</span>
    </div>
    <div class="activity-chart stacked-chart">
      <div class="y-labels"><span v-for="y in yLabels" :key="y">{{ number(y) }}</span></div>
      <div class="chart-body">
        <div class="grid-lines"><i></i><i></i><i></i><i></i><i></i></div>
        <div class="bar-groups">
          <div v-for="(d, i) in daily" :key="d.day" class="bar-group" :title="`${d.day} · Soundscape ${d.soundscape} · Quick Session ${d.quick} · ${d.minutes} นาที`">
            <div class="stack">
              <span v-if="showSound" :style="{ height: ((d.soundscape || 0) / dayMax) * 100 + '%', background: SOUND }"></span>
              <span v-if="showQuick" :style="{ height: ((d.quick || 0) / dayMax) * 100 + '%', background: QUICK }"></span>
            </div>
            <span class="x-label">{{ showX(i) ? xLabel(d) : '' }}</span>
          </div>
        </div>
      </div>
    </div>
  </section>

  <section class="panel full-panel">
    <div class="panel-heading"><div><h2>ล่าสุด</h2><p>20 ครั้งล่าสุด</p></div></div>
    <div class="table-wrap">
      <table>
        <thead><tr><th>ผู้เล่น</th><th>โหมด</th><th>รายละเอียด</th><th>ระยะเวลา</th><th>เวลา</th></tr></thead>
        <tbody>
          <tr v-for="r in recent" :key="r.session_id">
            <td><strong>{{ r.player }}</strong></td>
            <td><span class="mode-pill" :class="MODE_STYLE[r.mode]?.pill">{{ MODE_STYLE[r.mode]?.icon }} {{ r.mode }}</span></td>
            <td>{{ recentDetail(r) }}</td>
            <td>{{ duration(r.duration) }} นาที</td>
            <td>{{ when(r.played_at) }}</td>
          </tr>
          <tr v-if="!recent.length"><td colspan="5" class="empty-inline">ยังไม่มีการฝึกในช่วงนี้</td></tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.relax-tabs { display: flex; gap: 8px; margin: 0 0 16px; flex-wrap: wrap }
.relax-tabs button { border: 1px solid var(--line); background: #fff; border-radius: 999px; padding: 8px 16px; font: inherit; font-size: 12px; font-weight: 700; color: var(--muted); cursor: pointer; transition: all .18s ease }
.relax-tabs button:hover { transform: translateY(-1px); border-color: var(--mode, var(--line)) }
.relax-tabs button.active { background: color-mix(in srgb, var(--mode, #d4e59d) 45%, #fff); color: var(--ink-strong); border-color: var(--mode, #d4e59d) }
.relax-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 16px; margin-bottom: 18px }
.relax-grid.loading, .loading { opacity: .55; transition: opacity .2s }
.relax-hero { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; margin-top: 14px }
.relax-hero > div { background: color-mix(in srgb, var(--mode) 35%, #fff); border-radius: 16px; padding: 14px; display: grid; gap: 2px }
.relax-hero b { font-size: 26px; font-weight: 800; color: var(--ink-strong) }
.relax-hero span { font-size: 11px; color: var(--muted); font-weight: 600 }
.scene-row { grid-template-columns: 110px 1fr 34px }
.scene-row em { grid-column: 1 / -1; font-style: normal; font-size: 10px; color: var(--muted); margin-top: -6px }
.ring-row { display: flex; align-items: center; gap: 16px; margin-top: 16px }
.ring-row p { margin: 0; font-size: 12px; font-weight: 700; color: var(--ink-strong) }
.ring-row small { color: var(--muted); font-weight: 600 }
.ring { --p: 0; width: 76px; height: 76px; border-radius: 50%; display: grid; place-items: center; flex: none;
  background: conic-gradient(var(--mode) calc(var(--p) * 1%), color-mix(in srgb, var(--mode) 18%, #fff) 0) }
.ring span { width: 56px; height: 56px; border-radius: 50%; background: #fff; display: grid; place-items: center; font-weight: 800; font-size: 14px }
</style>
