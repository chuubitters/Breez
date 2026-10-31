<script setup>
// แต่ละโหมด: ตัวชี้วัดที่ใช้ปรับความยาก/สมดุลของเกม
import { computed, ref, watch } from 'vue'
import { loadSessions, modeDetail, secText, MODES } from '../lib/stats'
import { MODE_COLOR, MODE_INFO, number } from '../lib/format'
import BarList from '../components/BarList.vue'

const props = defineProps({ days: Number, modeFilter: String })
const emit = defineEmits(['navigate'])

const tab = ref(MODES.includes(props.modeFilter) ? props.modeFilter : MODES[0])
const data = ref({ cur: [] })
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

const counts = computed(() => Object.fromEntries(MODES.map(m => [m, data.value.cur.filter(r => r.mode === m).length])))
const d = computed(() => modeDetail(tab.value, data.value.cur))
</script>

<template>
  <p v-if="error" class="banner err">{{ error }}</p>

  <nav class="tabs">
    <button v-for="m in MODES" :key="m" :class="{ on: tab === m }" @click="tab = m">
      <i class="dot" :style="{ background: MODE_COLOR[m] }"></i>{{ m }}<span class="count">{{ counts[m] }}</span>
    </button>
  </nav>

  <section class="kpis" :class="{ loading }">
    <div class="kpi"><div class="label">จำนวนครั้ง</div><div class="value">{{ number(d.sessions) }}</div><div class="foot">{{ MODE_INFO[tab] }}</div></div>
    <div class="kpi"><div class="label">ผู้เล่น</div><div class="value">{{ number(d.players) }}<small>คน</small></div><div class="foot">{{ d.players ? (d.sessions / d.players).toFixed(1) : 0 }} ครั้ง/คน</div></div>
    <div class="kpi"><div class="label">เวลาต่อครั้ง (มัธยฐาน)</div><div class="value">{{ d.sessions ? secText(d.medianDuration) : '—' }}</div><div class="foot">เฉลี่ย {{ d.sessions ? secText(d.avgDuration) : '—' }}</div></div>
    <div class="kpi"><div class="label">ประวัติ</div><div class="value" style="font-size:15px;padding-top:8px"><button class="btn-link" @click="emit('navigate', 'sessions', { mode: tab })">ดูผลการเล่นทุกครั้ง →</button></div></div>
  </section>

  <template v-if="d.sessions">
    <section class="card" :class="{ loading }">
      <div class="card-head"><h2>ตัวชี้วัดของเกม</h2><p>ใช้ดูว่าเกมยาก/ง่ายเกินไปหรือไม่</p></div>
      <div class="metrics">
        <div v-for="m in d.metrics" :key="m.label" class="metric">
          <div class="label">{{ m.label }}</div>
          <div class="value">{{ m.value }}</div>
          <div v-if="m.hint" class="hint">{{ m.hint }}</div>
        </div>
      </div>
    </section>

    <section class="grid-2" :class="{ loading }">
      <article v-for="c in d.charts" :key="c.title" class="card">
        <div class="card-head"><h2>{{ c.title }}</h2><p>จำนวนครั้ง</p></div>
        <BarList :data="c.data.map(x => ({ ...x, color: MODE_COLOR[tab] }))" />
        <p v-if="c.note" class="muted small" style="margin-top:12px">{{ c.note }}</p>
      </article>
    </section>
  </template>
  <section v-else class="card"><p class="empty">ยังไม่มีการเล่น {{ tab }} ในช่วงเวลานี้</p></section>
</template>
