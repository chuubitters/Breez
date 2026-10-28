<script setup>
import { ref } from 'vue'
import { rpc, supabase } from '../lib/supabase'
import { avatarColor, initials, when } from '../lib/format'

const props = defineProps({ me: Object })

const staff = ref([])
const loading = ref(true)
const error = ref('')
const msg = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try { staff.value = await rpc('staff_list') ?? [] } catch (e) { error.value = e.message } finally { loading.value = false }
}
load()

const form = ref({ name: '', email: '', password: '', role: 'admin' })
const creating = ref(false)
async function createStaff() {
  msg.value = ''; error.value = ''
  creating.value = true
  try {
    const { data, error: err } = await supabase.functions.invoke('create-staff', { body: form.value })
    if (err) {
      let detail = err.message
      try { detail = (await err.context.json()).error || detail } catch { /* ใช้ข้อความเดิม */ }
      throw new Error(detail)
    }
    msg.value = `สร้างบัญชี ${data.email} (${data.role}) แล้ว ส่งอีเมลและรหัสผ่านให้ผู้ดูแลได้เลย`
    form.value = { name: '', email: '', password: '', role: 'admin' }
    await load()
  } catch (e) { error.value = e.message } finally { creating.value = false }
}

const grantEmail = ref('')
const grantRole = ref('admin')
async function grant() {
  msg.value = ''; error.value = ''
  try {
    await rpc('staff_set_role', { p_email: grantEmail.value, p_role: grantRole.value })
    msg.value = `ให้สิทธิ์ ${grantRole.value} กับ ${grantEmail.value} แล้ว`
    grantEmail.value = ''
    await load()
  } catch (e) { error.value = e.message }
}

async function changeRole(s, role) {
  msg.value = ''; error.value = ''
  try { await rpc('staff_set_role', { p_email: s.email, p_role: role }); await load() } catch (e) { error.value = e.message }
}
async function remove(s) {
  if (!window.confirm(`ถอนสิทธิ์หลังบ้านของ ${s.email}?\n(บัญชียังอยู่ แต่จะเข้าหลังบ้านไม่ได้)`)) return
  msg.value = ''; error.value = ''
  try { await rpc('staff_remove', { p_user_id: s.user_id }); await load() } catch (e) { error.value = e.message }
}

const matrix = [
  ['ภาพรวม / กราฟ', true, true],
  ['ภาพรวมผู้เล่น / อารมณ์ (ไม่ระบุตัวตน)', true, true],
  ['ประวัติการเล่น + ส่งออก CSV', true, true],
  ['อุปกรณ์', true, true],
  ['บันทึกอารมณ์รายบุคคล', true, 'เห็นเฉพาะสัดส่วนรวม'],
  ['จัดการผู้ดูแล', true, false],
]
</script>

<template>
  <section class="welcome-row section-welcome">
    <div><div class="eyebrow"><span class="eyebrow-dot"></span> ACCESS CONTROL · OWNER ONLY</div><h1>ผู้ดูแล<span class="highlight">ระบบ</span></h1><p class="page-description">เพิ่มผู้ดูแล กำหนดสิทธิ์ Owner / Admin และถอนสิทธิ์</p></div>
  </section>

  <p v-if="msg" class="banner-ok">{{ msg }}</p>
  <p v-if="error" class="banner-error">{{ error }}</p>

  <section class="lower-grid staff-grid">
    <article class="panel">
      <div class="panel-heading"><div><h2>ผู้ดูแลทั้งหมด</h2><p>{{ staff.length }} บัญชี</p></div></div>
      <div class="table-wrap" :class="{ loading }">
        <table>
          <thead><tr><th>ผู้ดูแล</th><th>สิทธิ์</th><th>เข้าใช้ล่าสุด</th><th></th></tr></thead>
          <tbody>
            <tr v-for="s in staff" :key="s.user_id">
              <td><div class="player-cell"><div class="tiny-avatar" :class="avatarColor(s.user_id)">{{ initials(s.name) }}</div><div><strong>{{ s.name }}<span v-if="s.user_id === me.user_id" class="you"> (คุณ)</span></strong><small>{{ s.email }}</small></div></div></td>
              <td>
                <select class="filter-select" :value="s.role" :disabled="s.user_id === me.user_id" @change="changeRole(s, $event.target.value)">
                  <option value="owner">Owner</option><option value="admin">Admin</option>
                </select>
              </td>
              <td>{{ when(s.last_sign_in_at) }}</td>
              <td><button v-if="s.user_id !== me.user_id" class="text-button danger" @click="remove(s)">ถอนสิทธิ์</button></td>
            </tr>
          </tbody>
        </table>
      </div>
    </article>

    <article class="panel">
      <div class="panel-heading"><div><h2>สิทธิ์ของแต่ละระดับ</h2><p>บังคับใช้ที่ฐานข้อมูล ไม่ใช่แค่ซ่อนปุ่ม</p></div></div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>ส่วน</th><th>Owner</th><th>Admin</th></tr></thead>
          <tbody>
            <tr v-for="r in matrix" :key="r[0]">
              <td><strong>{{ r[0] }}</strong></td>
              <td v-for="(v, i) in [r[1], r[2]]" :key="i">
                <span v-if="v === true" class="yes">✓</span><span v-else-if="v === false" class="no">✕</span><span v-else class="muted">{{ v }}</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </article>
  </section>

  <section class="lower-grid staff-grid">
    <form class="panel stack-form" @submit.prevent="createStaff">
      <div class="panel-heading"><div><h2>สร้างบัญชีผู้ดูแลใหม่</h2><p>ใช้เมื่อคนนั้นยังไม่มีบัญชีในระบบ</p></div></div>
      <label class="field"><span>ชื่อที่แสดง</span><input v-model="form.name" placeholder="เช่น Mint" /></label>
      <label class="field"><span>อีเมล</span><input v-model="form.email" type="email" required placeholder="admin@example.com" /></label>
      <label class="field"><span>รหัสผ่านเริ่มต้น (อย่างน้อย 8 ตัว)</span><input v-model="form.password" type="text" minlength="8" required autocomplete="new-password" /></label>
      <label class="field"><span>สิทธิ์</span><select v-model="form.role" class="filter-select"><option value="admin">Admin</option><option value="owner">Owner</option></select></label>
      <button class="primary-button" :disabled="creating">{{ creating ? 'กำลังสร้าง…' : 'สร้างบัญชี' }}</button>
    </form>

    <form class="panel stack-form" @submit.prevent="grant">
      <div class="panel-heading"><div><h2>ให้สิทธิ์บัญชีที่มีอยู่</h2><p>เช่น ผู้เล่นที่สมัครผ่านเกมแล้ว และต้องการให้เป็นผู้ดูแลด้วย</p></div></div>
      <label class="field"><span>อีเมลของบัญชี</span><input v-model="grantEmail" type="email" required placeholder="user@example.com" /></label>
      <label class="field"><span>สิทธิ์</span><select v-model="grantRole" class="filter-select"><option value="admin">Admin</option><option value="owner">Owner</option></select></label>
      <button class="secondary-button">ให้สิทธิ์</button>
      <p class="page-description">หมายเหตุ: บัญชีที่เป็นผู้ดูแลจะไม่ถูกนับเป็นผู้เล่นในสถิติ</p>
    </form>
  </section>
</template>
