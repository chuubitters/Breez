<script setup>
import { ref } from 'vue'
import { supabase, translateError } from '../lib/supabase'
import BreezFlower from '../components/BreezFlower.vue'

const props = defineProps({ notice: String, resolveStaff: Function })
const emit = defineEmits(['clear-notice'])

const email = ref('')
const password = ref('')
const showPassword = ref(false)
const loading = ref(false)
const error = ref('')

async function submit() {
  error.value = ''
  emit('clear-notice')
  if (!email.value || !password.value) {
    error.value = 'กรอกอีเมลและรหัสผ่าน'
    return
  }
  loading.value = true
  try {
    const { error: err } = await supabase.auth.signInWithPassword({
      email: email.value.trim(),
      password: password.value,
    })
    if (err) throw err
    await props.resolveStaff()
  } catch (e) {
    error.value = translateError(e.message)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <div class="login-art" aria-hidden="true">
      <div class="login-hill"></div>
      <BreezFlower class="deco" style="left:-70px;top:22%" :size="420" :petals="4" color="#c9dcae" center="" :rotate="20" />
      <BreezFlower class="deco" style="left:-10px;top:32%" :size="300" :petals="4" color="#f4bfd1" center="" :rotate="20" />
      <BreezFlower class="deco" style="right:6%;top:24%" :size="340" color="#f2b6ca" />
      <BreezFlower class="deco" style="right:8%;bottom:70px" :size="120" :petals="5" color="#c9c0f0" center="#ffffff" />
      <BreezFlower class="deco" style="left:14%;bottom:40px" :size="110" :petals="5" color="#c9c0f0" center="#ffffff" />
      <span class="orb orb-butter"></span>
      <span class="orb orb-pink"></span>
    </div>
    <form class="login-card" @submit.prevent="submit" novalidate>
      <div class="login-brand">
        <img class="brand-logo" src="/breez-logo.png" alt="breez" />
        <span class="brand-sub">ADMIN CONSOLE</span>
      </div>
      <h1>เข้าสู่ระบบ<span class="highlight">หลังบ้าน</span></h1>
      <p class="page-description">สำหรับเจ้าของระบบและผู้ดูแลเท่านั้น</p>

      <label class="field">
        <span>อีเมล</span>
        <input v-model="email" type="email" autocomplete="username" placeholder="admin@example.com" :disabled="loading" />
      </label>
      <label class="field">
        <span>รหัสผ่าน</span>
        <div class="password-wrap">
          <input v-model="password" :type="showPassword ? 'text' : 'password'" autocomplete="current-password" placeholder="••••••••" :disabled="loading" />
          <button type="button" class="ghost-toggle" @click="showPassword = !showPassword">{{ showPassword ? 'ซ่อน' : 'แสดง' }}</button>
        </div>
      </label>

      <p v-if="error || notice" class="form-error" role="alert">{{ error || notice }}</p>

      <button class="primary-button" type="submit" :disabled="loading">
        {{ loading ? 'กำลังเข้าสู่ระบบ…' : 'เข้าสู่ระบบ' }}
      </button>
      <p class="login-foot">ลืมรหัสผ่าน? ติดต่อเจ้าของระบบเพื่อรีเซ็ต</p>
    </form>
  </div>
</template>
