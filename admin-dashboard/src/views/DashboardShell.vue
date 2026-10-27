<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { initials, number } from '../lib/format'
import OverviewPage from '../pages/OverviewPage.vue'
import PlayersPage from '../pages/PlayersPage.vue'
import SessionsPage from '../pages/SessionsPage.vue'
import MoodsPage from '../pages/MoodsPage.vue'
import DevicesPage from '../pages/DevicesPage.vue'
import StaffPage from '../pages/StaffPage.vue'

const props = defineProps({ me: { type: Object, required: true } })
const emit = defineEmits(['sign-out'])

const isOwner = computed(() => props.me.role === 'owner')

const allNav = [
  { id: 'overview', label: 'ภาพรวม', icon: '◫', title: 'ภาพรวมระบบ', page: OverviewPage },
  { id: 'players', label: 'ผู้เล่น', icon: '♙', title: 'จัดการผู้เล่น', page: PlayersPage },
  { id: 'sessions', label: 'ประวัติการเล่น', icon: '▷', title: 'ประวัติการเล่น', page: SessionsPage },
  { id: 'moods', label: 'บันทึกอารมณ์', icon: '♡', title: 'บันทึกอารมณ์', page: MoodsPage, owner: true },
  { id: 'devices', label: 'อุปกรณ์', icon: '⌁', title: 'อุปกรณ์ Breath Gadget', page: DevicesPage },
  { id: 'staff', label: 'ผู้ดูแลระบบ', icon: '⚿', title: 'ผู้ดูแลระบบ', page: StaffPage, owner: true },
]
const navItems = computed(() => allNav.filter(n => !n.owner || isOwner.value))

function fromHash() {
  const id = location.hash.replace('#/', '').split('?')[0]
  return navItems.value.some(n => n.id === id) ? id : 'overview'
}
const active = ref(fromHash())
const search = ref('')
const playerFilter = ref(null) // { id, name } ใช้เมื่อกดดูประวัติจากหน้าผู้เล่น
const showNotifications = ref(false)
const showMenu = ref(false)
const summary = ref(null)

const current = computed(() => navItems.value.find(n => n.id === active.value) ?? navItems.value[0])

function go(id, opts = {}) {
  active.value = id
  search.value = ''
  playerFilter.value = opts.player ?? null
  showNotifications.value = false
  location.hash = `#/${id}`
}

const onHash = () => { const id = fromHash(); if (id !== active.value) go(id) }
onMounted(async () => {
  window.addEventListener('hashchange', onHash)
  try { summary.value = await rpc('dashboard_overview', { p_days: 7 }) } catch { /* แสดงหน้าได้ปกติ */ }
})
onBeforeUnmount(() => window.removeEventListener('hashchange', onHash))
watch(search, () => { if (search.value) playerFilter.value = null })

const notifications = computed(() => {
  const s = summary.value
  if (!s) return []
  const list = []
  if (s.devices_low_battery) list.push(`มีอุปกรณ์แบตเตอรี่ต่ำกว่า 20% จำนวน ${number(s.devices_low_battery)} เครื่อง`)
  if (s.players_new) list.push(`มีผู้เล่นใหม่ใน 7 วันล่าสุด ${number(s.players_new)} คน`)
  if (s.sessions) list.push(`มีการเล่น ${number(s.sessions)} ครั้งใน 7 วันล่าสุด`)
  return list
})
</script>

<template>
  <div class="app-shell">
    <aside class="sidebar">
      <div class="brand">
        <div class="brand-flower"><span>✿</span></div>
        <div><div class="brand-name">fuuu<span>.</span></div><div class="brand-sub">BREATHE & BLOOM</div></div>
      </div>
      <div class="workspace-label">WORKSPACE</div>
      <nav class="nav-list">
        <button v-for="item in navItems" :key="item.id" class="nav-item" :class="{ active: active === item.id }" @click="go(item.id)" :title="item.label">
          <span class="nav-icon">{{ item.icon }}</span><span>{{ item.label }}</span>
          <span v-if="item.id === 'devices' && summary?.devices_low_battery" class="nav-count">{{ summary.devices_low_battery }}</span>
        </button>
      </nav>
      <div class="sidebar-bottom">
        <div class="help-card">
          <div class="help-spark">✦</div>
          <strong>Keep growing!</strong>
          <p>ทุกลมหายใจคือการเริ่มต้นใหม่</p>
          <div class="help-flower">✿</div>
        </div>
        <div class="admin-profile">
          <button class="admin-avatar" @click="showMenu = !showMenu" aria-label="เมนูบัญชี">{{ initials(me.name) }}</button>
          <div class="admin-info"><strong>{{ me.name }}</strong><span>{{ isOwner ? 'เจ้าของระบบ' : 'ผู้ดูแล' }}</span></div>
          <button class="more more-button" @click="showMenu = !showMenu" aria-label="เมนูบัญชี">···</button>
          <div v-if="showMenu" class="account-pop">
            <small>{{ me.email }}</small>
            <button @click="emit('sign-out')">ออกจากระบบ</button>
          </div>
        </div>
      </div>
    </aside>

    <main class="main-area">
      <header class="topbar">
        <div class="breadcrumb"><span>fuuu Admin</span><span class="crumb-slash">/</span><strong>{{ current.title }}</strong></div>
        <div class="top-actions">
          <div v-if="current.id !== 'overview' && current.id !== 'staff'" class="search-box">
            <span>⌕</span><input v-model="search" placeholder="ค้นหาผู้เล่น, session..." aria-label="ค้นหา" />
          </div>
          <button class="icon-button notification-button" @click="showNotifications = !showNotifications" aria-label="การแจ้งเตือน">♧<i v-if="notifications.length"></i></button>
          <div v-if="showNotifications" class="notification-pop">
            <strong>การแจ้งเตือน</strong>
            <p v-for="n in notifications" :key="n">{{ n }}</p>
            <p v-if="!notifications.length">ยังไม่มีการแจ้งเตือน</p>
          </div>
          <span class="role-badge" :class="me.role">{{ isOwner ? 'OWNER' : 'ADMIN' }}</span>
          <button class="top-avatar" @click="showMenu = !showMenu" :title="me.email">{{ initials(me.name) }}</button>
        </div>
      </header>

      <div class="page-content">
        <component :is="current.page" :key="current.id" :me="me" :search="search" :player-filter="playerFilter"
          @navigate="(id, opts) => go(id, opts)" @clear-player="playerFilter = null" />
        <footer class="page-footer"><span>✿ fuuu admin dashboard</span><span>เชื่อมต่อ Supabase · ข้อมูลจริง</span></footer>
      </div>
    </main>
  </div>
</template>
