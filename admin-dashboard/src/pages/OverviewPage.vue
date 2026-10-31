<script setup>
// ภาพรวม: ตัวเลขหลัก 4 ตัว + การฝึกรายวัน + โหมดที่นิยม + อารมณ์/อุปกรณ์ + ช่วงเวลาที่ฝึก
// กราฟแต่ละแบบมีหน้าที่ไม่ซ้ำกัน และสลับมุมมองได้ด้วยปุ่มเล็ก ๆ บนการ์ด
import { computed, ref, watch } from 'vue'
import { loadSessions, summarize, changePct, dailyByMode, modeTable, moodSummary, loadMoods,
  byHour, byWeekday, dailyPlayers, MOOD_KEYS } from '../lib/stats'
import { rpc } from '../lib/supabase'
import { MODE_COLOR, MODE_INFO, MOODS, number } from '../lib/format'
import DailyChart from '../components/DailyChart.vue'
import Donut from '../components/Donut.vue'
import Columns from '../components/Columns.vue'

const props = defineProps({ days: Number, me: Object })
const emit = defineEmits(['navigate'])

const data = ref({ cur: [], prev: [] })
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try { data.value = await loadSessions(props.days) }
  catch (e) { error.value = e.message }
  finally { loading.value = false }
}
watch(() => props.days, load, { immediate: true })

// อารมณ์ (เฉพาะ Super Admin เพราะเป็นข้อมูลส่วนตัว)
const isSuper = computed(() => props.me?.role === 'owner')
const moods = ref(null)
async function fetchMoods() {
  if (!isSuper.value) return
  try { moods.value = moodSummary((await loadMoods(props.days)).cur) } catch { moods.value = null }
}
watch(() => props.days, fetchMoods, { immediate: true })

// อุปกรณ์
const devices = ref([])
rpc('dashboard_devices', { p_search: null }).then(r => { devices.value = r ?? [] }).catch(() => {})
const lowBatt = d => d.battery_level != null && d.battery_level < 20
const dev = computed(() => ({
  total: devices.value.length,
  online: devices.value.filter(d => d.is_online).length,
  low: devices.value.filter(lowBatt).length,
}))

const s = computed(() => summarize(data.value.cur))
const p = computed(() => summarize(data.value.prev))
const delta = key => (props.days ? changePct(s.value[key], p.value[key]) : null)
const kpis = computed(() => [
  { label: 'ผู้ใช้ที่เข้ามาฝึก', value: number(s.value.players), unit: 'คน', d: delta('players'), how: 'จำนวนผู้ใช้ไม่ซ้ำกันที่ฝึกอย่างน้อย 1 ครั้งในช่วงเวลาที่เลือก' },
  { label: 'จำนวนครั้งที่ฝึก', value: number(s.value.sessions), unit: 'ครั้ง', d: delta('sessions'), how: 'จำนวนครั้งที่ฝึกรวมทุกโหมด' },
  { label: 'เวลาฝึกรวม', value: number(s.value.minutes / 60, 1), unit: 'ชั่วโมง', d: delta('minutes'), how: 'ผลรวมระยะเวลาการฝึกทุกครั้ง' },
  { label: 'ผู้ใช้ที่กลับมาฝึกซ้ำ', value: `${s.value.returningPct}%`, unit: '', d: null, how: 'ผู้ใช้ที่ฝึกมากกว่า 1 วัน ÷ ผู้ใช้ทั้งหมดที่ฝึกในช่วงนี้ × 100' },
])

// การฝึกรายวัน: สลับ จำนวนครั้ง / จำนวนผู้ใช้
const dailyView = ref('sessions')
const daily = computed(() => dailyByMode(data.value.cur, props.days))
const playersDaily = computed(() => dailyPlayers(data.value.cur, props.days))

// โหมดที่นิยม (เรียงจากมากไปน้อย)
const modes = computed(() => modeTable(data.value.cur).slice().sort((a, b) => b.sessions - a.sessions))
const maxShare = computed(() => Math.max(1, ...modes.value.map(m => m.share)))

