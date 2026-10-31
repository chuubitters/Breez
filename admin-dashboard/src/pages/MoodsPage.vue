<script setup>
// อารมณ์ (Super Admin): จำนวนคนในแต่ละอารมณ์ + ภาพรวมความเครียด + บันทึกแต่ละครั้ง
import { computed, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { useRemoteList } from '../lib/useRemoteList'
import { moodSummary, loadMoods } from '../lib/stats'
import { MOODS, downloadCsv, number, when } from '../lib/format'
import MoodPulse from '../components/MoodPulse.vue'
import Pager from '../components/Pager.vue'

const props = defineProps({ days: Number })
const search = ref('')
const list = useRemoteList(
  ({ limit, offset }) => rpc('dashboard_moods', { p_days: props.days, p_search: search.value || null, p_limit: limit, p_offset: offset }),
  { pageSize: 25, deps: [search, () => props.days] },
)

// โหลดช่วงนี้ + ช่วงก่อนหน้า เพื่อเทียบว่าเครียดขึ้นหรือลดลง
const cur = ref([])
const prev = ref([])
const loading = ref(true)
async function loadAll() {
  loading.value = true
  try {
    ({ cur: cur.value, prev: prev.value } = await loadMoods(props.days))
  } catch { cur.value = []; prev.value = [] }
  finally { loading.value = false }
}
watch(() => props.days, loadAll, { immediate: true })

const m = computed(() => moodSummary(cur.value))
const p = computed(() => (props.days ? moodSummary(prev.value) : null))
const periodText = computed(() => (props.days ? ` ${props.days} วันที่ผ่านมา` : 'ที่ผ่านมา'))
const maxPeople = computed(() => Math.max(1, ...m.value.byMood.map(x => x.people)))

function exportCsv() {
  downloadCsv('breez-moods.csv', ['player', 'mood', 'note', 'recorded_at'], cur.value.map(r => [r.player, r.mood, r.note, r.recorded_at]))
}
</script>

<template>
  <section class="grid-2" :class="{ loading }">
    <article class="card">
      <div class="card-head"><h2>จำนวนผู้ใช้แยกตามอารมณ์</h2><p>{{ number(m.people) }} คน · {{ number(m.records) }} รายการบันทึก</p></div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>อารมณ์</th><th class="r">จำนวนผู้ใช้</th><th class="r">สัดส่วน</th><th class="r">จำนวนบันทึก</th></tr></thead>
          <tbody>
            <tr v-for="x in m.byMood" :key="x.key">
              <td><i class="dot" :style="{ background: MOODS[x.key].color }"></i>{{ MOODS[x.key].th }}</td>
              <td class="r num"><b>{{ number(x.people) }}</b></td>
              <td class="r"><div class="share-cell"><div class="track"><span :style="{ width: (x.people / maxPeople * 100) + '%', background: MOODS[x.key].color }"></span></div><b class="num">{{ x.pct }}%</b></div></td>
              <td class="r num muted">{{ number(x.records) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="muted small" style="margin-top:10px">นับจากอารมณ์ล่าสุดที่ผู้ใช้แต่ละคนบันทึกในช่วงเวลานี้ (ผู้ใช้ 1 คนนับเพียง 1 ครั้ง) ผลรวมจึงเท่ากับ 100%</p>
    </article>

    <article class="card">
      <div class="card-head"><h2>อารมณ์โดยรวม</h2><p>สำหรับใช้ประชาสัมพันธ์และเชิญชวนให้ฝึกหายใจ</p></div>
      <MoodPulse :m="m" :prev="p" :period-text="periodText" />
    </article>
  </section>

  <section class="card">
    <div class="card-head"><h2>รายการบันทึกอารมณ์</h2></div>
    <div class="toolbar" style="margin-bottom:12px">
      <input v-model="search" class="input search" placeholder="ค้นหาชื่อผู้ใช้" />
      <span class="spacer"></span>
      <button class="btn" @click="exportCsv">ส่งออก CSV</button>
    </div>
    <p v-if="list.error.value" class="banner err">{{ list.error.value }}</p>
    <div class="table-wrap" :class="{ loading: list.loading.value }">
      <table>
        <thead><tr><th>เวลา</th><th>ผู้ใช้</th><th>อารมณ์</th><th>บันทึกเพิ่มเติม</th></tr></thead>
        <tbody>
          <tr v-for="r in list.rows.value" :key="r.mood_id">
            <td class="muted" style="white-space:nowrap">{{ when(r.recorded_at) }}</td>
            <td>{{ r.player }}</td>
            <td><i class="dot" :style="{ background: MOODS[r.mood]?.color }"></i>{{ MOODS[r.mood]?.th ?? r.mood }}</td>
            <td class="muted">{{ r.note || '—' }}</td>
          </tr>
          <tr v-if="!list.loading.value && !list.rows.value.length"><td colspan="4" class="empty">ไม่พบบันทึกอารมณ์ในช่วงเวลานี้</td></tr>
        </tbody>
      </table>
    </div>
    <Pager v-model:page="list.page.value" :page-size="list.pageSize" :total="list.total.value" />
  </section>
</template>
