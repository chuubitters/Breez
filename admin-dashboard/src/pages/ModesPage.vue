<script setup>
// หน้า โหมดการฝึก: สถิติแยกตามโหมด + ข้อมูลเฉพาะของแต่ละโหมด (dashboard_mode_stats)
import { computed, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { MODE_STYLE, TRAINING_INFO, dayOfMonth, duration, number, pct, seconds, weekdayShort, when } from '../lib/format'

const emit = defineEmits(['navigate'])

const period = ref(30)
const data = ref(null)
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try { data.value = await rpc('dashboard_mode_stats', { p_days: period.value }) }
  catch (e) {
    error.value = /dashboard_mode_stats|schema cache|does not exist/i.test(e.message)
      ? 'ฐานข้อมูลยังไม่มีสถิติแยกโหมด (ต้องรัน migration 20261029000003_all_modes.sql ก่อน)'
      : e.message
  }
  finally { loading.value = false }
}
watch(period, load, { immediate: true })

const periodLabel = computed(() => period.value ? `${period.value} วันล่าสุด` : 'ทั้งหมด')
const modes = computed(() => (data.value?.modes ?? []).map(m => ({ ...m, style: MODE_STYLE[m.mode] ?? {}, info: TRAINING_INFO[m.mode] })))
const totalSessions = computed(() => modes.value.reduce((a, m) => a + m.sessions, 0))
const top = computed(() => [...modes.value].sort((a, b) => b.sessions - a.sessions)[0])

// ---------- กราฟรายวัน แยก 5 โหมด ----------
const series = ['Calm Flow', 'Power Release', 'Breathe & Focus', 'Soundscape', 'Quick Session']
  .map(m => ({ mode: m, key: MODE_STYLE[m].key, color: MODE_STYLE[m].color }))
const daily = computed(() => data.value?.daily ?? [])
const dayMax = computed(() => {
  const m = Math.max(1, ...daily.value.map(d => series.reduce((a, s) => a + (d[s.key] || 0), 0)))
  return Math.max(1, Math.ceil(m / 4)) * 4
})
const yLabels = computed(() => [4, 3, 2, 1, 0].map(i => (dayMax.value / 4) * i))
const xLabel = d => daily.value.length > 10 ? dayOfMonth(d.day) : weekdayShort(d.day)
const showX = i => daily.value.length <= 16 || i % Math.ceil(daily.value.length / 15) === 0

const calm = computed(() => data.value?.calm ?? {})
const power = computed(() => data.value?.power ?? {})
const focus = computed(() => data.value?.focus ?? {})
const sound = computed(() => data.value?.soundscape ?? {})
const quick = computed(() => data.value?.quick ?? {})
const calmLengths = computed(() => {
  const c = calm.value
  const total = (c.minutes_1 || 0) + (c.minutes_2 || 0) + (c.minutes_3 || 0)
  return [1, 2, 3].map(n => ({ label: `${n} นาที`, value: c['minutes_' + n] || 0, percent: pct(c['minutes_' + n], total) }))
})
const sceneMax = computed(() => Math.max(1, ...(sound.value.scenes ?? []).map(s => s.sessions)))
</script>

