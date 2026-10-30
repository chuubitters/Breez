/*
  Breez — Breath Gadget (ESP32-C3 Super Mini + KY-037 + จอกลม GC9A01 1.28")
  ----------------------------------------------------------------
  1) ส่งแรงเป่า 0-100 ให้เกม Unity ผ่าน USB (เหมือนเดิม)
  2) Quick Session: ฝึกหายใจ 4-7-8 บนตัวเครื่องได้เลย ไม่ต้องเปิดแอป
       หายใจเข้า 4 วิ -> กลั้น 7 วิ -> หายใจออก (เป่า) 8 วิ  x 4 รอบ
       กดปุ่ม BOOT บนบอร์ด (หรือปุ่มที่ต่อเพิ่ม) = เริ่ม / หยุด
       กดค้าง 2 วินาทีที่หน้าแรก = ปรับเสียงพื้นหลังใหม่ (calibrate)

  รูปแบบที่ส่งไป Unity (1 บรรทัด/ครั้ง):
      57                 ค่าแรงเป่า 0-100   (BreathReceiver.breathLevel)
      BAT:85             แบตเตอรี่ %         (ถ้าเปิดใช้ BAT_PIN)
      QS:START           เริ่ม Quick Session
      QS:STOP            หยุดกลางคัน
      QS:DONE,4,76,85    จบครบ: รอบ, วินาที, % เวลาที่เป่าได้ตอนหายใจออก
      # ...              ข้อความดีบัก        (Unity ไม่สนใจ)
  รับจาก Unity:
      NAME:<ชื่อ>  ICON:<...>   (จำไว้ใน flash)
      CAL                       คาลิเบรตใหม่
      QS                        เริ่ม Quick Session จากเกม

  ---------------- การต่อสาย ----------------
  KY-037           -> ESP32-C3 Super Mini
      + (VCC)      -> 3V3   (ห้ามใช้ 5V)
      G (GND)      -> GND
      A0 (AO)      -> GPIO3
  จอ GC9A01 (240x240 SPI)
      VCC          -> 3V3
      GND          -> GND
      SCL / SCK    -> GPIO4
      SDA / MOSI   -> GPIO6
      DC           -> GPIO10
      CS           -> GPIO7
      RST / RES    -> GPIO1
      BLK / BL     -> 3V3 (หรือไม่ต่อ ถ้าโมดูลดึงขึ้นไว้แล้ว)
  ปุ่ม: ใช้ปุ่ม BOOT บนบอร์ด (GPIO9) ได้เลย
        หรือต่อปุ่มกดเพิ่ม: ขาหนึ่ง -> GPIO0, อีกขา -> GND

  Arduino IDE:
      Board = "ESP32C3 Dev Module" (หรือ "Nologo ESP32C3 Super Mini")
      USB CDC On Boot = Enabled
      Library: "GFX Library for Arduino" (Arduino_GFX โดย moononournation)
*/

#include <Preferences.h>
#include <Arduino_GFX_Library.h>
#include "breez_ui.h"

// ---------------- ขา ----------------
const int MIC_PIN      = 3;     // KY-037 AO
const int LED_PIN      = 8;     // LED บนบอร์ด (active LOW)
const int BTN_PIN      = 9;     // ปุ่ม BOOT บนบอร์ด
const int EXT_BTN_PIN  = 0;     // ปุ่มต่อเพิ่ม (ต่อลง GND) ไม่ใช้ใส่ -1
const int BAT_PIN      = -1;    // ถ้าต่อ voltage divider 1:1 ใส่เลขขา ADC เช่น 5

const int TFT_SCK  = 4;
const int TFT_MOSI = 6;
const int TFT_CS   = 7;
const int TFT_DC   = 10;
const int TFT_RST  = 1;
const int TFT_ROTATION = 0;     // หมุนจอ 0-3
// ความเร็วส่งภาพไปจอ: สายจัมเปอร์ยาวบน breadboard ใช้ 20MHz
// ถ้าจอยังขึ้นสีมั่ว/เป็นจุดๆ ลดเป็น 10000000 / ถ้าบัดกรีสายสั้นแล้วลองเพิ่มเป็น 40000000 ให้ภาพลื่นขึ้น
const long TFT_SPI_HZ  = 20000000;