// อารมณ์
const donutMoods = computed(() => (moods.value ? MOOD_KEYS.map(k => ({ label: MOODS[k].th, value: moods.value.byMood.find(x => x.key === k).people, color: MOODS[k].color })) : []))

// ช่วงเวลาที่ฝึก: สลับ รายชั่วโมง / วันในสัปดาห์
const timeView = ref('hour')
const timeData = computed(() => (timeView.value === 'hour' ? byHour(data.value.cur) : byWeekday(data.value.cur)))
const peak = computed(() => timeData.value.reduce((a, h) => (h.value > a.value ? h : a), timeData.value[0] ?? { label: '-', value: 0 }))
const periodText = computed(() => (props.days ? `${props.days} วันล่าสุด` : 'ทั้งหมด'))
const showMood = computed(() => isSuper.value && !!moods.value)
</script>

<template>
  <p v-if="error" class="banner err">{{ error }}</p>
  <p v-if="data.truncated" class="banner note">ข้อมูลมีมากกว่า 8,000 รายการ ระบบจึงคำนวณจาก 8,000 รายการล่าสุด กรุณาเลือกช่วงเวลาที่สั้นลงเพื่อความแม่นยำ</p>

  <!-- ตัวเลขหลัก -->
  <section class="kpis" :class="{ loading }">
    <div v-for="k in kpis" :key="k.label" class="kpi">
      <div class="label">{{ k.label }}<i class="info" :data-tip="k.how">i</i></div>
      <div class="value">{{ k.value }}<small v-if="k.unit">{{ k.unit }}</small></div>
      <div class="foot">
        <template v-if="k.d !== null"><span :class="k.d >= 0 ? 'up' : 'down'">{{ k.d >= 0 ? '▲' : '▼' }} {{ Math.abs(k.d) }}%</span> เทียบกับช่วงก่อนหน้า</template>
        <template v-else>{{ periodText }}</template>
      </div>
    </div>
  </section>

  <!-- การฝึกรายวัน -->
  <section class="card" :class="{ loading }">
    <div class="card-head">
      <h2>การฝึกรายวัน</h2>
      <div class="seg seg-sm">
        <button :class="{ on: dailyView === 'sessions' }" @click="dailyView = 'sessions'">จำนวนครั้ง</button>
        <button :class="{ on: dailyView === 'players' }" @click="dailyView = 'players'">จำนวนผู้ใช้</button>
      </div>
    </div>
    <DailyChart v-if="dailyView === 'sessions'" :days="daily" />
    <Columns v-else :data="playersDaily" :every="playersDaily.length > 14 ? 3 : 1" :height="200" unit="คน" how="จำนวนผู้ใช้ไม่ซ้ำกันที่ฝึกในวันนั้น" />
  </section>

  <section class="grid-2" :class="{ loading }">
    <!-- โหมดที่นิยม -->
    <article class="card">
      <div class="card-head"><h2>โหมดที่นิยม<i class="info" data-tip="สัดส่วน = จำนวนครั้งของโหมดนั้น ÷ จำนวนครั้งทุกโหมด × 100">i</i></h2><button class="btn-link small" @click="emit('navigate', 'modes')">ดูข้อมูลแต่ละโหมด</button></div>
      <ul class="rank">
        <li v-for="(m, i) in modes" :key="m.mode" class="click" @click="emit('navigate', 'modes', { mode: m.mode })">
          <span class="rank-no">{{ i + 1 }}</span>
          <span class="rank-name"><b>{{ m.mode }}</b><small>{{ MODE_INFO[m.mode] }}</small></span>
          <span class="rank-bar"><span :style="{ width: (m.share / maxShare) * 100 + '%', background: MODE_COLOR[m.mode] }"></span></span>
          <span class="rank-val"><b>{{ m.share }}%</b><small>{{ number(m.sessions) }} ครั้ง · {{ number(m.players) }} คน</small></span>
        </li>
      </ul>
    </article>

    <!-- อารมณ์ (Super Admin) / ช่วงเวลาที่ฝึก -->
    <article v-if="showMood" class="card">
      <div class="card-head"><h2>อารมณ์ของผู้ใช้</h2><button class="btn-link small" @click="emit('navigate', 'moods')">ดูรายละเอียด</button></div>
      <Donut v-if="moods.people" :parts="donutMoods" :center="moods.tensePct + '%'" center-sub="เครียด/โกรธ" unit="คน"
        how="นับผู้ใช้ตามอารมณ์ล่าสุดที่บันทึกในช่วงนี้ (1 คนนับ 1 ครั้ง)" />
      <p v-else class="empty">ยังไม่มีการบันทึกอารมณ์ในช่วงเวลานี้</p>
    </article>
    <article v-else class="card">
      <div class="card-head">
        <h2>ช่วงเวลาที่ฝึก</h2>
        <div class="seg seg-sm">
          <button :class="{ on: timeView === 'hour' }" @click="timeView = 'hour'">รายชั่วโมง</button>
          <button :class="{ on: timeView === 'week' }" @click="timeView = 'week'">วันในสัปดาห์</button>
        </div>
      </div>
      <p class="muted small" style="margin:-6px 0 12px">ฝึกมากที่สุด: <b>{{ timeView === 'hour' ? peak.label + ':00 น.' : peak.label }}</b> ({{ peak.value }} ครั้ง)</p>
      <Columns :data="timeData" :every="timeView === 'hour' ? 3 : 1" :height="170" :color="timeView === 'hour' ? '#6f9a52' : '#d9a441'" unit="ครั้ง"
        :how="timeView === 'hour' ? 'นับจำนวนครั้งที่เริ่มฝึกในแต่ละชั่วโมง (เวลาประเทศไทย)' : 'นับจำนวนครั้งที่ฝึกในแต่ละวันของสัปดาห์'" />
    </article>
  </section>

  <section class="grid-2 wide-left" :class="{ loading }">
    <article v-if="showMood" class="card">
      <div class="card-head">
        <h2>ช่วงเวลาที่ฝึก</h2>
        <div class="seg seg-sm">
          <button :class="{ on: timeView === 'hour' }" @click="timeView = 'hour'">รายชั่วโมง</button>
          <button :class="{ on: timeView === 'week' }" @click="timeView = 'week'">วันในสัปดาห์</button>
        </div>
      </div>
      <p class="muted small" style="margin:-6px 0 12px">ฝึกมากที่สุด: <b>{{ timeView === 'hour' ? peak.label + ':00 น.' : peak.label }}</b> ({{ peak.value }} ครั้ง)</p>
      <Columns :data="timeData" :every="timeView === 'hour' ? 3 : 1" :height="170" :color="timeView === 'hour' ? '#6f9a52' : '#d9a441'" unit="ครั้ง"
        :how="timeView === 'hour' ? 'นับจำนวนครั้งที่เริ่มฝึกในแต่ละชั่วโมง (เวลาประเทศไทย)' : 'นับจำนวนครั้งที่ฝึกในแต่ละวันของสัปดาห์'" />
    </article>
    <article class="card" :style="showMood ? null : 'grid-column: 1 / -1'">
      <div class="card-head"><h2>อุปกรณ์ Breath Gadget</h2><button class="btn-link small" @click="emit('navigate', 'devices')">ดูรายละเอียด</button></div>
      <div class="dev-stats">
        <div><span class="muted small">ออนไลน์อยู่ตอนนี้</span><b>{{ dev.online }}<small> / {{ dev.total }} เครื่อง</small></b></div>
        <div><span class="muted small">แบตเตอรี่ต่ำกว่า 20%</span><b :class="{ bad: dev.low }">{{ dev.low }}<small> เครื่อง</small></b></div>
      </div>
    </article>
  </section>
</template>
