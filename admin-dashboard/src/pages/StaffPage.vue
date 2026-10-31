<script setup>
import { ref } from 'vue'
import { rpc, supabase } from '../lib/supabase'
import { when } from '../lib/format'

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
  ['ภาพรวม / แต่ละโหมด / ประวัติการเล่น', true, true],
  ['ผู้เล่น (อีเมลถูกปิดบางส่วนสำหรับ Admin)', true, true],
  ['อุปกรณ์', true, true],
  ['อารมณ์รายบุคคล', true, false],
  ['จัดการผู้ดูแล', true, false],
]
</script>

<template>
  <p v-if="msg" class="banner ok">{{ msg }}</p>
  <p v-if="error" class="banner err">{{ error }}</p>

  <section class="card">
    <div class="card-head"><h2>ผู้ดูแลทั้งหมด</h2><p>{{ staff.length }} บัญชี · บัญชีผู้ดูแลไม่ถูกนับเป็นผู้เล่น</p></div>
    <div class="table-wrap" :class="{ loading }">
      <table>
        <thead><tr><th>ผู้ดูแล</th><th>สิทธิ์</th><th>เข้าใช้ล่าสุด</th><th></th></tr></thead>
        <tbody>
          <tr v-for="s in staff" :key="s.user_id">
            <td><strong>{{ s.name }}</strong><span v-if="s.user_id === me.user_id" class="muted"> (คุณ)</span><span class="sub">{{ s.email }}</span></td>
            <td>
              <select class="select" :value="s.role" :disabled="s.user_id === me.user_id" @change="changeRole(s, $event.target.value)">
                <option value="owner">Owner</option><option value="admin">Admin</option>
              </select>
            </td>
            <td class="muted">{{ when(s.last_sign_in_at) }}</td>
            <td class="r"><button v-if="s.user_id !== me.user_id" class="btn-link danger" @click="remove(s)">ถอนสิทธิ์</button></td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>

  <section class="grid-2">
    <form class="card form" style="max-width:none" @submit.prevent="createStaff">
      <div class="card-head" style="margin:0"><h2>สร้างบัญชีผู้ดูแลใหม่</h2><p>สำหรับคนที่ยังไม่มีบัญชี</p></div>
      <label class="field"><span>ชื่อที่แสดง</span><input v-model="form.name" class="input" /></label>
      <label class="field"><span>อีเมล</span><input v-model="form.email" class="input" type="email" required /></label>
      <label class="field"><span>รหัสผ่านเริ่มต้น (อย่างน้อย 8 ตัว)</span><input v-model="form.password" class="input" type="text" minlength="8" required autocomplete="new-password" /></label>
      <label class="field"><span>สิทธิ์</span><select v-model="form.role" class="select"><option value="admin">Admin</option><option value="owner">Owner</option></select></label>
      <button class="btn btn-primary" style="justify-self:start" :disabled="creating">{{ creating ? 'กำลังสร้าง…' : 'สร้างบัญชี' }}</button>
    </form>

    <form class="card form" style="max-width:none" @submit.prevent="grant">
      <div class="card-head" style="margin:0"><h2>ให้สิทธิ์บัญชีที่มีอยู่</h2><p>เช่น ผู้เล่นที่สมัครผ่านเกมแล้ว</p></div>
      <label class="field"><span>อีเมลของบัญชี</span><input v-model="grantEmail" class="input" type="email" required /></label>
      <label class="field"><span>สิทธิ์</span><select v-model="grantRole" class="select"><option value="admin">Admin</option><option value="owner">Owner</option></select></label>
      <button class="btn" style="justify-self:start">ให้สิทธิ์</button>
      <div>
        <p class="small" style="font-weight:600;margin:8px 0 4px">สิทธิ์แต่ละระดับ</p>
        <table>
          <thead><tr><th>ส่วน</th><th>Owner</th><th>Admin</th></tr></thead>
          <tbody>
            <tr v-for="r in matrix" :key="r[0]">
              <td class="small">{{ r[0] }}</td>
              <td v-for="(v, i) in [r[1], r[2]]" :key="i" class="small">{{ v === true ? 'ได้' : v === false ? 'ไม่ได้' : v }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </form>
  </section>
</template>
