<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { rpc } from '../lib/supabase'
import { clearSessionCache } from '../lib/stats'
import OverviewPage from '../pages/OverviewPage.vue'
import ModesPage from '../pages/ModesPage.vue'
import SessionsPage from '../pages/SessionsPage.vue'
import MoodsPage from '../pages/MoodsPage.vue'
import DevicesPage from '../pages/DevicesPage.vue'
import StaffPage from '../pages/StaffPage.vue'

const props = defineProps({ me: { type: Object, required: true } })
const emit = defineEmits(['sign-out'])
const isOwner = computed(() => props.me.role === 'owner')

// เมนู: ข้อมูลการเล่น (ใช้ปรับเกม) → ผู้ใช้ → ระบบ
const ICON = {
  overview: 'M4 13h6V4H4zM14 20h6V11h-6zM4 20h6v-3H4zM14 7h6V4h-6z',
  modes: 'M6 12h4M8 10v4M15 11h.01M18 13h.01M7 6h10a5 5 0 0 1 5 5v2a5 5 0 0 1-5 5H7a5 5 0 0 1-5-5v-2a5 5 0 0 1 5-5z',
  sessions: 'M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01',
  moods: 'M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1-1.1a5.5 5.5 0 0 0-7.8 7.8l1 1.1L12 21l7.8-7.5 1-1.1a5.5 5.5 0 0 0 0-7.8z',
  devices: 'M7 2h10a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2zM12 18h.01',
  staff: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z',
}
const allNav = [
  { group: 'ข้อมูลการฝึก' },
  { id: 'overview', label: 'ภาพรวม', desc: 'สรุปข้อมูลสำคัญและประเด็นที่ควรตรวจสอบในช่วงเวลาที่เลือก', page: OverviewPage, period: true },
  { id: 'modes', label: 'ข้อมูลแต่ละโหมด', desc: 'ตัวชี้วัดของแต่ละเกม สำหรับใช้ปรับระดับความยากให้เหมาะสม', page: ModesPage, period: true },
  { id: 'sessions', label: 'ประวัติการเล่น', desc: 'ผลการเล่นทั้งหมด พร้อมค้นหา กรองข้อมูล และส่งออกไฟล์ CSV', page: SessionsPage, period: true },
  { id: 'moods', label: 'ข้อมูลอารมณ์', desc: 'จำนวนผู้ใช้แยกตามอารมณ์และภาพรวมความเครียด', page: MoodsPage, period: true, owner: true },
  { group: 'ระบบ' },
  { id: 'devices', label: 'อุปกรณ์', desc: 'สถานะการเชื่อมต่อและแบตเตอรี่ของ Breath Gadget', page: DevicesPage },
  { id: 'staff', label: 'จัดการแอดมิน', desc: 'เพิ่มแอดมินและกำหนดสิทธิ์การใช้งาน', page: StaffPage, owner: true },
]
const nav = computed(() => allNav.filter(n => !n.owner || isOwner.value))
const pages = computed(() => nav.value.filter(n => n.id))

function fromHash() {
  const id = location.hash.replace('#/', '').split('?')[0]
  return pages.value.some(n => n.id === id) ? id : 'overview'
}
const active = ref(fromHash())
const current = computed(() => pages.value.find(n => n.id === active.value) ?? pages.value[0])
const playerFilter = ref(null)   // { id, name } เมื่อกดดูประวัติของผู้เล่น
const modeFilter = ref('')

// ช่วงเวลา ใช้ร่วมกันทุกหน้า (จำไว้ในเครื่อง)
const PERIODS = [{ v: 7, t: '7 วัน' }, { v: 30, t: '30 วัน' }, { v: 90, t: '90 วัน' }, { v: null, t: 'ทั้งหมด' }]
const saved = (() => { try { return JSON.parse(localStorage.getItem('breez-admin-period')) } catch { return undefined } })()
const days = ref(saved === undefined ? 30 : saved)
function setDays(v) { days.value = v; try { localStorage.setItem('breez-admin-period', JSON.stringify(v)) } catch { /* ไม่เป็นไร */ } }

const reloadKey = ref(0)
function refresh() { clearSessionCache(); reloadKey.value++ }

function go(id, opts = {}) {
  active.value = id
  playerFilter.value = opts.player ?? null
  modeFilter.value = opts.mode ?? ''
  location.hash = `#/${id}`
}
const onHash = () => { const id = fromHash(); if (id !== active.value) go(id) }

const lowBattery = ref(0)
onMounted(async () => {
  window.addEventListener('hashchange', onHash)
  try { lowBattery.value = (await rpc('dashboard_devices', { p_search: null }) ?? []).filter(d => d.battery_level != null && d.battery_level < 20).length } catch { /* ไม่แสดงตัวเลข */ }
})
onBeforeUnmount(() => window.removeEventListener('hashchange', onHash))
</script>

<template>
  <div class="app">
    <aside class="sidebar">
      <div class="brand"><img src="/breez-logo.png" alt="breez" /><span>Admin</span></div>
      <nav class="nav">
        <template v-for="n in nav" :key="n.id || n.group">
          <div v-if="n.group" class="nav-group">{{ n.group }}</div>
          <button v-else class="nav-item" :class="{ active: active === n.id }" @click="go(n.id)">
            <svg viewBox="0 0 24 24"><path :d="ICON[n.id]" /></svg>
            {{ n.label }}
            <span v-if="n.id === 'devices' && lowBattery" class="nav-badge" title="แบตเตอรี่ต่ำกว่า 20%">{{ lowBattery }}</span>
          </button>
        </template>
      </nav>
      <div class="side-foot">
        <div class="row"><strong>{{ me.name }}</strong><span class="role">{{ me.role === 'owner' ? 'Super Admin' : 'Admin' }}</span></div>
        <span class="muted small">{{ me.email }}</span>
        <button class="btn-link small" style="justify-self:start;margin-top:6px" @click="emit('sign-out')">ออกจากระบบ</button>
      </div>
    </aside>

    <main class="main">
      <header class="topbar">
        <div>
          <h1>{{ current.label }}</h1>
          <p class="desc">{{ current.desc }}</p>
        </div>
        <div class="actions">
          <div v-if="current.period" class="seg" role="group" aria-label="ช่วงเวลา">
            <button v-for="p in PERIODS" :key="String(p.v)" :class="{ on: days === p.v }" @click="setDays(p.v)">{{ p.t }}</button>
          </div>
          <button class="btn" title="โหลดข้อมูลล่าสุด" @click="refresh">อัปเดตข้อมูล</button>
        </div>
      </header>
      <div class="content">
        <component :is="current.page" :key="current.id + reloadKey" :me="me" :days="days" :player-filter="playerFilter" :mode-filter="modeFilter"
          @navigate="(id, opts) => go(id, opts)" @clear-player="playerFilter = null" />
      </div>
    </main>
  </div>
</template>