// ---------------- ตั้งค่า ----------------
const long BAUD          = 115200;
const int  SEND_EVERY_MS = 40;   // ส่งค่าแรงเป่า ~25 ครั้ง/วินาที
const int  WINDOW_MS     = 16;   // ช่วงวัดความดัง (peak-to-peak)

// ความไว: เป่าแรงแล้วไม่ถึง 100 -> ลด FULL_SCALE / เงียบแล้วไม่เป็น 0 -> เพิ่ม NOISE_GATE
int FULL_SCALE = 900;
int NOISE_GATE = 40;

// Quick Session 4-7-8
const int INHALE_S = 4, HOLD_S = 7, EXHALE_S = 8;
const int ROUNDS   = 4;          // ถ้าเปลี่ยน ให้แก้ข้อความ "ฝึกครบ 4 รอบแล้ว" ใน tools/display/make_text_bitmaps.py ด้วย
const int READY_S  = 3;
const int BLOW_MIN = 12;         // แรงเป่าขั้นต่ำที่นับว่า "กำลังเป่า" ตอนหายใจออก
const uint32_t DONE_SCREEN_MS = 30000;   // หน้าจบค้างไว้กี่ ms แล้วกลับหน้าแรก

// ---------------- จอ ----------------
Arduino_DataBus *bus = new Arduino_HWSPI(TFT_DC, TFT_CS, TFT_SCK, TFT_MOSI, GFX_NOT_DEFINED);
Arduino_GFX *gfx = new Arduino_GC9A01(bus, TFT_RST, TFT_ROTATION, true /* IPS */);
uint16_t *frame = nullptr;
bool displayOk = false;

// ---------------- สถานะ ----------------
enum State { ST_CAL, ST_IDLE, ST_READY, ST_INHALE, ST_HOLD, ST_EXHALE, ST_DONE, ST_STOPPED };
State state = ST_CAL;
uint32_t stateAt = 0;            // millis ที่เข้าสถานะนี้
uint32_t sessionAt = 0;
int round_ = 0;
uint32_t exhaleMs = 0, blowMs = 0;
int lastBlowPct = -1;
int sessionsDone = 0;

Preferences prefs;
String gadgetName = "Breath Gadget";
String gadgetIcon = "cat";

float baseline = 0;
float level = 0;
unsigned long lastSend = 0, lastBat = 0, lastLoop = 0;
String rxLine;

// ปุ่ม
bool btnDown = false, longFired = false;
uint32_t btnAt = 0;

// ---------------------------------------------------------------
int readLoudness()
{
  int lo = 4095, hi = 0;
  unsigned long start = millis();
  while (millis() - start < (unsigned long)WINDOW_MS)
  {
    int v = analogRead(MIC_PIN);
    if (v < lo) lo = v;
    if (v > hi) hi = v;
  }
  return hi - lo;
}

void showFrame()
{
  if (displayOk) gfx->draw16bitRGBBitmap(0, 0, frame, 240, 240);
}

void calibrate()
{
  Serial.println("# calibrating, keep quiet...");
  digitalWrite(LED_PIN, LOW);
  long sum = 0;
  const int n = 70;
  for (int i = 0; i < n; i++)
  {
    sum += readLoudness();
    if (displayOk && i % 4 == 0) { ui::drawCalibrating(millis()); showFrame(); }
  }
  baseline = (float)sum / n;
  level = 0;
  digitalWrite(LED_PIN, HIGH);
  Serial.printf("# baseline=%.0f\n", baseline);
}

void enter(State s)
{
  state = s;
  stateAt = millis();
}

// ---------------- Quick Session ----------------
void startSession()
{
  round_ = 0;
  exhaleMs = blowMs = 0;
  sessionAt = millis();
  enter(ST_READY);
  Serial.println("QS:START");
}

void stopSession()
{
  Serial.println("QS:STOP");
  enter(ST_STOPPED);
}

