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

### Element ใช้ซ้ำ: สถานะหายใจ + สถานะ Breath Gadget
- **Breez → Create Status Elements** สร้าง prefab ใน `Assets/Prefabs/Breez/`
  - `BreathStatus` — "status : หายใจเข้า / หายใจออก" (เป่า = หายใจออก, กดลูกศรขึ้นก็ได้) มีแถบแรงเป่าเล็กๆ ด้านล่าง
  - `GadgetStatus` — [ไอคอน] ชื่อเครื่อง · เชื่อมต่อแล้ว / ยังไม่เชื่อมต่อ (ไอคอนและชื่อมาจากหน้า Gadget Setting, กดแล้วไปหน้า Gadget Setting) แบตเตอรี่ซ่อนไว้ เปิดได้ที่ `Show Battery`
- **Breez → Add Status Elements To Game Scenes** ใส่ BreathStatus ใน PowerRelease / BreathFocus / Rainy Forest และ GadgetStatus ใน Home ให้ในครั้งเดียว
- หรือลาก prefab ใส่ Canvas เอง / ใช้ **Add Breath Status To Open Scene**, **Add Gadget Status To Open Scene**
- ข้อความแก้ได้ใน Inspector (ถ้าไม่มี font ไทย จะใช้ Inhale / Exhale, Connected / Not connected)

### Soundscape: Rainy Forest + Windy Mountain
- **Breez → Upgrade Rainy Forest Rain** — ใส่ `BreathRain` ให้ RainEffect: ฝนเม็ดเล็ก (เส้นฝนบางๆ แทนรูปเม็ดใหญ่) ตกเมื่อเป่า ยิ่งเป่าแรงยิ่งตกหนัก ปรับขนาด/ความหนาแน่น/สีได้ใน Inspector
- **Breez → Create Windy Mountain Scene** — สร้าง `Windy Mountain.unity` จาก Rainy Forest (ระบบหายใจ 5-5, Start, countdown, บันทึก session เหมือนเดิม)
  - ฉากภูเขาโทนพาสเทล + เงาดอกแดนดิไลออน 6 ต้น
  - เป่า = เมล็ดหลุดปลิวไปตามลม ต้นโยกตามแรงเป่า, หยุดเป่า ~2.5 วิ เมล็ดงอกกลับ
  - เสียง BGM / SFX เว้นว่าง: ใส่ AudioClip ที่ `SPGameManager > Bgm / Sfx`
  - ผูกปุ่ม "Windy Mountain" ในหน้าเลือก Soundscape ให้อัตโนมัติ
  - ฉากหลังเป็น placeholder เปลี่ยน sprite ที่ `Background (replace me)` ได้

### ฟอนต์ทั้งเกม: LINE Seed Sans TH App
ทั้งเกมใช้ **LINE Seed Sans TH App** ทั้งไทยและอังกฤษ (Regular + Bold, SIL OFL 1.1)
TextMeshPro จัดวรรณยุกต์/สระบนเองไม่ได้ จึงใช้เวอร์ชันที่จัดตำแหน่งไว้แล้ว ชื่อไฟล์ **Breez Seed TH** + สคริปต์ `ThaiText`
1. เมนู **Breez → Install LINE Seed Font (ทั้งเกม)** (กดซ้ำได้)
   - สร้าง `Assets/Fonts/Breez/Breez Seed TH SDF` และ `Breez Seed TH Bold SDF`
   - ตั้งเป็นฟอนต์เริ่มต้นของ TextMeshPro (ข้อความใหม่ใช้ฟอนต์นี้อัตโนมัติ)
   - เปลี่ยนข้อความทุกอันในทุก scene + prefab: ตัวหนา → Bold, นอกนั้น → Regular และใส่ `ThaiText`
   - ลบฟอนต์เก่า (Breez Thai / Montserrat)
2. ข้อความใหม่ที่มีภาษาไทย: Add Component → `ThaiText` (พิมพ์ไทยตามปกติได้เลย) ถ้าต้องการตัวหนาให้เลือก `Breez Seed TH Bold SDF` แทนการกดปุ่ม B
3. ฟอนต์สร้างจาก `tools/thai-font/build_thai_font.py`

