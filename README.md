# Breez

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

### หน้า Gadget Setting (ตั้งชื่อ + ไอคอนอุปกรณ์)
เมนู **Breez → Create Gadget Setting Scene**
- สร้าง `Assets/Scenes/GadgetSetting.unity`: แตะไอคอนเพื่อเลือก (แมว ดอกไม้ เมฆ ดาว หัวใจ พระจันทร์), แตะชื่อเพื่อเปลี่ยน, แสดงสถานะเชื่อมต่อ + แบตเตอรี่
- ทำให้รูป GadgetStatus ในหน้า Home กดแล้วเปิดหน้านี้
- ใส่ SerialController ใน scene (คัดลอก port จาก scene PowerRelease) เพื่อให้สถานะเป็นแบบสด
- บันทึกลงตาราง `devices` และส่งไปที่บอร์ด (ดูหัวข้อ ESP32 ด้านล่าง)

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

### ESP32: รับชื่อและไอคอนไปแสดงบนจอ (แบบ AirPods)
เมื่อบอร์ดเชื่อมต่อ หรือเปลี่ยนค่าในหน้า Gadget Setting เกมจะส่งมา 2 บรรทัด:
`NAME:<ชื่อ>` และ `ICON:<cat|flower|cloud|star|heart|moon>`

```cpp
String gadgetName = "Breath Gadget";
String gadgetIcon = "cat";

void readCommands() {
  while (Serial.available()) {
    String line = Serial.readStringUntil('\n');
    line.trim();
    if (line.startsWith("NAME:")) { gadgetName = line.substring(5); drawIdentity(); }
    else if (line.startsWith("ICON:")) { gadgetIcon = line.substring(5); drawIdentity(); }
  }
}
// เรียก readCommands() ใน loop() แล้วเขียน drawIdentity() ให้วาดชื่อ/ไอคอนบนจอ 1.28" (เช่นด้วย TFT_eSPI)
// ถ้าอยากให้จำค่าไว้หลังปิดเครื่อง ใช้ Preferences.h บันทึก gadgetName / gadgetIcon
```

## 3. Breath Gadget: ESP32-C3 Super Mini + KY-037

โค้ดบอร์ด: [`esp32/breez_gadget/breez_gadget.ino`](esp32/breez_gadget/breez_gadget.ino)

### ต่อสาย
| KY-037 | ESP32-C3 Super Mini |
|---|---|
| + (VCC) | **3V3** (ห้ามใช้ 5V) |
| G (GND) | GND |
| A0 (AO) | **GPIO3** |
| D0 | ไม่ต้องต่อ |

### Arduino IDE
1. Settings → Additional boards manager URLs ใส่
   `https://espressif.github.io/arduino-esp32/package_esp32_index.json`
2. Boards Manager → ติดตั้ง **esp32 by Espressif Systems**
3. Tools → Board: **ESP32C3 Dev Module** (หรือ Nologo ESP32C3 Super Mini)
4. Tools → **USB CDC On Boot: Enabled** (สำคัญ)
5. เลือก Port (Mac: `/dev/cu.usbmodem…`) แล้ว Upload
   - ถ้า upload ไม่ได้: กดปุ่ม BOOT ค้างไว้ แล้วเสียบ USB ใหม่ แล้วกด Upload
6. เปิด Serial Monitor ที่ 115200 ลองเป่า ควรเห็นตัวเลข 0 → 100 แล้ว **ปิด Serial Monitor ก่อนเปิด Unity** (พอร์ตใช้ได้ทีละโปรแกรม)

ปรับความไวที่บรรทัด `FULL_SCALE` (เป่าแรงแล้วไม่ถึง 100 → ลดค่า) และ `NOISE_GATE` (เงียบแล้วยังไม่เป็น 0 → เพิ่มค่า)
ตอนเปิดบอร์ดจะคาลิเบรตเสียงพื้นหลัง 1-2 วินาที (ไฟบนบอร์ดติด) อย่าเป่าช่วงนั้น

### Unity
1. เมนู **Breez → Configure Serial for ESP32** — ตั้งทุก scene ที่มี SerialController เป็น 115200 + DTR และใส่ `SerialPortAutoSelect` ให้หาพอร์ต ESP32 เอง
2. เสียบบอร์ด → **Breez → Show Serial Ports** ต้องเห็น `/dev/cu.usbmodem…`
3. กด Play ใน PowerRelease / BreathFocus / Rainy Forest — Console จะขึ้น `[Breez] Serial port: …`
4. ถ้าเกมไวเกิน/ไม่ไวพอ ปรับ `Breath Threshold` ใน GameManager ของแต่ละเกม (ค่าเริ่มต้น 50)

## 4. หลังบ้าน (Admin Dashboard)
ดู [`admin-dashboard/README.md`](admin-dashboard/README.md) — วิธีตั้ง owner คนแรก และ deploy ขึ้น Vercel