void finishSession()
{
  uint32_t secs = (millis() - sessionAt) / 1000;
  lastBlowPct = exhaleMs > 0 ? (int)(blowMs * 100 / exhaleMs) : 0;
  sessionsDone++;
  prefs.putInt("qs_count", sessionsDone);
  Serial.printf("QS:DONE,%d,%lu,%d\n", ROUNDS, (unsigned long)secs, lastBlowPct);
  enter(ST_DONE);
}

void onShortPress()
{
  switch (state)
  {
  case ST_IDLE:
  case ST_DONE:
  case ST_STOPPED: startSession(); break;
  case ST_READY:
  case ST_INHALE:
  case ST_HOLD:
  case ST_EXHALE: stopSession(); break;
  default: break;
  }
}

void onLongPress()
{
  if (state == ST_IDLE || state == ST_DONE || state == ST_STOPPED)
  {
    enter(ST_CAL);
    calibrate();
    enter(ST_IDLE);
  }
}

bool buttonPressed()
{
  bool p = digitalRead(BTN_PIN) == LOW;
  if (EXT_BTN_PIN >= 0) p = p || digitalRead(EXT_BTN_PIN) == LOW;
  return p;
}

void readButton()
{
  bool p = buttonPressed();
  uint32_t now = millis();
  if (p && !btnDown) { btnDown = true; btnAt = now; longFired = false; }
  else if (p && btnDown && !longFired && now - btnAt >= 2000) { longFired = true; onLongPress(); }
  else if (!p && btnDown)
  {
    btnDown = false;
    if (!longFired && now - btnAt >= 30) onShortPress();   // 30ms กันสัญญาณเด้ง
  }
}

/// เดินเวลาแต่ละเฟส แล้ววาดจอ
void updateSession(uint32_t dt)
{
  uint32_t now = millis();
  float t = (now - stateAt) / 1000.f;
  int lv = (int)(level + 0.5f);

  switch (state)
  {
  case ST_READY:
    if (t >= READY_S) { enter(ST_INHALE); t = 0; }
    else { ui::drawReady(READY_S - (int)t, t / READY_S); return; }
    // fallthrough
  case ST_INHALE:
    if (state == ST_INHALE)
    {
      if (t >= INHALE_S) { enter(ST_HOLD); t = 0; }
      else { ui::drawPhase(ui::PH_INHALE, t / INHALE_S, INHALE_S - (int)t, round_, ROUNDS, lv, false); return; }
    }
    // fallthrough
  case ST_HOLD:
    if (state == ST_HOLD)
    {
      if (t >= HOLD_S) { enter(ST_EXHALE); t = 0; }
      else { ui::drawPhase(ui::PH_HOLD, t / HOLD_S, HOLD_S - (int)t, round_, ROUNDS, lv, false); return; }
    }
    // fallthrough
  case ST_EXHALE:
    if (state == ST_EXHALE)
    {
      if (t >= EXHALE_S)
      {
        round_++;
        if (round_ >= ROUNDS) { finishSession(); ui::drawDone(0, lastBlowPct, sessionsDone); return; }
        enter(ST_INHALE);
        ui::drawPhase(ui::PH_INHALE, 0.f, INHALE_S, round_, ROUNDS, lv, false);
        return;
      }
      bool blowing = lv >= BLOW_MIN;
      exhaleMs += dt;
      if (blowing) blowMs += dt;
      ui::drawPhase(ui::PH_EXHALE, t / EXHALE_S, EXHALE_S - (int)t, round_, ROUNDS, lv, blowing);
    }
    return;
  default:
    return;
  }
}

void render()
{
  uint32_t now = millis();
  switch (state)
  {
  case ST_IDLE: ui::drawIdle(now, (int)(level + 0.5f), sessionsDone); break;
  case ST_DONE:
    if (now - stateAt > DONE_SCREEN_MS) { enter(ST_IDLE); ui::drawIdle(now, 0, sessionsDone); }
    else ui::drawDone(now - stateAt, lastBlowPct, sessionsDone);
    break;
  case ST_STOPPED:
    if (now - stateAt > 1500) { enter(ST_IDLE); ui::drawIdle(now, 0, sessionsDone); }
    else ui::drawStopped();
    break;
  case ST_CAL: ui::drawCalibrating(now); break;
  default: break;   // เฟสฝึก วาดใน updateSession
  }
}