### ภาษาไทยทั้งเกม
เมนู **Breez → Update Thai Text (ทั้งเกม)** แปลข้อความในทุก scene + prefab เป็นภาษาไทย (คงชื่อโหมด/ชื่อหัวข้อหลักเป็นภาษาอังกฤษ) และใช้ฟอนต์ LINE Seed ให้ด้วย
ทุกเมนูที่สร้างหน้าใหม่จะแปลให้อัตโนมัติ

### Breath Play: เลเยอร์ + วิธีการเล่น
เมนู **Breez → Fix Layers + How To Play (Breath Play)** (Power Release / Breathe & Focus / Calm Flow)
- ย้ายหน้าวิธีการเล่น / ผลลัพธ์ / พัก / นับถอยหลัง ไปไว้ใน `BreezTopCanvas` (Overlay, Sort Order 100) อยู่บนสุดเสมอ
- หน้าวิธีการเล่นหน้าตาเดียวกันทั้ง 3 เกม: การ์ดขาว + ภาพประกอบ + คำอธิบาย 2 บรรทัด + ปุ่ม "เริ่ม" + Back

### Rainy Forest: ฝน
ถ้า RainEffect หายไปจาก scene เกมจะสร้างฝนให้เองตอนเริ่ม (`SPGameManager.autoCreateRain`)
หรือกด **Breez → Upgrade Rainy Forest Rain** เพื่อสร้าง/ตั้งค่าฝนลงใน scene ถาวร

### Soundscape: หน้านับถอยหลัง
เมนู **Breez → Restyle Soundscape Countdown** ใช้กับ Rainy Forest + Windy Mountain
- นับถอยหลัง 3 · 2 · 1 · เริ่ม! เป็นวงกลมกลางจอ วงแหวนลดลงทีละวินาที
- ป้ายนำการหายใจ: วงกลมขยาย/หด + หายใจเข้า/ออก + วินาทีที่เหลือ + แถบความคืบหน้า
- บันทึกฉาก, จำนวนรอบหายใจ 5-5 ที่ครบ และเวลาที่เป่า ขึ้นหลังบ้าน
- กด "จบการฝึก" => หน้าผลลัพธ์: เวลาที่ใช้ในโหมดนี้ / รอบหายใจ / เวลาที่เป่า + Replay / Home (ไปหน้าเลือกโหมด)
- Windy Mountain: จัดหัวข้อ / ปุ่มกลับ / สถานะอุปกรณ์ / ปุ่มจบ ตามตำแหน่งของ Rainy Forest (ถ้าย้ายใน Rainy Forest แล้วกดเมนูนี้ซ้ำ Windy จะย้ายตาม)

### จัด UI ทุกหน้า + แอนิเมชัน
เมนู **Breez → Polish UI (All Scenes)** (กดซ้ำได้)
- ปุ่มกลับทุกหน้าใช้รูป `back-bt` ขนาดและตำแหน่งเดียวกัน (มุมซ้ายบน) ปุ่มกลับในหน้าวิธีการเล่น/ผลลัพธ์ซ้อนตรงกับของฉากพอดี
- Soundscape: หัวข้อ + สถานะอุปกรณ์อยู่แถวเดียวกับปุ่มกลับ / นับถอยหลังพื้นหลังขาว

ทำงานเองตอนเล่น ไม่ต้องกดเมนู (`Assets/Script/UI`)
- ทุกปุ่ม: ชี้แล้วขยายเล็กน้อย กดแล้วยุบ ปล่อยแล้วเด้ง (`BreezButtonFx`)
- หน้าวิธีการเล่น / ผลลัพธ์ / พัก / สรุป: ค่อยๆ ปรากฏ และอยู่บนสุดเสมอ (`BreezPanelPop`)
- ไม่อยากให้ปุ่ม/แผงไหนมีแอนิเมชัน ใส่ component `BreezNoFx`
- เมนูนี้ยังเปลี่ยนรูป `GadgetStatus.png` (แค่ภาพ) ทุกหน้าเป็นป้ายสถานะจริง (prefab GadgetStatus) กดแล้วไปหน้าตั้งค่า แล้วกลับมาหน้าเดิม

### หน้า Gadget Setting
เมนู **Breez → Restyle Gadget Setting**
- แก้ชื่อ เลือกไอคอน 6 แบบ เลือกสีไอคอน 8 สี เห็นตัวอย่างทันที แล้วกด **บันทึก** (ยกเลิกได้ / กดกลับตอนยังไม่บันทึกจะเตือนก่อน)
- สีเก็บใน `devices.device_icon` แบบ `cat|#A9C77E` และส่ง `ICON:` + `COLOR:` ไปที่บอร์ด

