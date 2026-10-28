<script setup>
// หน้า ผู้เล่น: ภาพรวมการเติบโตและการกลับมาฝึก (ไม่แสดงรายชื่อรายบุคคล)
import { computed, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { dayOfMonth, number, pct, trend, weekdayShort } from '../lib/format'

const props = defineProps({ me: Object })

const period = ref(30)
const data = ref(null)
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try { data.value = await rpc('dashboard_insights', { p_days: period.value }) }
  catch (e) { error.value = e.message }
  finally { loading.value = false }
}
watch(period, load, { immediate: true })

const periodLabel = computed(() => period.value ? `${period.value} วันล่าสุด` : 'ทั้งหมด')
const newTrend = computed(() => trend(data.value?.players_new, data.value?.players_new_prev))
const returningPct = computed(() => pct(data.value?.returning_players, data.value?.practitioners))

const daily = computed(() => data.value?.daily ?? [])
const max = computed(() => {
  const m = Math.max(1, ...daily.value.flatMap(d => [d.people, d.new_players]))
  return Math.max(1, Math.ceil(m / 4)) * 4
})
const yLabels = computed(() => [4, 3, 2, 1, 0].map(i => (max.value / 4) * i))
const xLabel = d => daily.value.length > 10 ? dayOfMonth(d.day) : weekdayShort(d.day)

// เส้นทางผู้เล่น: สมัคร -> เคยฝึก -> มาฝึกในช่วงนี้ -> กลับมาซ้ำ
const funnel = computed(() => {
  const d = data.value
  if (!d) return []
  const total = d.players_total || 0
  const played = total - (d.never_played || 0)
  return [
    { label: 'สมัครสมาชิก', value: total, color: 'var(--lavender)' },
    { label: 'เคยฝึกอย่างน้อย 1 ครั้ง', value: played, color: 'var(--pink)' },
    { label: `มาฝึกใน ${periodLabel.value}`, value: d.practitioners, color: 'var(--sage)' },
    { label: 'กลับมาฝึกซ้ำ (≥ 2 วัน)', value: d.returning_players, color: '#e2dc8f' },
  ].map(s => ({ ...s, percent: pct(s.value, total) }))
})
</script>

<template>
  <section class="welcome-row section-welcome">
    <div><div class="eyebrow"><span class="eyebrow-dot"></span> PLAYERS</div><h1>ผู้เล่นของ <span class="highlight">breez</span></h1><p class="page-description">การเติบโตของผู้เล่นและการกลับมาฝึกหายใจ</p></div>
    <div class="period-select">
      <span>ช่วงเวลา</span>
      <select v-model="period"><option :value="7">7 วันล่าสุด</option><option :value="30">30 วันล่าสุด</option><option :value="null">ทั้งหมด</option></select>
    </div>
  </section>

  <p v-if="error" class="banner-error">{{ error }}</p>

  <section class="stats-grid compact-stats" :class="{ loading }">
    <article class="stat-card stat-lilac"><div class="stat-label">ผู้เล่นทั้งหมด</div><div class="stat-value">{{ number(data?.players_total) }}<span class="stat-unit">คน</span></div><div class="stat-foot">บัญชีที่ลงทะเบียนผ่านเกม</div></article>
    <article class="stat-card stat-green"><div class="stat-label">มาฝึกหายใจ</div><div class="stat-value">{{ number(data?.practitioners) }}<span class="stat-unit">คน</span></div><div class="stat-foot"><span class="online-dot"></span> {{ periodLabel }}</div></article>
    <article class="stat-card stat-peach"><div class="stat-label">ผู้เล่นใหม่</div><div class="stat-value">+{{ number(data?.players_new) }}</div><div class="stat-foot"><span v-if="newTrend" class="trend" :class="{ down: !newTrend.up }">{{ newTrend.text }}</span><span>{{ periodLabel }}</span></div></article>
    <article class="stat-card stat-yellow"><div class="stat-label">กลับมาฝึกซ้ำ</div><div class="stat-value">{{ returningPct }}<span class="stat-unit">%</span></div><div class="stat-foot">{{ number(data?.returning_players) }} คน ฝึกมากกว่า 1 วัน</div></article>
  </section>

  <section class="dashboard-grid">
    <article class="panel">
      <div class="panel-heading"><div><h2>ผู้เล่นรายวัน</h2><p>จำนวนคนที่มาฝึก และผู้เล่นใหม่ในแต่ละวัน</p></div></div>
      <div class="chart-legend"><span><i class="legend-dot legend-sage"></i>คนที่มาฝึก</span><span><i class="legend-dot legend-coral"></i>ผู้เล่นใหม่</span></div>
      <div class="activity-chart">
        <div class="y-labels"><span v-for="y in yLabels" :key="y">{{ number(y) }}</span></div>
        <div class="chart-body">
          <div class="grid-lines"><i></i><i></i><i></i><i></i><i></i></div>
          <div class="bar-groups">
            <div v-for="d in daily" :key="d.day" class="bar-group" :title="`${d.day}: มาฝึก ${d.people} คน · ใหม่ ${d.new_players} คน`">
              <div class="bars">
                <span class="bar sage-bar wide" :style="{ height: (d.people / max) * 100 + '%' }"></span>
                <span class="bar coral-bar wide" :style="{ height: (d.new_players / max) * 100 + '%' }"></span>
              </div>
              <span class="x-label">{{ xLabel(d) }}</span>
            </div>
          </div>
        </div>
      </div>
    </article>

    <article class="panel">
      <div class="panel-heading"><div><h2>เส้นทางผู้เล่น</h2><p>เทียบกับผู้เล่นทั้งหมด</p></div></div>
      <div class="funnel">
        <div v-for="s in funnel" :key="s.label" class="funnel-row">
          <div class="funnel-top"><span>{{ s.label }}</span><b>{{ number(s.value) }} คน · {{ s.percent }}%</b></div>
          <div class="funnel-track"><span :style="{ width: Math.max(s.percent, s.value ? 3 : 0) + '%', background: s.color }"></span></div>
        </div>
      </div>
      <p class="page-description funnel-note">รายชื่อและประวัติรายบุคคลดูได้ในหน้า "ประวัติการเล่น"</p>
    </article>
  </section>
</template>
