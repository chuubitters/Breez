<script setup>
import { computed, ref } from 'vue'
import { rpc } from '../lib/supabase'
import { useRemoteList } from '../lib/useRemoteList'
import { MOODS, avatarColor, downloadCsv, initials, number, when } from '../lib/format'
import Pager from '../components/Pager.vue'

const props = defineProps({ me: Object, search: String })
const days = ref(null)

const list = useRemoteList(
  ({ limit, offset }) => rpc('dashboard_moods', { p_days: days.value, p_search: props.search || null, p_limit: limit, p_offset: offset }),
  { pageSize: 25, deps: [days, () => props.search] },
)

const summary = ref(null)
const loadSummary = () => rpc('dashboard_overview', { p_days: days.value }).then(d => (summary.value = d)).catch(() => {})
loadSummary()

const moodTotal = computed(() => (summary.value?.moods ?? []).reduce((a, m) => a + m.count, 0))
const top = computed(() => summary.value?.moods?.[0])
const stressPct = computed(() => {
  const s = summary.value?.moods?.find(m => m.mood === 'Stress' || m.mood === 'Angry') ? summary.value.moods.filter(m => ['Stress', 'Angry'].includes(m.mood)).reduce((a, m) => a + m.count, 0) : 0
  return moodTotal.value ? Math.round((s / moodTotal.value) * 100) : 0
})

async function exportCsv() {
  const rows = await rpc('dashboard_moods', { p_days: days.value, p_search: props.search || null, p_limit: 500, p_offset: 0 })
  downloadCsv('fuuu-mood-logs.csv', ['mood_id', 'user_id', 'player', 'mood', 'note', 'recorded_at'],
    rows.map(m => [m.mood_id, m.user_id, m.player, m.mood, m.note, m.recorded_at]))
}
</script>

<template>
  <section class="welcome-row section-welcome">
    <div><div class="eyebrow"><span class="eyebrow-dot"></span> MOOD JOURNAL · OWNER ONLY</div><h1>บันทึก<span class="highlight">อารมณ์</span></h1><p class="page-description">ข้อมูลอารมณ์เป็นข้อมูลส่วนบุคคล หน้านี้จึงเปิดให้เฉพาะเจ้าของระบบ</p></div>
    <div class="header-actions">
      <select class="filter-select" v-model="days" @change="loadSummary"><option :value="null">ทุกช่วงเวลา</option><option :value="7">7 วันล่าสุด</option><option :value="30">30 วันล่าสุด</option></select>
      <button class="secondary-button" @click="exportCsv">↓ ส่งออก CSV</button>
    </div>
  </section>

  <section class="stats-grid compact-stats">
    <article class="stat-card stat-lilac"><div class="stat-label">บันทึกอารมณ์</div><div class="stat-value">{{ number(summary?.moods_total) }}</div><div class="stat-foot">รายการในช่วงที่เลือก</div></article>
    <article class="stat-card stat-green"><div class="stat-label">อารมณ์ที่พบบ่อย</div><div class="stat-value">{{ top ? `${top.mood} ${MOODS[top.mood]?.emoji}` : '—' }}</div><div class="stat-foot">{{ top ? `${number(top.count)} ครั้ง` : 'ยังไม่มีข้อมูล' }}</div></article>
    <article class="stat-card stat-peach"><div class="stat-label">เครียด / โกรธ</div><div class="stat-value">{{ stressPct }}<span class="stat-unit">%</span></div><div class="stat-foot">ของบันทึกทั้งหมด</div></article>
    <article class="stat-card stat-yellow">
      <div class="stat-label">การกระจายอารมณ์</div>
      <div class="mood-strip"><span v-for="m in summary?.moods ?? []" :key="m.mood" :class="MOODS[m.mood]?.cls" :style="{ flex: m.count }" :title="`${m.mood} ${m.count}`"></span></div>
      <div class="stat-foot">{{ (summary?.moods ?? []).map(m => m.mood).join(' · ') || '—' }}</div>
    </article>
  </section>

  <section class="panel full-panel">
    <div class="panel-heading"><div><h2>รายการบันทึกอารมณ์</h2><p>ค้นหาด้วยชื่อผู้เล่นหรือชื่ออารมณ์ (เช่น Stress)</p></div></div>
    <p v-if="list.error.value" class="banner-error">{{ list.error.value }}</p>
    <div class="table-wrap" :class="{ loading: list.loading.value }">
      <table class="wide-table">
        <thead><tr><th>ผู้เล่น</th><th>อารมณ์</th><th>บันทึกเพิ่มเติม</th><th>วันที่และเวลา</th></tr></thead>
        <tbody>
          <tr v-for="m in list.rows.value" :key="m.mood_id">
            <td><div class="player-cell"><div class="tiny-avatar" :class="avatarColor(m.user_id)">{{ initials(m.player) }}</div><strong>{{ m.player }}</strong></div></td>
            <td><span class="mood-tag" :class="MOODS[m.mood]?.cls">{{ MOODS[m.mood]?.emoji }} {{ m.mood }}</span></td>
            <td>{{ m.note || '—' }}</td>
            <td>{{ when(m.recorded_at) }}</td>
          </tr>
          <tr v-if="!list.loading.value && !list.rows.value.length"><td colspan="4" class="empty-state">ยังไม่มีบันทึกอารมณ์</td></tr>
        </tbody>
      </table>
    </div>
    <Pager v-model:page="list.page.value" :page-size="list.pageSize" :total="list.total.value" label="รายการ" />
  </section>
</template>