### เสียง (BGM + เสียงคลิก + เสียงประกอบเกม)
ไม่ต้องกดเมนู ใช้ได้ทันทีหลัง pull
- ไฟล์เสียงทั้งหมดอยู่ที่ `Assets/Sound/Breez` (สร้างจาก `tools/audio/make_sounds.py` ใช้ได้ฟรี ไม่ติดลิขสิทธิ์)
- จับคู่เสียงที่ `Assets/Resources/BreezSoundBank.asset`: ลากไฟล์เสียงใหม่ใส่ช่อง `clip` ของ id ที่ต้องการเปลี่ยน / ปรับ `volume` ได้ / ตั้งเพลงแต่ละ scene ใน `Scene Music` (เว้นว่าง = ไม่เล่นเพลง)
- เพลงพื้นหลังเล่นต่อเนื่องข้าม scene (`BreezAudio`) เปลี่ยนเพลงแบบ crossfade
- ทุกปุ่มมีเสียงคลิกอัตโนมัติ (ปุ่มกลับ/ปิดใช้เสียง `ui_back`) ไม่อยากให้ปุ่มไหนมีเสียง ใส่ component `BreezNoClickSound`
- Soundscape: ถ้า scene ใส่เสียง BGM/SFX เองไว้ จะใช้ของเดิม ถ้าช่องว่างจะใช้เสียงของ Breez (ลม / ฝน)
- ระดับเสียงเก็บใน PlayerPrefs: `BreezAudio.MusicVolume`, `BreezAudio.SfxVolume`, `BreezAudio.Muted`

### Weekly Summary (สรุปรายสัปดาห์)
เมนู **Breez → Create Mood Tracking Scene** สร้างหน้าใหม่ตามดีไซน์ (พื้นหลังดอกไม้ `Assets/element/Mood/weekly_bg.png`)
ลิงก์ **See All** ของ "อารมณ์เฉลี่ย" เปิดประวัติอารมณ์ / ของ "เล่นล่าสุด" เปิดประวัติการเล่น (แยกกัน)

### Weekly Summary: ดูประวัติย้อนหลัง
เมนู **Breez → Add Daily History To Mood Tracking** ใส่ปุ่ม "ดูประวัติ" ในการ์ดการเล่นล่าสุด
กดแล้วเลือกวัน (เลื่อนทีละสัปดาห์) เพื่อดูอารมณ์และการเล่นของวันนั้น (RPC `my_history`)

### Breathe & Focus
- เมนู **Breez → Restyle Breathe & Focus**: หน้า "วิธีการเล่น" + หน้า "ผลลัพธ์" แบบใหม่ (Replay / Home / Back)
- เป่า = ลอยขึ้นทันที, หยุดเป่า = ตกทันที (`PlayerController.instantControl`, `riseSpeed`, `fallSpeed`)
- เมฆเดี่ยว สุ่มอยู่บน / ล่าง / บางครั้งกลาง ห่างกันมากขึ้น (`ObstacleSetManager`: `singleCloudMode`, `cloudSpacingX`)
- เป่านานแค่ไหนตัวละครก็ไม่ลอยพ้นขอบบน แต่ถ้าไม่เป่าจะตกลงได้ (`PlayerController.clampToTop`)

### Calm Flow (ผ่อนลมหายใจให้ดอกไม้บาน)
เมนู **Breez → Create Calm Flow Scene** สร้าง `Assets/Scenes/CalmFlow.unity` และผูกปุ่ม Calm Flow ในหน้า BreathPlay ให้
1. เลือกเวลา Session 1 / 2 / 3 นาที → **Start Session**
2. เป่า (หรือกดลูกศรขึ้น) = ดอกไม้ค่อยๆ โตและบาน ยิ่งเป่าต่อเนื่องนานยิ่งใหญ่ (8 วินาที = 100%)
3. หยุดเป่านานกว่า 0.55 วินาที = บันทึกขนาดดอก ปลูกลงสวนด้านล่าง แล้วเริ่มดอกใหม่ (สัญญาณตกแป๊บเดียวไม่นับว่าหยุด)
4. หมดเวลา = สรุป Flowers Grown / Largest Flower / Longest Exhale / Total Exhale Time และบันทึกขึ้น Supabase
- ปรับค่าได้ที่ `GameManager > CalmFlowGame`: Breath Threshold, Grace Seconds (0.4–0.7), Seconds To Full Bloom, Min Exhale Seconds
- สีดอก / ขนาดกลีบ ปรับที่ `Flower > BloomFlower`

