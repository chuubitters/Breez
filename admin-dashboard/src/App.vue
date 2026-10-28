<script setup>
import { onMounted, ref } from 'vue'
import { supabase, rpc } from './lib/supabase'
import LoginView from './views/LoginView.vue'
import DashboardShell from './views/DashboardShell.vue'
import BreezFlower from './components/BreezFlower.vue'

const me = ref(null)
const ready = ref(false)
const loginNotice = ref('')

// ตรวจว่าบัญชีที่ login เป็น owner/admin หรือไม่
async function resolveStaff() {
  try {
    me.value = await rpc('dashboard_me')
  } catch {
    me.value = null
  }
  if (!me.value) {
    await supabase.auth.signOut()
    loginNotice.value = 'บัญชีนี้ไม่มีสิทธิ์เข้าหลังบ้าน (ผู้เล่นทั่วไปใช้ได้เฉพาะในเกม)'
  }
  return me.value
}

async function signOut() {
  await supabase.auth.signOut()
  me.value = null
  loginNotice.value = ''
}

onMounted(async () => {
  const { data } = await supabase.auth.getSession()
  if (data.session) await resolveStaff()
  ready.value = true
  supabase.auth.onAuthStateChange((event) => {
    if (event === 'SIGNED_OUT') me.value = null
  })
})
</script>

<template>
  <div v-if="!ready" class="boot-screen"><BreezFlower class="spin" :size="56" :petals="5" /></div>
  <LoginView v-else-if="!me" :notice="loginNotice" :resolve-staff="resolveStaff" @clear-notice="loginNotice = ''" />
  <DashboardShell v-else :me="me" @sign-out="signOut" />
</template>
