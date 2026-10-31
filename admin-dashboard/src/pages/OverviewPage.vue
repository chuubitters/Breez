<script setup>
// ภาพรวม: ตัวเลขหลัก + ข้อสังเกต + กราฟวงกลม/แท่งหลายมุม + ตารางโหมด
import { computed, ref, watch } from 'vue'
import { loadSessions, summarize, changePct, dailyByMode, modeTable, findings, secText, moodSummary, loadMoods,
  byHour, byWeekday, durationBuckets, frequencyBuckets, dailyPlayers, MOOD_KEYS } from '../lib/stats'
import { rpc } from '../lib/supabase'
import { MODE_COLOR, MODE_INFO, MOODS, number } from '../lib/format'
import DailyChart from '../components/DailyChart.vue'
import MoodPulse from '../components/MoodPulse.vue'
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

// ภาพรวมอารมณ์ (เฉพาะ Super Admin เพราะข้อมูลอารมณ์เป็นข้อมูลส่วนตัว)
const isSuper = computed(() => props.me?.role === 'owner')
const moods = ref({ cur: null, prev: null })
async function fetchMoods() {
  if (!isSuper.value) return
  try {
    const { cur, prev } = await loadMoods(props.days)
    moods.value = { cur: moodSummary(cur), prev: props.days ? moodSummary(prev) : null }
  } catch { moods.value = { cur: null, prev: null } }
}
watch(() => props.days, fetchMoods, { immediate: true })

const s = computed(() => summarize(data.value.cur))
const p = computed(() => summarize(data.value.prev))
const delta = key => (props.days ? changePct(s.value[key], p.value[key]) : null)
// อุปกรณ์ (ทุกบัญชีเห็นได้)
const devices = ref([])
rpc('dashboard_devices', { p_search: null }).then(r => { devices.value = r ?? [] }).catch(() => {})

const kpis = computed(() => [
  { label: 'ผู้ใช้ที่เข้ามาฝึก', value: number(s.value.players), unit: 'คน', d: delta('players'), foot: 'นับผู้ใช้ไม่ซ้ำกัน' },
  { label: 'จำนวนครั้งที่ฝึก', value: number(s.value.sessions), unit: 'ครั้ง', d: delta('sessions'), foot: 'รวมทุกโหมด' },
  { label: 'เวลาฝึกรวม', value: number(s.value.minutes / 60, 1), unit: 'ชั่วโมง', d: delta('minutes'), foot: `${number(s.value.minutes, 0)} นาที` },
  { label: 'เวลาต่อครั้ง (มัธยฐาน)', value: secText(s.value.medianDuration), unit: '', d: delta('medianDuration'), foot: `เฉลี่ย ${secText(s.value.avgDuration)}` },
  { label: 'ผู้ใช้ที่กลับมาฝึกซ้ำ', value: `${s.value.returningPct}%`, unit: '', d: null, foot: `${s.value.returning} จาก ${s.value.players} คน ฝึกมากกว่า 1 วัน` },
  { label: 'อุปกรณ์ออนไลน์', value: `${devices.value.filter(d => d.is_online).length}/${devices.value.length}`, unit: 'เครื่อง', d: null, foot: `แบตเตอรี่ต่ำ ${devices.value.filter(lowBatt).length} เครื่อง` },
])
const lowBatt = d => d.battery_level != null && d.battery_level < 20
const cur = computed(() => data.value.cur)
const donutModes = computed(() => modes.value.map(m => ({ label: m.mode, value: m.sessions, color: MODE_COLOR[m.mode] })))
const donutMoods = computed(() => (moods.value.cur ? MOOD_KEYS.map(k => ({ label: MOODS[k].th, value: moods.value.cur.byMood.find(x => x.key === k).people, color: MOODS[k].color })) : []))
const donutChannel = computed(() => {
  const gadget = cur.value.filter(r => r.mode === 'Quick Session').length
  return [{ label: 'แอปพลิเคชัน', value: cur.value.length - gadget, color: '#6f9a52' }, { label: 'อุปกรณ์ Breath Gadget', value: gadget, color: '#9a86cf' }]
})
const FREQ_COLORS = ['#dfe6d6', '#b9cf9f', '#86ad62', '#4f7a3a']
const donutFreq = computed(() => frequencyBuckets(cur.value).map((b, i) => ({ ...b, color: FREQ_COLORS[i] })))
const DUR_COLORS = ['#f1d9a8', '#e8c171', '#d9a441', '#b88a2e', '#8c6a22']
const donutDur = computed(() => durationBuckets(cur.value).map((b, i) => ({ ...b, color: DUR_COLORS[i] })))
const donutDevices = computed(() => {
  const on = devices.value.filter(d => d.is_online).length
  const low = devices.value.filter(lowBatt).length
  return [{ label: 'ออนไลน์', value: on, color: '#5f9b4c' }, { label: 'ออฟไลน์', value: devices.value.length - on, color: '#c9cdc4' }, { label: 'แบตเตอรี่ต่ำกว่า 20%', value: low, color: '#c9564f' }]
})
const hours = computed(() => byHour(cur.value))
const weekdays = computed(() => byWeekday(cur.value))
const playersDaily = computed(() => dailyPlayers(cur.value, props.days))
const peakHour = computed(() => hours.value.reduce((a, h) => (h.value > a.value ? h : a), hours.value[0]))
const notes = computed(() => findings(data.value.cur, data.value.prev))
const daily = computed(() => dailyByMode(data.value.cur, props.days))
const modes = computed(() => modeTable(data.value.cur))
const periodText = computed(() => (props.days ? `${props.days} วันล่าสุด` : 'ทั้งหมด'))
</script>