<template>
  <section class="welcome-row">
    <div>
      <div class="eyebrow"><span class="eyebrow-dot"></span> TRAINING MODES</div>
      <h1>โหมด<span class="highlight">การฝึก</span></h1>
      <p class="page-description">ข้อมูลที่เก็บจากแต่ละโหมด · รวม/ไม่รวมบัญชีผู้ดูแล เลือกได้ที่ปุ่มด้านบน</p>
    </div>
    <div class="period-select">
      <span>ช่วงเวลา</span>
      <select v-model="period"><option :value="7">7 วันล่าสุด</option><option :value="30">30 วันล่าสุด</option><option :value="null">ทั้งหมด</option></select>
    </div>
  </section>

  <p v-if="error" class="banner-error">{{ error }} <button class="text-button" @click="load">ลองใหม่</button></p>

  <!-- การ์ดสรุปแต่ละโหมด -->
  <section class="mode-cards" :class="{ loading }">
    <article v-for="m in modes" :key="m.mode" class="mode-card" :style="{ '--mode': m.style.color }">
      <div class="mode-card-top">
        <span class="mode-icon">{{ m.style.icon }}</span>
        <div><strong>{{ m.mode }}</strong><small>{{ m.info?.label }}</small></div>
      </div>
      <div class="mode-big">{{ number(m.sessions) }}<span>ครั้ง</span></div>
      <div class="mode-share"><span :style="{ width: pct(m.sessions, totalSessions) + '%' }"></span></div>
      <dl class="mode-facts">
        <div><dt>ผู้เล่น</dt><dd>{{ number(m.players) }} คน</dd></div>
        <div><dt>เวลาเฉลี่ย</dt><dd>{{ duration(m.avg_duration) }} นาที</dd></div>
        <div><dt>เวลารวม</dt><dd>{{ number(m.minutes, 1) }} นาที</dd></div>
        <div><dt>เล่นล่าสุด</dt><dd>{{ m.last_played ? when(m.last_played) : '—' }}</dd></div>
      </dl>
    </article>
  </section>

  <section class="panel full-panel">
    <div class="panel-heading">
      <div><h2>จำนวนครั้งต่อวัน แยกตามโหมด</h2><p>{{ period ? periodLabel : '30 วันล่าสุด' }} · โหมดที่นิยมที่สุด: <b>{{ top?.sessions ? top.mode : '—' }}</b></p></div>
      <button class="text-button" @click="emit('navigate', 'sessions')">ดูประวัติทั้งหมด <span>↗</span></button>
    </div>
    <div class="chart-legend"><span v-for="s in series" :key="s.key"><i class="legend-dot" :style="{ background: s.color }"></i>{{ s.mode }}</span></div>
    <div class="activity-chart stacked-chart">
      <div class="y-labels"><span v-for="y in yLabels" :key="y">{{ number(y) }}</span></div>
      <div class="chart-body">
        <div class="grid-lines"><i></i><i></i><i></i><i></i><i></i></div>
        <div class="bar-groups">
          <div v-for="(d, i) in daily" :key="d.day" class="bar-group" :title="d.day + ' · ' + series.map(s => s.mode + ' ' + (d[s.key] || 0)).join(', ')">
            <div class="stack">
              <span v-for="s in series" :key="s.key" :style="{ height: ((d[s.key] || 0) / dayMax) * 100 + '%', background: s.color }"></span>
            </div>
            <span class="x-label">{{ showX(i) ? xLabel(d) : '' }}</span>
          </div>
        </div>
      </div>
    </div>
  </section>

  <!-- ข้อมูลเฉพาะโหมด -->
  <section class="detail-grid" :class="{ loading }">
    <article class="panel detail-panel" style="--mode: #c9de8c">
      <div class="panel-heading"><div><h2>✿ Calm Flow</h2><p>ผ่อนลมหายใจออกยาวๆ ให้ดอกไม้บาน</p></div></div>
      <div class="metric-grid">
        <div><b>{{ number(calm.flowers) }}</b><span>ดอกไม้ที่ปลูกทั้งหมด</span></div>
        <div><b>{{ number(calm.avg_flowers, 1) }}</b><span>ดอกเฉลี่ยต่อครั้ง</span></div>
        <div><b>{{ number(calm.avg_largest_pct) }}%</b><span>ดอกใหญ่สุดเฉลี่ย</span></div>
        <div><b>{{ number(calm.full_bloom_sessions) }}</b><span>ครั้งที่บานเต็ม 100%</span></div>
        <div><b>{{ seconds(calm.avg_longest_exhale_ms) }} วิ</b><span>หายใจออกนานสุด (เฉลี่ย)</span></div>
        <div><b>{{ seconds(calm.best_longest_exhale_ms) }} วิ</b><span>หายใจออกนานสุด (สถิติ)</span></div>
      </div>
      <div class="mini-bars">
        <small>ระยะเวลาที่เลือก</small>
        <div v-for="l in calmLengths" :key="l.label" class="mini-row"><span>{{ l.label }}</span><div class="mini-track"><i :style="{ width: l.percent + '%' }"></i></div><b>{{ number(l.value) }}</b></div>
      </div>
    </article>

    <article class="panel detail-panel" style="--mode: #f3bccb">
      <div class="panel-heading"><div><h2>◯ Power Release</h2><p>เป่าแรงๆ ให้ลูกโป่งแตก</p></div></div>
      <div class="metric-grid">
        <div><b>{{ number(power.balloons_popped) }}</b><span>ลูกโป่งที่แตกทั้งหมด</span></div>
        <div><b>{{ number(power.avg_popped, 1) }}<small>/{{ number(power.avg_total) }}</small></b><span>ลูกโป่งเฉลี่ยต่อครั้ง</span></div>
        <div><b>{{ pct(power.completed, power.sessions) }}%</b><span>เล่นจบครบทุกลูก</span></div>
        <div><b>{{ duration(power.avg_duration_completed) }}</b><span>เวลาเฉลี่ยตอนเล่นจบ</span></div>
      </div>
    </article>

    <article class="panel detail-panel" style="--mode: #f1dc86">
      <div class="panel-heading"><div><h2>☁ Breathe & Focus</h2><p>ควบคุมลมหายใจหลบก้อนเมฆ</p></div></div>
      <div class="metric-grid">
        <div><b>{{ number(focus.obstacles) }}</b><span>หลบสิ่งกีดขวางทั้งหมด</span></div>
        <div><b>{{ number(focus.avg_obstacles, 1) }}</b><span>หลบได้เฉลี่ยต่อครั้ง</span></div>
        <div><b>{{ number(focus.best_obstacles) }}</b><span>สถิติสูงสุด</span></div>
        <div><b>{{ duration(focus.avg_survive) }}</b><span>อยู่รอดเฉลี่ย (นาที)</span></div>
      </div>
    </article>

    <article class="panel detail-panel" style="--mode: #b8d9a4">
      <div class="panel-heading"><div><h2>♪ Soundscape</h2><p>หายใจเข้า 5 ออก 5 พร้อมเสียงธรรมชาติ</p></div></div>
      <div class="metric-grid">
        <div><b>{{ number(sound.sessions) }}</b><span>ครั้งที่ฝึก</span></div>
        <div><b>{{ number(sound.avg_cycles, 1) }}</b><span>รอบหายใจเฉลี่ยต่อครั้ง</span></div>
      </div>
      <div class="mini-bars">
        <small>แยกตามฉาก</small>
        <div v-for="s in sound.scenes ?? []" :key="s.scene" class="mini-row"><span>{{ s.scene }}</span><div class="mini-track"><i :style="{ width: (s.sessions / sceneMax) * 100 + '%' }"></i></div><b>{{ number(s.sessions) }}</b></div>
        <p v-if="!(sound.scenes ?? []).length" class="empty-inline">ยังไม่มีข้อมูลในช่วงนี้</p>
      </div>
    </article>

    <article class="panel detail-panel" style="--mode: #f7cfbd">
      <div class="panel-heading"><div><h2>◷ Quick Session</h2><p>หายใจ 4-7-8 บนตัวเครื่อง Breath Gadget</p></div></div>
      <div class="metric-grid">
        <div><b>{{ number(quick.sessions) }}</b><span>ครั้งที่ฝึก</span></div>
        <div><b>{{ number(quick.players) }}</b><span>ผู้เล่น</span></div>
        <div><b>{{ number(quick.minutes, 1) }}</b><span>นาทีรวม</span></div>
      </div>
    </article>
  </section>
</template>
