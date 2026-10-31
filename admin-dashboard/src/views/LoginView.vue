<script setup>
import { ref } from 'vue'
import { supabase, translateError } from '../lib/supabase'

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
  <div class="login">
    <form class="login-card" @submit.prevent="submit" novalidate>
      <img src="/breez-logo.png" alt="breez" />
      <div>
        <h1>เข้าสู่ระบบหลังบ้าน</h1>
        <p class="muted small">สำหรับเจ้าของระบบและผู้ดูแลเท่านั้น</p>
      </div>
      <label class="field">
        <span>อีเมล</span>
        <input v-model="email" class="input" type="email" autocomplete="username" :disabled="loading" />
      </label>
      <label class="field">
        <span>รหัสผ่าน</span>
        <div class="pw">
          <input v-model="password" class="input" :type="showPassword ? 'text' : 'password'" autocomplete="current-password" :disabled="loading" />
          <button type="button" class="btn" @click="showPassword = !showPassword">{{ showPassword ? 'ซ่อน' : 'แสดง' }}</button>
        </div>
      </label>
      <p v-if="error || notice" class="banner err" role="alert">{{ error || notice }}</p>
      <button class="btn btn-primary" style="justify-content:center" type="submit" :disabled="loading">
        {{ loading ? 'กำลังเข้าสู่ระบบ…' : 'เข้าสู่ระบบ' }}
      </button>
      <p class="muted small">ลืมรหัสผ่าน? ติดต่อเจ้าของระบบเพื่อรีเซ็ต</p>
    </form>
  </div>
</template>
