<script setup>
// ภาพรวม: ตัวเลขหลัก + ข้อสังเกตที่ควรปรับ + การเล่นรายวัน + ตารางโหมด
import { computed, ref, watch } from 'vue'
import { loadSessions, summarize, changePct, dailyByMode, modeTable, findings, secText } from '../lib/stats'
import { MODE_COLOR, number, when } from '../lib/format'
import DailyChart from '../components/DailyChart.vue'

const props = defineProps({ days: Number })
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

const s = computed(() => summarize(data.value.cur))
const p = computed(() => summarize(data.value.prev))
const delta = key => (props.days ? changePct(s.value[key], p.value[key]) : null)
const kpis = computed(() => [
  { label: 'ผู้เล่นที่เล่น', value: number(s.value.players), unit: 'คน', d: delta('players'), foot: `เฉลี่ย ${s.value.perPlayer.toFixed(1)} ครั้ง/คน` },
  { label: 'จำนวนครั้งที่เล่น', value: number(s.value.sessions), unit: 'ครั้ง', d: delta('sessions'), foot: `รวม ${number(s.value.minutes, 0)} นาที` },
  { label: 'เวลาต่อครั้ง (มัธยฐาน)', value: secText(s.value.medianDuration), unit: '', d: delta('medianDuration'), foot: `เฉลี่ย ${secText(s.value.avgDuration)}` },
  { label: 'กลับมาเล่นซ้ำ', value: `${s.value.returningPct}%`, unit: '', d: null, foot: `${s.value.returning} จาก ${s.value.players} คน เล่นมากกว่า 1 วัน` },
])
const notes = computed(() => findings(data.value.cur, data.value.prev))
const daily = computed(() => dailyByMode(data.value.cur, props.days))
const modes = computed(() => modeTable(data.value.cur))
const periodText = computed(() => (props.days ? `${props.days} วันล่าสุด` : 'ทั้งหมด'))
</script>

<template>
  <p v-if="error" class="banner err">{{ error }}</p>
  <p v-if="data.truncated" class="banner note">ข้อมูลเยอะเกิน 8,000 ครั้ง ตัวเลขคำนวณจาก 8,000 ครั้งล่าสุด ลองเลือกช่วงเวลาที่สั้นลง</p>

  <section class="kpis" :class="{ loading }">
    <div v-for="k in kpis" :key="k.label" class="kpi">
      <div class="label">{{ k.label }}</div>
      <div class="value">{{ k.value }}<small v-if="k.unit">{{ k.unit }}</small></div>
      <div class="foot">
        <span v-if="k.d !== null" :class="k.d >= 0 ? 'up' : 'down'">{{ k.d >= 0 ? '+' : '' }}{{ k.d }}% จากช่วงก่อน · </span>{{ k.foot }}
      </div>
    </div>
  </section>

  <section class="card" :class="{ loading }">
    <div class="card-head"><h2>ควรดู / ควรปรับ</h2><p>สรุปอัตโนมัติจากผลการเล่น {{ periodText }}</p></div>
    <ul class="findings">
      <li v-for="(f, i) in notes" :key="i" class="finding" :class="f.level">
        <span class="tag">{{ f.level === 'warn' ? 'ควรดู' : f.level === 'ok' ? 'ปกติ' : 'ข้อมูล' }}</span>
        <span><b v-if="f.mode"><i class="dot" :style="{ background: MODE_COLOR[f.mode] }"></i>{{ f.mode }}: </b>{{ f.text }}
          <button v-if="f.mode" class="btn-link small" @click="emit('navigate', 'modes', { mode: f.mode })">ดูรายละเอียด</button>
        </span>
      </li>
    </ul>
  </section>

  <section class="card" :class="{ loading }">
    <div class="card-head"><h2>จำนวนครั้งที่เล่นต่อวัน</h2><p>{{ days && days <= 60 ? periodText : '60 วันล่าสุด' }}</p></div>
    <DailyChart :days="daily" />
  </section>

  <section class="card" :class="{ loading }">
    <div class="card-head"><h2>แต่ละโหมด</h2><button class="btn-link" @click="emit('navigate', 'modes')">ดูตัวชี้วัดของแต่ละโหมด</button></div>
    <div class="table-wrap">
      <table>
        <thead><tr><th>โหมด</th><th class="r">ครั้ง</th><th class="r">ผู้เล่น</th><th class="r">เวลาเฉลี่ย/ครั้ง</th><th class="r">สัดส่วน</th><th>เล่นล่าสุด</th></tr></thead>
        <tbody>
          <tr v-for="m in modes" :key="m.mode" class="click" @click="emit('navigate', 'modes', { mode: m.mode })">
            <td><i class="dot" :style="{ background: MODE_COLOR[m.mode] }"></i>{{ m.mode }}</td>
            <td class="r num">{{ number(m.sessions) }}</td>
            <td class="r num">{{ number(m.players) }}</td>
            <td class="r num">{{ m.sessions ? secText(m.avgDuration) : '—' }}</td>
            <td class="r num">{{ m.share }}%</td>
            <td class="muted">{{ when(m.last) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
