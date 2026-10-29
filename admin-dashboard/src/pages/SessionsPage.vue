<script setup>
import { computed, ref } from 'vue'
import { rpc } from '../lib/supabase'
import { useRemoteList } from '../lib/useRemoteList'
import { MODES, MODE_STYLE, downloadCsv, duration, number, sessionDetail, shortId, when } from '../lib/format'
import Pager from '../components/Pager.vue'

const props = defineProps({ me: Object, search: String, playerFilter: Object })
const emit = defineEmits(['clear-player'])

const mode = ref('')
const days = ref(null)

const args = (limit, offset) => ({
  p_mode: mode.value || null,
  p_days: days.value,
  p_search: props.search || null,
  p_user_id: props.playerFilter?.id ?? null,
  p_limit: limit,
  p_offset: offset,
})

const list = useRemoteList(
  ({ limit, offset }) => rpc('dashboard_play_sessions', args(limit, offset)),
  { pageSize: 25, deps: [mode, days, () => props.search, () => props.playerFilter] },
)

const summary = ref(null)
const loadSummary = () => rpc('dashboard_overview', { p_days: days.value }).then(d => (summary.value = d)).catch(() => {})
loadSummary()

const periodLabel = computed(() => days.value ? `${days.value} วันล่าสุด` : 'ทั้งหมด')

async function exportCsv() {
  const rows = await rpc('dashboard_play_sessions', args(1000, 0))
  downloadCsv('breez-play-sessions.csv',
    ['session_id', 'user_id', 'player', 'mode', 'score', 'duration_seconds', 'detail', 'device', 'played_at'],
    rows.map(s => [s.session_id, s.user_id, s.player, s.mode, s.score, s.duration, sessionDetail(s), s.device_name, s.played_at]))
}
</script>

<template>
  <section class="welcome-row section-welcome">
    <div><div class="eyebrow"><span class="eyebrow-dot"></span> SESSION ACTIVITY</div><h1>ประวัติการ<span class="highlight">เล่นเกม</span></h1><p class="page-description">ผลการเล่นแต่ละครั้ง และข้อมูลเฉพาะของแต่ละโหมด</p></div>
    <button class="secondary-button" @click="exportCsv">↓ ส่งออก CSV</button>
  </section>

  <section class="stats-grid compact-stats">
    <article class="stat-card stat-lilac"><div class="stat-label">จำนวนครั้งที่เล่น</div><div class="stat-value">{{ number(summary?.sessions) }}</div><div class="stat-foot">{{ periodLabel }}</div></article>
    <article class="stat-card stat-green"><div class="stat-label">คะแนนเฉลี่ย (เกม)</div><div class="stat-value">{{ number(summary?.avg_score, 1) }}</div><div class="stat-foot">Calm Flow · Power Release · Breathe & Focus</div></article>
    <article class="stat-card stat-peach"><div class="stat-label">เวลาเฉลี่ยต่อครั้ง</div><div class="stat-value">{{ duration(summary?.avg_duration) }}</div><div class="stat-foot">นาทีต่อครั้ง</div></article>
    <article class="stat-card stat-yellow"><div class="stat-label">เวลาฝึกรวม</div><div class="stat-value">{{ number(summary?.minutes, 1) }}<span class="stat-unit">นาที</span></div><div class="stat-foot">{{ periodLabel }}</div></article>
  </section>

  <section class="panel full-panel">
    <div class="panel-heading">
      <div>
        <h2>การเล่นทั้งหมด</h2>
        <p v-if="playerFilter">เฉพาะผู้เล่น <strong>{{ playerFilter.name }}</strong> · <button class="text-button" @click="emit('clear-player')">ล้างตัวกรอง ✕</button></p>
        <p v-else>เลือกดูข้อมูลตามโหมดเกมและช่วงเวลา</p>
      </div>
      <div class="header-actions">
        <select class="filter-select" v-model="days" @change="loadSummary"><option :value="null">ทุกช่วงเวลา</option><option :value="7">7 วันล่าสุด</option><option :value="30">30 วันล่าสุด</option></select>
        <select class="filter-select" v-model="mode"><option value="">ทุกโหมด</option><option v-for="m in MODES" :key="m" :value="m">{{ m }}</option></select>
      </div>
    </div>
    <p v-if="list.error.value" class="banner-error">{{ list.error.value }}</p>
    <div class="table-wrap" :class="{ loading: list.loading.value }">
      <table class="wide-table">
        <thead><tr><th>รหัส</th><th>ผู้เล่น</th><th>โหมดเกม</th><th>คะแนน</th><th>ระยะเวลา</th><th>ผลลัพธ์เฉพาะโหมด</th><th>อุปกรณ์</th><th>เวลาเล่น</th></tr></thead>
        <tbody>
          <tr v-for="s in list.rows.value" :key="s.session_id">
            <td class="muted">#{{ s.session_id }}</td>
            <td><strong>{{ s.player }}</strong><small class="sub-id">{{ shortId(s.user_id) }}</small></td>
            <td><span class="mode-pill" :class="MODE_STYLE[s.mode]?.pill">{{ s.mode }}</span></td>
            <td><strong class="score">{{ number(s.score) }}</strong></td>
            <td>{{ duration(s.duration) }} นาที</td>
            <td>{{ sessionDetail(s) }}</td>
            <td class="muted">{{ s.device_name || '—' }}</td>
            <td>{{ when(s.played_at) }}</td>
          </tr>
          <tr v-if="!list.loading.value && !list.rows.value.length"><td colspan="8" class="empty-state">ไม่พบการเล่นที่ตรงกับเงื่อนไข</td></tr>
        </tbody>
      </table>
    </div>
    <Pager v-model:page="list.page.value" :page-size="list.pageSize" :total="list.total.value" label="ครั้ง" />
  </section>
</template>
