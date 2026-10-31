<script setup>
import { onMounted, ref } from 'vue'
import { supabase, rpc, MOCK } from './lib/supabase'
import LoginView from './views/LoginView.vue'
import DashboardShell from './views/DashboardShell.vue'

const me = ref(null)
const ready = ref(false)
const loginNotice = ref('')

// ต้องเป็น owner / admin เท่านั้น
async function resolveStaff() {
  try { me.value = await rpc('dashboard_me') } catch { me.value = null }
  if (!me.value) {
    await supabase.auth.signOut()
    loginNotice.value = 'บัญชีนี้ไม่มีสิทธิ์เข้าใช้ระบบผู้ดูแล บัญชีผู้ใช้ทั่วไปสามารถใช้งานได้ในแอปพลิเคชัน Breez เท่านั้น'
  }
  return me.value
}

async function signOut() {
  await supabase.auth.signOut()
  me.value = null
  loginNotice.value = ''
}

onMounted(async () => {
  if (MOCK) { await resolveStaff(); ready.value = true; return }
  const { data } = await supabase.auth.getSession()
  if (data.session) await resolveStaff()
  ready.value = true
  supabase.auth.onAuthStateChange(event => { if (event === 'SIGNED_OUT') me.value = null })
})
</script>

<template>
  <div v-if="!ready" class="boot">กำลังโหลดข้อมูล…</div>
  <LoginView v-else-if="!me" :notice="loginNotice" :resolve-staff="resolveStaff" @clear-notice="loginNotice = ''" />
  <DashboardShell v-else :me="me" @sign-out="signOut" />
</template>