// ---------------- Serial ----------------
void handleLine(String line)
{
  line.trim();
  if (line.startsWith("NAME:"))
  {
    gadgetName = line.substring(5);
    prefs.putString("name", gadgetName);
    Serial.println("# name=" + gadgetName);
  }
  else if (line.startsWith("ICON:"))
  {
    gadgetIcon = line.substring(5);
    prefs.putString("icon", gadgetIcon);
    Serial.println("# icon=" + gadgetIcon);
  }
  else if (line == "CAL")
  {
    enter(ST_CAL);
    calibrate();
    enter(ST_IDLE);
  }
  else if (line == "QS" || line == "QS:START")
  {
    if (state == ST_IDLE || state == ST_DONE || state == ST_STOPPED) startSession();
  }
}

void readCommands()
{
  while (Serial.available())
  {
    char c = Serial.read();
    if (c == '\n' || c == '\r')
    {
      if (rxLine.length()) handleLine(rxLine);
      rxLine = "";
    }
    else if (rxLine.length() < 64) rxLine += c;
  }
}

int readBatteryPercent()
{
  int mv = analogReadMilliVolts(BAT_PIN) * 2;
  return constrain(map(mv, 3300, 4200, 0, 100), 0, 100);
}

// ---------------------------------------------------------------
void setup()
{
  Serial.begin(BAUD);
#if ARDUINO_USB_CDC_ON_BOOT
  Serial.setTxTimeoutMs(0);
#endif
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, HIGH);
  pinMode(BTN_PIN, INPUT_PULLUP);
  if (EXT_BTN_PIN >= 0) pinMode(EXT_BTN_PIN, INPUT_PULLUP);
  analogReadResolution(12);

  prefs.begin("breez", false);
  gadgetName = prefs.getString("name", gadgetName);
  gadgetIcon = prefs.getString("icon", gadgetIcon);
  sessionsDone = prefs.getInt("qs_count", 0);

  frame = (uint16_t *)malloc(240 * 240 * sizeof(uint16_t));
  if (frame != nullptr) ui::begin(frame);
  if (frame != nullptr && gfx->begin(TFT_SPI_HZ))
  {
    displayOk = true;
    gfx->fillScreen(0xFFFF);
  }
  else
  {
    Serial.println("# display init failed (เช็คสายจอ) — ยังส่งค่าแรงเป่าได้ตามปกติ");
  }

  delay(300);
  enter(ST_CAL);
  calibrate();
  enter(ST_IDLE);
  lastLoop = millis();
}

void loop()
{
  readCommands();
  readButton();

  int loud = readLoudness();
  float over = loud - baseline - NOISE_GATE;
  float target = over <= 0 ? 0 : min(100.0f, over * 100.0f / FULL_SCALE);
  level += (target - level) * (target > level ? 0.6f : 0.25f);
  if (target == 0) baseline += (loud - baseline) * 0.01f;
  digitalWrite(LED_PIN, level > 30 ? LOW : HIGH);

  uint32_t now = millis();
  uint32_t dt = now - lastLoop;
  lastLoop = now;

  if (displayOk)
  {
    if (state >= ST_READY && state <= ST_EXHALE) updateSession(dt);
    else render();
    showFrame();
  }
  else if (frame != nullptr && state >= ST_READY && state <= ST_EXHALE)
  {
    updateSession(dt);   // ไม่มีจอก็ยังเดินเวลา (ดูผ่าน Serial ได้)
  }

  if (now - lastSend >= (unsigned long)SEND_EVERY_MS)
  {
    lastSend = now;
    Serial.println((int)(level + 0.5f));
  }
  if (BAT_PIN >= 0 && now - lastBat >= 30000)
  {
    lastBat = now;
    Serial.printf("BAT:%d\n", readBatteryPercent());
  }
}
