<script setup>
// ประวัติการเล่น: ทุกครั้ง + กรองโหมด/ผู้เล่น + ส่งออก CSV
import { ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { useRemoteList } from '../lib/useRemoteList'
import { MODES, MODE_COLOR, downloadCsv, duration, sessionDetail, when } from '../lib/format'
import Pager from '../components/Pager.vue'

const props = defineProps({ days: Number, playerFilter: Object, modeFilter: String })
const emit = defineEmits(['clear-player'])

const mode = ref(props.modeFilter || '')
const search = ref('')

let useV2 = true
async function fetchSessions(a) {
  if (useV2) {
    try { return await rpc('dashboard_play_sessions_v2', a) }
    catch (e) { if (!/dashboard_play_sessions_v2|schema cache|does not exist/i.test(e.message)) throw e; useV2 = false }
  }
  return rpc('dashboard_play_sessions', a)
}
const args = (limit, offset) => ({
  p_mode: mode.value || null,
  p_days: props.days,
  p_search: search.value || null,
  p_user_id: props.playerFilter?.id ?? null,
  p_limit: limit,
  p_offset: offset,
})
const list = useRemoteList(({ limit, offset }) => fetchSessions(args(limit, offset)),
  { pageSize: 25, deps: [mode, search, () => props.days, () => props.playerFilter] })

const exporting = ref(false)
async function exportCsv() {
  exporting.value = true
  try {
    const rows = await fetchSessions(args(1000, 0)) ?? []
    downloadCsv('breez-sessions.csv',
      ['session_id', 'player', 'mode', 'score', 'duration_seconds', 'result', 'played_at'],
      rows.map(s => [s.session_id, s.player, s.mode, s.score, s.duration, sessionDetail(s), s.played_at]))
  } finally { exporting.value = false }
}
watch(() => props.modeFilter, v => { if (v) mode.value = v })
</script>

<template>
  <section class="card">
    <div class="toolbar" style="margin-bottom:12px">
      <select v-model="mode" class="select">
        <option value="">ทุกโหมด</option>
        <option v-for="m in MODES" :key="m" :value="m">{{ m }}</option>
      </select>
      <input v-model="search" class="input search" placeholder="ค้นหาชื่อผู้ใช้" />
      <span v-if="playerFilter" class="chip">ผู้ใช้: {{ playerFilter.name }} <button class="btn-link" @click="emit('clear-player')">ล้าง</button></span>
      <span class="spacer"></span>
      <button class="btn" :disabled="exporting" @click="exportCsv">{{ exporting ? 'กำลังส่งออกไฟล์…' : 'ส่งออก CSV' }}</button>
    </div>
    <p v-if="list.error.value" class="banner err">{{ list.error.value }}</p>
    <div class="table-wrap" :class="{ loading: list.loading.value }">
      <table>
        <thead><tr><th>เวลา</th><th>ผู้ใช้</th><th>โหมด</th><th>ผลการเล่น</th><th class="r">คะแนน</th><th class="r">ระยะเวลา</th></tr></thead>
        <tbody>
          <tr v-for="s in list.rows.value" :key="s.session_id">
            <td class="muted" style="white-space:nowrap">{{ when(s.played_at) }}</td>
            <td>{{ s.player }}</td>
            <td style="white-space:nowrap"><i class="dot" :style="{ background: MODE_COLOR[s.mode] }"></i>{{ s.mode }}</td>
            <td>{{ sessionDetail(s) }}</td>
            <td class="r num">{{ s.score || '—' }}</td>
            <td class="r num">{{ duration(s.duration) }}</td>
          </tr>
          <tr v-if="!list.loading.value && !list.rows.value.length"><td colspan="6" class="empty">ไม่พบข้อมูลที่ตรงกับเงื่อนไขที่เลือก</td></tr>
        </tbody>
      </table>
    </div>
    <Pager v-model:page="list.page.value" :page-size="list.pageSize" :total="list.total.value" />
  </section>
</template>
