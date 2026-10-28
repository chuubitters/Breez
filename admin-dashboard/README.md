# breez — Admin Dashboard

หลังบ้านสำหรับดูข้อมูลผู้เล่นจากเกม Unity (Vue 3 + Vite + Supabase)

## สิทธิ์ผู้ใช้

| ส่วน | Owner (เจ้าของ) | Admin (ผู้ดูแล) |
|---|---|---|
| ภาพรวม / กราฟ | ✓ | ✓ |
| ภาพรวมผู้เล่น / อารมณ์ (ไม่ระบุตัวตน) | ✓ | ✓ |
| ประวัติการเล่น + CSV | ✓ | ✓ |
| อุปกรณ์ | ✓ | ✓ |
| บันทึกอารมณ์รายบุคคล | ✓ | ✗ (เห็นแค่สัดส่วนรวม) |
| จัดการผู้ดูแล | ✓ | ✗ |

สิทธิ์ถูกบังคับที่ฐานข้อมูล (ฟังก์ชัน `dashboard_*` ใน `supabase/migrations`) ไม่ใช่แค่ซ่อนเมนู
ผู้เล่นทั่วไปที่สมัครจากเกมจะ login หลังบ้านไม่ได้

## ตั้ง Owner คนแรก (ทำครั้งเดียว)

1. Supabase Dashboard → **Authentication → Users → Add user → Create new user**
   ใส่อีเมล + รหัสผ่าน และติ๊ก **Auto Confirm User**
2. **SQL Editor** แล้วรัน
   ```sql
   select public.grant_owner_by_email('อีเมลของคุณ');
   ```
3. เข้าหลังบ้านด้วยอีเมลนั้น แล้วเพิ่ม Admin คนอื่นได้จากเมนู **ผู้ดูแลระบบ**

## รันในเครื่อง

```bash
cd admin-dashboard
npm install
npm run dev        # http://localhost:5173
```

ค่า Supabase URL / publishable key ตั้ง default ไว้ใน `src/lib/supabase.js` แล้ว
ถ้าต้องการเปลี่ยนให้คัดลอก `.env.example` เป็น `.env.local`

## Deploy ขึ้น Vercel (แนะนำ ฟรี)

1. เข้า https://vercel.com → **Add New → Project** → เลือกรีโป `chuubitters/Breez`
2. **Root Directory** = `admin-dashboard`
3. Framework Preset = **Vite** (Build `npm run build`, Output `dist` — Vercel ตั้งให้อัตโนมัติ)
4. (ไม่บังคับ) Environment Variables: `VITE_SUPABASE_URL`, `VITE_SUPABASE_ANON_KEY`
5. Deploy → ได้ URL เช่น `breez-admin.vercel.app`
6. Supabase → **Authentication → URL Configuration** ใส่ URL นั้นใน Site URL / Redirect URLs

หลังจากนี้ทุกครั้งที่ push เข้า GitHub Vercel จะ deploy ใหม่ให้อัตโนมัติ
(ใช้ Netlify หรือ Cloudflare Pages ก็ได้ ตั้งค่าแบบเดียวกัน)

## ห้ามทำ

- ห้ามใส่ `service_role` key ในโค้ดนี้หรือใน Vercel env ที่ขึ้นต้นด้วย `VITE_`
  (การสร้างบัญชีผู้ดูแลใช้ Edge Function `create-staff` ฝั่ง server แทน)
