<script setup>
// อารมณ์ (owner): สัดส่วนอารมณ์ในช่วงเวลา + บันทึกแต่ละครั้ง
import { computed, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { useRemoteList } from '../lib/useRemoteList'
import { MOODS, downloadCsv, number, when } from '../lib/format'
import BarList from '../components/BarList.vue'
import Pager from '../components/Pager.vue'

const props = defineProps({ days: Number })
const search = ref('')
const list = useRemoteList(
  ({ limit, offset }) => rpc('dashboard_moods', { p_days: props.days, p_search: search.value || null, p_limit: limit, p_offset: offset }),
  { pageSize: 25, deps: [search, () => props.days] },
)

const all = ref([])
async function loadAll() {
  try { all.value = await rpc('dashboard_moods', { p_days: props.days, p_search: null, p_limit: 1000, p_offset: 0 }) ?? [] }
  catch { all.value = [] }
}
watch(() => props.days, loadAll, { immediate: true })

const dist = computed(() => Object.entries(MOODS).map(([k, m]) => ({ label: m.th, value: all.value.filter(r => r.mood === k).length, color: m.color })))
const people = computed(() => new Set(all.value.map(r => r.user_id)).size)
const tense = computed(() => all.value.filter(r => r.mood === 'Stress' || r.mood === 'Angry').length)

async function exportCsv() {
  downloadCsv('breez-moods.csv', ['player', 'mood', 'note', 'recorded_at'], all.value.map(m => [m.player, m.mood, m.note, m.recorded_at]))
}
</script>

<template>
  <section class="grid-3">
    <article class="card">
      <div class="card-head"><h2>สัดส่วนอารมณ์</h2><p>{{ number(all.length) }} ครั้ง จาก {{ number(people) }} คน</p></div>
      <BarList :data="dist" percent />
    </article>
    <article class="card">
      <div class="card-head"><h2>เครียด / โกรธ</h2></div>
      <div class="kpi" style="border:0;padding:0">
        <div class="value">{{ all.length ? Math.round(tense / all.length * 100) : 0 }}%</div>
        <div class="foot">{{ number(tense) }} ครั้งในช่วงนี้ — ใช้ดูว่ากลุ่มผู้ใช้ต้องการโหมดผ่อนคลายมากแค่ไหน</div>
      </div>
    </article>
  </section>

  <section class="card">
    <div class="toolbar" style="margin-bottom:12px">
      <input v-model="search" class="input search" placeholder="ค้นหาชื่อผู้เล่น" />
      <span class="spacer"></span>
      <button class="btn" @click="exportCsv">ส่งออก CSV</button>
    </div>
    <p v-if="list.error.value" class="banner err">{{ list.error.value }}</p>
    <div class="table-wrap" :class="{ loading: list.loading.value }">
      <table>
        <thead><tr><th>เวลา</th><th>ผู้เล่น</th><th>อารมณ์</th><th>โน้ต</th></tr></thead>
        <tbody>
          <tr v-for="m in list.rows.value" :key="m.mood_id">
            <td class="muted" style="white-space:nowrap">{{ when(m.recorded_at) }}</td>
            <td>{{ m.player }}</td>
            <td><i class="dot" :style="{ background: MOODS[m.mood]?.color }"></i>{{ MOODS[m.mood]?.th ?? m.mood }}</td>
            <td class="muted">{{ m.note || '—' }}</td>
          </tr>
          <tr v-if="!list.loading.value && !list.rows.value.length"><td colspan="4" class="empty">ไม่มีบันทึกอารมณ์ในช่วงนี้</td></tr>
        </tbody>
      </table>
    </div>
    <Pager v-model:page="list.page.value" :page-size="list.pageSize" :total="list.total.value" />
  </section>
</template>