### Power Release (เป่าลูกโป่ง)
เมนู **Breez → Restyle Power Release** จัดหน้าให้ตรงดีไซน์ (กดซ้ำได้)
- ลูกโป่งเกิดใน `BalloonLayer` ซึ่งอยู่เหนือ bg/เมฆ แต่อยู่ใต้เวลา, `จำนวนลูกโป่ง : x/20` และป้ายหายใจ
- เวลานับถอยหลังแบบ 00:00, ปุ่ม "← กลับ" ไปหน้า BreathPlay
- หน้าผลลัพธ์: คะแนน / ระยะเวลาที่ใช้ / ลูกโป่งที่เป่าได้ + ปุ่ม **เล่นอีกครั้ง** (เริ่มใหม่) และ **หน้าหลัก** (ไป Home)
- เวลาเล่นยังเป็น `timeLimit` = 50 วินาทีที่ GameManager (ดีไซน์โชว์ 02:00 ถ้าต้องการ 2 นาทีให้ตั้ง 120)

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

## 3. Breath Gadget: ESP32-C3 Super Mini + KY-037 + จอกลม GC9A01

โค้ดบอร์ด: [`esp32/breez_gadget/breez_gadget.ino`](esp32/breez_gadget/breez_gadget.ino)

### ต่อสาย
| KY-037 | ESP32-C3 Super Mini |
|---|---|
| + (VCC) | **3V3** (ห้ามใช้ 5V) |
| G (GND) | GND |
| A0 (AO) | **GPIO3** |
| D0 | ไม่ต้องต่อ |

| จอ GC9A01 1.28" | ESP32-C3 Super Mini |
|---|---|
| VCC | **3V3** |
| GND | GND |
| SCL (SCK) | **GPIO4** |
| SDA (MOSI) | **GPIO6** |
| DC | **GPIO10** |
| CS | **GPIO7** |
| RST (RES) | **GPIO1** |
| BLK (BL) | 3V3 (หรือไม่ต่อ) |

ผังการเสียบบน breadboard ครึ่งแผ่น (30 แถว): [`tools/display/breadboard_wiring.png`](tools/display/breadboard_wiring.png)

ปุ่มเริ่ม/หยุด Quick Session: ใช้ปุ่ม **BOOT** บนบอร์ดได้เลย หรือต่อปุ่มกดเพิ่ม ขาหนึ่ง → **GPIO0** อีกขา → GND

### Quick Session (ฝึกหายใจ 4-7-8 บนตัวเครื่อง ไม่ต้องเปิดแอป)
- กดปุ่ม → นับ 3-2-1 → หายใจเข้า 4 วิ → กลั้น 7 วิ → หายใจออก (เป่าใส่ไมค์) 8 วิ × 4 รอบ
- ระหว่างหายใจออก วงกลมจะเรืองแสงเมื่อเป่าจริง หน้าจบบอกว่าเป่าได้กี่ %
- กดปุ่มระหว่างฝึก = หยุด / กดค้าง 2 วินาทีที่หน้าแรก = ปรับเสียงพื้นหลังใหม่
- จำนวนครั้งที่ฝึกเก็บในเครื่อง (แสดง "ฝึกแล้ว N ครั้ง")
- ถ้าเสียบสายกับเกมอยู่ตอนฝึกจบ เกมจะบันทึกลงประวัติ (mode `Quick Session`)
- แก้ข้อความบนจอ: แก้ `tools/display/make_text_bitmaps.py` แล้วรัน (ใช้ฟอนต์เดียวกับเกม) → ได้ `ui_text.h` ใหม่
- พรีวิวหน้าจอ: [`tools/display/quick_session_preview.png`](tools/display/quick_session_preview.png)

### Arduino IDE
1. Settings → Additional boards manager URLs ใส่
   `https://espressif.github.io/arduino-esp32/package_esp32_index.json`
2. Boards Manager → ติดตั้ง **esp32 by Espressif Systems**
3. Tools → Board: **ESP32C3 Dev Module** (หรือ Nologo ESP32C3 Super Mini)
4. Tools → **USB CDC On Boot: Enabled** (สำคัญ)
   - Library Manager → ติดตั้ง **GFX Library for Arduino** (by Moon On Our Nation)
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
