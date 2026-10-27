# Breez (fuuu)

เกมฝึกหายใจ Unity + Breath Gadget (ESP32-C3) + Supabase + Admin Dashboard

```
ESP32 (MAX9814) --USB Serial--> Unity (Ardity) --HTTPS--> Supabase <-- Admin Dashboard (Vercel)
```

| โฟลเดอร์ | คืออะไร |
|---|---|
| `Assets/` | เกม Unity 2022.3 |
| `Assets/Script/Backend/` | ระบบ login + บันทึกข้อมูลขึ้น Supabase |
| `admin-dashboard/` | หลังบ้าน Vue (deploy บน Vercel) ดู `admin-dashboard/README.md` |
| `supabase/migrations/` | SQL ของฐานข้อมูล (ติดตั้งใน project แล้ว) |
| `supabase/functions/create-staff/` | Edge Function สำหรับ owner สร้างบัญชีผู้ดูแล |

---

## 1. ฐานข้อมูล (Supabase)

ติดตั้งแล้วใน project `ubdiwwmtbuzksyswmwod` ตารางตาม ERD:
`users`, `devices`, `mood_logs`, `play_sessions`, `calm_flow_results`, `power_release_results`, `breathe_focus_results`
และเพิ่ม `staff_roles` (owner / admin)

- ผู้เล่นสมัครจากเกม → สร้างแถวใน `users` อัตโนมัติ
- ผู้เล่นเห็น/เขียนได้เฉพาะข้อมูลตัวเอง (Row Level Security)
- `user_id` เป็น uuid (ผูกกับ Supabase Auth) แทน int ใน ERD

**แนะนำสำหรับช่วงพัฒนา:** Supabase → Authentication → Sign In / Providers → Email →
ปิด **Confirm email** เพื่อให้สมัครในเกมแล้วเล่นได้ทันที (ถ้าเปิดไว้ ผู้เล่นต้องกดยืนยันในอีเมลก่อน)

## 2. เกม Unity

### ครั้งแรกหลัง pull
1. เปิดโปรเจกต์ใน Unity รอ compile
2. เมนู **Breez → Create Login Scene**
   สร้าง `Assets/Scenes/Login.unity` และตั้งเป็น scene แรกใน Build Settings ให้อัตโนมัติ
3. เปิด scene `Home` → เมนู **Breez → Add Account Actions To Open Scene** → Save
   (ถ้ายังไม่ login จะเด้งไปหน้า Login, ผูกปุ่ม Logout ไปที่ `AccountActions.SignOut`)
4. กด Play ที่ scene Login → สมัคร → เล่นเกม → ดูข้อมูลในหลังบ้าน

### หน้า Mood Tracking & Weekly Summary
เมนู **Breez → Create Mood Tracking Scene**
- สร้าง `Assets/Scenes/MoodTracking.unity` ตามดีไซน์ (เลือกอารมณ์วันนี้, จำนวน session, อารมณ์ที่พบบ่อย, จำนวนแต่ละเกม, streak, การเล่นล่าสุด) ข้อมูลย้อนหลัง 7 วัน
- ผูกปุ่ม "Mood Tracking & Summary" ใน scene Mode ให้เปิดหน้านี้อัตโนมัติ
- รูปหน้าอารมณ์ใน `Assets/element/Mood/` เป็น placeholder — เปลี่ยนเป็นไอคอนจาก Figma ได้โดยแทนที่ไฟล์ PNG ชื่อเดิม
- ถ้า import font ไทย (เช่น Prompt / Noto Sans Thai) แล้วสร้าง TMP Font Asset ไว้ในโปรเจกต์ ให้กดเมนูนี้อีกครั้ง จะใช้ข้อความภาษาไทยให้

### ข้อมูลที่บันทึกอัตโนมัติ
| เกม | จุดที่บันทึก | ข้อมูล |
|---|---|---|
| Power Release | `PWGameManager.EndGame` | คะแนน, เวลา, ลูกโป่งทั้งหมด/ที่แตก |
| Breathe & Focus | `BFGameManager.GameOver` | คะแนน, เวลา, สิ่งกีดขวางที่หลบได้ |
| Soundscape (Rainy Forest) | `SPGameManager` ตอนออกจาก scene (เล่น ≥ 10 วิ) | เวลาฝึก |
| Calm Flow | ยังไม่มี GameManager ในรีโป — ตอนจบเกมให้เรียก `GameDataRecorder.RecordCalmFlow(score, seconds, flameOutCount)` | |
| อุปกรณ์ | ทุก 60 วิ ขณะเสียบบอร์ด | ออนไลน์/ออฟไลน์, แบตเตอรี่ |

ถ้าเน็ตหลุด ผลจะถูกเก็บในเครื่องและส่งให้เองเมื่อออนไลน์

### สคริปต์ที่ใช้ต่อกับ UI ได้
- `MoodPicker` — ผูกปุ่มอารมณ์ไปที่ `SelectHappy()`, `SelectStress()` ฯลฯ
- `PlayerSummaryUI` — หน้า Mood Tracking & Summary (จำนวนครั้ง, นาทีรวม, streak, อารมณ์)
- `DeviceSettings` — ตั้งชื่อ/ไอคอนอุปกรณ์ (ฟิลด์เดิม ผูกปุ่มเดิมได้)

### ESP32: ส่งระดับแบตเตอรี่ (ไม่บังคับ)
ส่งบรรทัด `BAT:<0-100>` ผ่าน Serial เป็นระยะ เช่นทุก 30 วินาที เกมจะส่งต่อให้หลังบ้าน

```cpp
// ต่อ Li-Po ผ่าน voltage divider 1:1 เข้า BAT_PIN (ADC)
int mv = analogReadMilliVolts(BAT_PIN) * 2;
int pct = constrain(map(mv, 3300, 4200, 0, 100), 0, 100);
Serial.print("BAT:");
Serial.println(pct);
```
ค่าแรงเป่า (`0-100`) ส่งแบบเดิมได้เลย

## 3. หลังบ้าน (Admin Dashboard)
ดู [`admin-dashboard/README.md`](admin-dashboard/README.md) — วิธีตั้ง owner คนแรก และ deploy ขึ้น Vercel
