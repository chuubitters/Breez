<script setup>
import { computed, ref } from 'vue'
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

const ROLES = [
  { v: 'admin', t: 'Admin', d: 'ดูข้อมูลการฝึก แต่ละโหมด ประวัติการเล่น และอุปกรณ์' },
  { v: 'owner', t: 'Super Admin', d: 'ทำได้ทุกอย่าง รวมถึงดูข้อมูลอารมณ์และเพิ่ม/ลบแอดมิน' },
]
const roleName = r => (r === 'owner' ? 'Super Admin' : 'Admin')
const addMode = ref('new') // new = สร้างบัญชีใหม่, existing = ให้สิทธิ์บัญชีที่มีอยู่
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
    msg.value = `เพิ่ม ${data.email} เป็น ${roleName(data.role)} แล้ว ส่งอีเมลและรหัสผ่านให้เจ้าตัวได้เลย`
    form.value = { name: '', email: '', password: '', role: 'admin' }
    await load()
  } catch (e) { error.value = e.message } finally { creating.value = false }
}

async function grant() {
  msg.value = ''; error.value = ''
  creating.value = true
  try {
    await rpc('staff_set_role', { p_email: form.value.email, p_role: form.value.role })
    msg.value = `ให้สิทธิ์ ${roleName(form.value.role)} กับ ${form.value.email} แล้ว`
    form.value = { name: '', email: '', password: '', role: 'admin' }
    await load()
  } catch (e) { error.value = e.message } finally { creating.value = false }
}
const submit = () => (addMode.value === 'new' ? createStaff() : grant())

async function changeRole(s, role) {
  msg.value = ''; error.value = ''
  try { await rpc('staff_set_role', { p_email: s.email, p_role: role }); await load() } catch (e) { error.value = e.message }
}
async function remove(s) {
  if (!window.confirm(`ถอนสิทธิ์แอดมินของ ${s.email}?\n(บัญชียังอยู่ แต่จะเข้าแดชบอร์ดไม่ได้)`)) return
  msg.value = ''; error.value = ''
  try { await rpc('staff_remove', { p_user_id: s.user_id }); await load() } catch (e) { error.value = e.message }
}

const matrix = [
  ['ภาพรวม / แต่ละโหมด / ประวัติการเล่น', true, true],
  ['อุปกรณ์', true, true],
  ['อารมณ์ (รายคนและภาพรวม)', true, false],
  ['เพิ่ม / เปลี่ยนสิทธิ์ / ถอนสิทธิ์แอดมิน', true, false],
]
const supers = computed(() => staff.value.filter(s => s.role === 'owner').length)
</script>

<template>
  <p v-if="msg" class="banner ok">{{ msg }}</p>
  <p v-if="error" class="banner err">{{ error }}</p>

  <section class="card">
    <div class="card-head"><h2>แอดมินทั้งหมด</h2><p>{{ staff.length }} บัญชี · Super Admin {{ supers }} · Admin {{ staff.length - supers }}</p></div>
    <div class="table-wrap" :class="{ loading }">
      <table>
        <thead><tr><th>ชื่อ</th><th>สิทธิ์</th><th>เข้าใช้ล่าสุด</th><th></th></tr></thead>
        <tbody>
          <tr v-for="s in staff" :key="s.user_id">
            <td><strong>{{ s.name }}</strong><span v-if="s.user_id === me.user_id" class="muted"> (คุณ)</span><span class="sub">{{ s.email }}</span></td>
            <td>
              <span v-if="s.user_id === me.user_id" class="role-pill" :class="{ super: s.role === 'owner' }">{{ roleName(s.role) }}</span>
              <select v-else class="select" :value="s.role" @change="changeRole(s, $event.target.value)">
                <option v-for="r in ROLES" :key="r.v" :value="r.v">{{ r.t }}</option>
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
    <form class="card form" style="max-width:none" @submit.prevent="submit">
      <div class="card-head" style="margin:0"><h2>เพิ่มแอดมิน</h2></div>
      <div class="seg" style="justify-self:start">
        <button type="button" :class="{ on: addMode === 'new' }" @click="addMode = 'new'">สร้างบัญชีใหม่</button>
        <button type="button" :class="{ on: addMode === 'existing' }" @click="addMode = 'existing'">ใช้บัญชีที่มีอยู่แล้ว</button>
      </div>
      <p class="muted small" style="margin:0">{{ addMode === 'new' ? 'สำหรับคนที่ยังไม่มีบัญชี ระบบจะสร้างบัญชีให้ด้วยอีเมลและรหัสผ่านนี้' : 'สำหรับคนที่สมัครผ่านเกมแล้ว ใส่อีเมลที่ใช้สมัคร' }}</p>
      <label v-if="addMode === 'new'" class="field"><span>ชื่อที่แสดง</span><input v-model="form.name" class="input" /></label>
      <label class="field"><span>อีเมล</span><input v-model="form.email" class="input" type="email" required /></label>
      <label v-if="addMode === 'new'" class="field"><span>รหัสผ่านเริ่มต้น (อย่างน้อย 8 ตัว)</span><input v-model="form.password" class="input" type="text" minlength="8" required autocomplete="new-password" /></label>
      <div class="field"><span>สิทธิ์</span>
        <div class="role-options">
          <label v-for="r in ROLES" :key="r.v" class="role-option" :class="{ on: form.role === r.v }">
            <input v-model="form.role" type="radio" :value="r.v" />
            <span><b>{{ r.t }}</b><small>{{ r.d }}</small></span>
          </label>
        </div>
      </div>
      <button class="btn btn-primary" style="justify-self:start" :disabled="creating">{{ creating ? 'กำลังบันทึก…' : 'เพิ่มแอดมิน' }}</button>
    </form>

    <article class="card">
      <div class="card-head"><h2>สิทธิ์แต่ละระดับ</h2></div>
      <table>
        <thead><tr><th>ส่วน</th><th>Super Admin</th><th>Admin</th></tr></thead>
        <tbody>
          <tr v-for="r in matrix" :key="r[0]">
            <td>{{ r[0] }}</td>
            <td v-for="(v, i) in [r[1], r[2]]" :key="i" :class="v ? '' : 'muted'">{{ v ? 'ได้' : 'ไม่ได้' }}</td>
          </tr>
        </tbody>
      </table>
      <p class="muted small" style="margin-top:12px">บัญชีแอดมินไม่ถูกนับเป็นผู้เล่นในสถิติ · เปลี่ยนสิทธิ์หรือถอนสิทธิ์ตัวเองไม่ได้</p>
    </article>
  </section>
</template>
