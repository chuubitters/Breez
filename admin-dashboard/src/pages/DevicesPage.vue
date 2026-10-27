<script setup>
import { computed, ref, watch } from 'vue'
import { rpc } from '../lib/supabase'
import { number, shortId, when } from '../lib/format'

const props = defineProps({ me: Object, search: String })
const devices = ref([])
const loading = ref(true)
const error = ref('')
const filter = ref('all')

const ICONS = { cat: '🐱', flower: '✿', cloud: '☁', star: '★', heart: '♥', moon: '☾' }

let timer
async function load() {
  loading.value = true
  error.value = ''
  try {
    devices.value = await rpc('dashboard_devices', { p_search: props.search || null }) ?? []
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}
watch(() => props.search, () => { clearTimeout(timer); timer = setTimeout(load, 300) })
load()

const stats = computed(() => ({
  total: devices.value.length,
  online: devices.value.filter(d => d.is_online).length,
  low: devices.value.filter(d => d.battery_level != null && d.battery_level < 20).length,
}))
const shown = computed(() => devices.value.filter(d =>
  filter.value === 'all' || (filter.value === 'online' ? d.is_online : filter.value === 'offline' ? !d.is_online : d.battery_level != null && d.battery_level < 20)))
</script>

<template>
  <section class="welcome-row section-welcome">
    <div><div class="eyebrow"><span class="eyebrow-dot"></span> DEVICE MANAGEMENT</div><h1>อุปกรณ์<span class="highlight">Breath Gadget</span></h1><p class="page-description">สถานะการเชื่อมต่อและแบตเตอรี่ (ออนไลน์ = เกมส่งสถานะมาภายใน 5 นาที)</p></div>
    <div class="header-actions">
      <select class="filter-select" v-model="filter"><option value="all">ทั้งหมด</option><option value="online">ออนไลน์</option><option value="offline">ออฟไลน์</option><option value="low">แบตต่ำ</option></select>
      <button class="secondary-button" @click="load">↻ รีเฟรช</button>
    </div>
  </section>

  <section class="stats-grid compact-stats">
    <article class="stat-card stat-lilac"><div class="stat-label">อุปกรณ์ทั้งหมด</div><div class="stat-value">{{ number(stats.total) }}</div><div class="stat-foot">อุปกรณ์ที่ลงทะเบียน</div></article>
    <article class="stat-card stat-green"><div class="stat-label">ออนไลน์</div><div class="stat-value">{{ number(stats.online) }}</div><div class="stat-foot"><span class="online-dot"></span> เชื่อมต่ออยู่</div></article>
    <article class="stat-card stat-peach"><div class="stat-label">ออฟไลน์</div><div class="stat-value">{{ number(stats.total - stats.online) }}</div><div class="stat-foot">ไม่ได้เชื่อมต่อในขณะนี้</div></article>
    <article class="stat-card stat-yellow"><div class="stat-label">แบตเตอรี่ต่ำ</div><div class="stat-value">{{ number(stats.low) }}</div><div class="stat-foot">ต่ำกว่า 20%</div></article>
  </section>

  <p v-if="error" class="banner-error">{{ error }}</p>
  <section class="device-grid" :class="{ loading }">
    <article v-for="d in shown" :key="d.device_id" class="device-card">
      <div class="device-card-top">
        <div class="device-illustration">{{ ICONS[d.device_icon] ?? '⌁' }}</div>
        <span class="status-label" :class="d.is_online ? 'active' : 'inactive'"><i></i>{{ d.is_online ? 'ออนไลน์' : 'ออฟไลน์' }}</span>
      </div>
      <h3>{{ d.device_name }}</h3>
      <p class="device-id">{{ shortId(d.device_id) }} · {{ d.owner_name }} · {{ number(d.sessions) }} sessions</p>
      <div class="battery-row"><span>แบตเตอรี่</span><strong :class="{ 'battery-low': d.battery_level != null && d.battery_level < 20 }">{{ d.battery_level != null ? d.battery_level + '%' : 'ไม่ทราบ' }}</strong></div>
      <div class="battery-track"><span :style="{ width: (d.battery_level ?? 0) + '%' }" :class="{ low: d.battery_level != null && d.battery_level < 20 }"></span></div>
      <div class="device-updated">อัปเดตล่าสุด <strong>{{ when(d.updated_at) }}</strong></div>
    </article>
    <p v-if="!loading && !shown.length" class="empty-state panel">ยังไม่มีอุปกรณ์{{ filter !== 'all' ? 'ในกลุ่มนี้' : '' }}</p>
  </section>
</template>
