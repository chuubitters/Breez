<script setup>
// อุปกรณ์: เครื่องไหนออนไลน์ แบตใกล้หมด หรือเงียบไปนาน
import { computed, ref } from 'vue'
import { rpc } from '../lib/supabase'
import { number, when } from '../lib/format'

const devices = ref([])
const loading = ref(true)
const error = ref('')
const filter = ref('all')
const search = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try { devices.value = await rpc('dashboard_devices', { p_search: null }) ?? [] }
  catch (e) { error.value = e.message }
  finally { loading.value = false }
}
load()

const low = d => d.battery_level != null && d.battery_level < 20
const stale = d => !d.is_online && Date.now() - new Date(d.updated_at).getTime() > 14 * 864e5
const counts = computed(() => ({
  all: devices.value.length,
  online: devices.value.filter(d => d.is_online).length,
  low: devices.value.filter(low).length,
  stale: devices.value.filter(stale).length,
}))
const shown = computed(() => devices.value
  .filter(d => filter.value === 'all' || (filter.value === 'online' && d.is_online) || (filter.value === 'low' && low(d)) || (filter.value === 'stale' && stale(d)))
  .filter(d => !search.value || `${d.device_name} ${d.owner_name}`.toLowerCase().includes(search.value.toLowerCase())))
const iconColor = d => { const c = String(d.device_icon || '').split('|')[1]; return /^#[0-9a-f]{6}$/i.test(c || '') ? c : '#9aa39b' }
</script>

<template>
  <p v-if="error" class="banner err">{{ error }}</p>
  <section class="card">
    <div class="toolbar" style="margin-bottom:12px">
      <div class="seg">
        <button :class="{ on: filter === 'all' }" @click="filter = 'all'">ทั้งหมด {{ counts.all }}</button>
        <button :class="{ on: filter === 'online' }" @click="filter = 'online'">ออนไลน์ {{ counts.online }}</button>
        <button :class="{ on: filter === 'low' }" @click="filter = 'low'">แบตเตอรี่ต่ำ {{ counts.low }}</button>
        <button :class="{ on: filter === 'stale' }" @click="filter = 'stale'">ไม่ได้ใช้งานเกิน 14 วัน {{ counts.stale }}</button>
      </div>
      <input v-model="search" class="input search" placeholder="ค้นหาชื่ออุปกรณ์หรือชื่อเจ้าของ" />
    </div>
    <div class="table-wrap" :class="{ loading }">
      <table>
        <thead><tr><th>อุปกรณ์</th><th>เจ้าของ</th><th>สถานะ</th><th class="r">แบตเตอรี่</th><th class="r">จำนวนครั้งที่ใช้</th><th>อัปเดตล่าสุด</th></tr></thead>
        <tbody>
          <tr v-for="d in shown" :key="d.device_id">
            <td><i class="dot" :style="{ background: iconColor(d) }"></i>{{ d.device_name || 'Breath Gadget' }}</td>
            <td>{{ d.owner_name }}</td>
            <td><span class="status" :class="d.is_online ? 'on' : ''">{{ d.is_online ? 'ออนไลน์' : 'ออฟไลน์' }}</span></td>
            <td class="r num"><span :class="{ 'status bad': low(d) }">{{ d.battery_level != null ? d.battery_level + '%' : '—' }}</span></td>
            <td class="r num">{{ number(d.sessions) }}</td>
            <td class="muted">{{ when(d.updated_at) }}</td>
          </tr>
          <tr v-if="!loading && !shown.length"><td colspan="6" class="empty">ไม่พบอุปกรณ์ในกลุ่มนี้</td></tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
