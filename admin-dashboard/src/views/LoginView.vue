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
  <div class="login-page">
    <div class="login-art" aria-hidden="true">
      <span class="blob blob-1"></span><span class="blob blob-2"></span><span class="blob blob-3"></span>
    </div>
    <form class="login-card" @submit.prevent="submit" novalidate>
      <div class="brand login-brand">
        <div class="brand-flower"><span>✿</span></div>
        <div><div class="brand-name">fuuu<span>.</span></div><div class="brand-sub">BREATHE & BLOOM</div></div>
      </div>
      <div class="eyebrow"><span class="eyebrow-dot"></span> ADMIN CONSOLE</div>
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
