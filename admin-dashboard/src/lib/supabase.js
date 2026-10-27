import { createClient } from '@supabase/supabase-js'

// ค่า default ชี้ไปที่ project ของ Breez (publishable key เป็นค่าสาธารณะ ใช้ใน browser ได้)
// ถ้าตั้ง VITE_SUPABASE_URL / VITE_SUPABASE_ANON_KEY ใน .env.local หรือ Vercel จะใช้ค่านั้นแทน
const url = import.meta.env.VITE_SUPABASE_URL || 'https://ubdiwwmtbuzksyswmwod.supabase.co'
const key = import.meta.env.VITE_SUPABASE_ANON_KEY || 'sb_publishable_E8-tt98y8Z0-ZxWUPvJgWg_2mYOAGdP'

export const supabase = createClient(url, key, {
  auth: { persistSession: true, autoRefreshToken: true, storageKey: 'breez-admin-auth' },
})

export async function rpc(name, args = {}) {
  const { data, error } = await supabase.rpc(name, args)
  if (error) throw new Error(translateError(error.message))
  return data
}

export function translateError(msg = '') {
  if (/Invalid login credentials/i.test(msg)) return 'อีเมลหรือรหัสผ่านไม่ถูกต้อง'
  if (/Email not confirmed/i.test(msg)) return 'บัญชีนี้ยังไม่ได้ยืนยันอีเมล'
  if (/owner only/i.test(msg)) return 'ส่วนนี้เฉพาะเจ้าของระบบ (Owner) เท่านั้น'
  if (/staff only/i.test(msg)) return 'บัญชีนี้ไม่มีสิทธิ์เข้าหลังบ้าน'
  if (/Failed to fetch|NetworkError/i.test(msg)) return 'เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ต'
  return msg
}