<template>
  <p v-if="error" class="banner err">{{ error }}</p>
  <p v-if="data.truncated" class="banner note">ข้อมูลมีมากกว่า 8,000 รายการ ระบบจึงคำนวณจาก 8,000 รายการล่าสุด กรุณาเลือกช่วงเวลาที่สั้นลงเพื่อความแม่นยำ</p>

  <section class="kpis six" :class="{ loading }">
    <div v-for="k in kpis" :key="k.label" class="kpi">
      <div class="label">{{ k.label }}</div>
      <div class="value">{{ k.value }}<small v-if="k.unit">{{ k.unit }}</small></div>
      <div class="foot">
        <span v-if="k.d !== null" :class="k.d >= 0 ? 'up' : 'down'">{{ k.d >= 0 ? '+' : '' }}{{ k.d }}% เทียบกับช่วงก่อนหน้า · </span>{{ k.foot }}
      </div>
    </div>
  </section>

  <section class="card" :class="{ loading }">
    <div class="card-head"><h2>ประเด็นที่ควรตรวจสอบ</h2><p>สรุปอัตโนมัติจากข้อมูลการฝึก ({{ periodText }})</p></div>
    <ul class="findings">
      <li v-for="(f, i) in notes" :key="i" class="finding" :class="f.level">
        <span class="tag">{{ f.level === 'warn' ? 'ควรตรวจสอบ' : f.level === 'ok' ? 'ปกติ' : 'ข้อมูล' }}</span>
        <span><b v-if="f.mode"><i class="dot" :style="{ background: MODE_COLOR[f.mode] }"></i>{{ f.mode }}: </b>{{ f.text }}
          <button v-if="f.mode" class="btn-link small" @click="emit('navigate', 'modes', { mode: f.mode })">ดูรายละเอียด</button>
        </span>
      </li>
    </ul>
  </section>


  <section class="grid-3eq" :class="{ loading }">
    <article class="card">
      <div class="card-head"><h2>สัดส่วนรูปแบบการฝึก</h2><p>จำนวนครั้ง</p></div>
      <Donut :parts="donutModes" :center-sub="'ครั้ง'" unit="ครั้ง" />
    </article>
    <article v-if="isSuper && moods.cur" class="card">
      <div class="card-head"><h2>อารมณ์ของผู้ใช้</h2><p>จำนวนผู้ใช้ (อารมณ์ล่าสุด)</p></div>
      <Donut :parts="donutMoods" :center="moods.cur.tensePct + '%'" center-sub="เครียด/โกรธ" unit="คน" />
    </article>
    <article class="card">
      <div class="card-head"><h2>ช่องทางการฝึก</h2><p>จำนวนครั้ง</p></div>
      <Donut :parts="donutChannel" center-sub="ครั้ง" unit="ครั้ง" />
    </article>
    <article class="card">
      <div class="card-head"><h2>ความถี่ในการฝึกต่อผู้ใช้</h2><p>จำนวนผู้ใช้</p></div>
      <Donut :parts="donutFreq" center-sub="คน" unit="คน" />
    </article>
    <article class="card">
      <div class="card-head"><h2>ระยะเวลาการฝึกต่อครั้ง</h2><p>จำนวนครั้ง</p></div>
      <Donut :parts="donutDur" center-sub="ครั้ง" unit="ครั้ง" />
    </article>
    <article class="card">
      <div class="card-head"><h2>สถานะอุปกรณ์</h2><button class="btn-link small" @click="emit('navigate', 'devices')">ดูรายละเอียด</button></div>
      <Donut :parts="donutDevices.slice(0, 2)" center-sub="เครื่อง" unit="เครื่อง" />
      <p v-if="donutDevices[2].value" class="small" style="margin-top:10px;color:#a3463f">แบตเตอรี่ต่ำกว่า 20%: {{ donutDevices[2].value }} เครื่อง</p>
    </article>
  </section>

  <section class="card" :class="{ loading }">
    <div class="card-head"><h2>จำนวนครั้งที่ฝึกต่อวัน</h2><p>{{ days && days <= 60 ? periodText : '60 วันล่าสุด' }} · แยกตามโหมด</p></div>
    <DailyChart :days="daily" />
  </section>

  <section class="card" :class="{ loading }">
    <div class="card-head"><h2>จำนวนผู้ใช้ที่ฝึกต่อวัน</h2><p>นับผู้ใช้ไม่ซ้ำกันในแต่ละวัน</p></div>
    <Columns :data="playersDaily" :every="playersDaily.length > 14 ? 3 : 1" unit="คน" />
  </section>

  <section class="grid-2" :class="{ loading }">
    <article class="card">
      <div class="card-head"><h2>ช่วงเวลาที่ฝึกในแต่ละวัน</h2><p>ช่วงที่มีการฝึกมากที่สุด: {{ peakHour?.label }}:00 น.</p></div>
      <Columns :data="hours" :every="3" color="#6f9a52" unit="ครั้ง" />
    </article>
    <article class="card">
      <div class="card-head"><h2>การฝึกแยกตามวันในสัปดาห์</h2><p>จำนวนครั้ง</p></div>
      <Columns :data="weekdays" color="#d9a441" unit="ครั้ง" />
    </article>
  </section>

  <section class="card" :class="{ loading }">
    <div class="card-head"><h2>สรุปแยกตามรูปแบบการฝึก</h2><p>ผู้ใช้ {{ number(s.players) }} คน · {{ number(s.sessions) }} ครั้ง · สัดส่วนคำนวณจากจำนวนครั้ง</p></div>
    <div class="table-wrap">
      <table>
        <thead><tr><th>รูปแบบ</th><th class="r">ผู้ใช้ที่ฝึก</th><th class="r">ครั้ง</th><th class="r">เวลาเฉลี่ย/ครั้ง</th><th class="r">สัดส่วน</th></tr></thead>
        <tbody>
          <tr v-for="m in modes" :key="m.mode" class="click" @click="emit('navigate', 'modes', { mode: m.mode })">
            <td><i class="dot" :style="{ background: MODE_COLOR[m.mode] }"></i>{{ m.mode }}<span class="sub">{{ MODE_INFO[m.mode] }}</span></td>
            <td class="r num">{{ number(m.players) }} <span class="muted small">({{ s.players ? Math.round(m.players / s.players * 100) : 0 }}%)</span></td>
            <td class="r num">{{ number(m.sessions) }}</td>
            <td class="r num">{{ m.sessions ? secText(m.avgDuration) : '—' }}</td>
            <td class="r"><div class="share-cell"><div class="track"><span :style="{ width: m.share + '%', background: MODE_COLOR[m.mode] }"></span></div><b class="num">{{ m.share }}%</b></div></td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>

  <section v-if="isSuper && moods.cur" class="card">
    <div class="card-head"><h2>อารมณ์โดยรวม</h2><button class="btn-link" @click="emit('navigate', 'moods')">ดูรายละเอียด</button></div>
    <MoodPulse :m="moods.cur" :prev="moods.prev" :period-text="periodText" compact />
  </section>
</template>
