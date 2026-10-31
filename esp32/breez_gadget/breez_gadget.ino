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
      QS:LOG,7,4,76,85,1793500000
                         ผลที่ยังไม่ได้ส่ง: id, รอบ, วินาที, % เป่า, เวลาจบ (unix, 0 = ไม่ทราบ)
                         เก็บในเครื่องได้ 20 รายการ ส่งซ้ำจนกว่าเกมตอบ QS:ACK
  Quick Session ที่ฝึกตอนไม่ได้เสียบเกม จะถูกเก็บไว้ แล้วส่งเข้าเกม/หลังบ้านเองตอนเสียบสาย
      # ...              ข้อความดีบัก        (Unity ไม่สนใจ)
  รับจาก Unity:
      NAME:<ชื่อ>  ICON:<...>   (จำไว้ใน flash)
      CAL                       คาลิเบรตใหม่
      QS                        เริ่ม Quick Session จากเกม
      TIME:<unix>               เวลาปัจจุบัน (เกมส่งตอนเชื่อมต่อ) => ส่งผลที่ค้างอยู่
      SYNC                      ส่งผลที่ค้างอยู่อีกครั้ง
      QS:ACK,<id>               เกมบันทึกแล้ว => ลบออกจากเครื่อง

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
const int  SEND_EVERY_MS = 25;   // ส่งค่าแรงเป่า ~40 ครั้ง/วินาที
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
const int BAND_H = 40;           // วาดจอทีละ 40 แถว (6 แถบ) ใช้ RAM แค่ 19KB
static uint16_t band[240 * BAND_H];
bool displayOk = false;
uint32_t lastDiag = 0;

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

void drawCurrent(uint32_t now);

/// วาดทั้งจอทีละแถบ แล้วส่งขึ้นจอ
void showFrame()
{
  if (!displayOk) return;
  uint32_t now = millis();
  for (int y = 0; y < 240; y += BAND_H)
  {
    ui::setBand(band, y, BAND_H);
    drawCurrent(now);
    gfx->draw16bitRGBBitmap(0, y, band, 240, BAND_H);
  }
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
    if (i % 4 == 0) showFrame();   // state = ST_CAL => หน้าปรับเสียง
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

// ---------------- ผล Quick Session ที่ยังไม่ได้ส่ง (เก็บใน flash) ----------------
const int QS_MAX = 20;
struct QsLog { uint32_t id; uint8_t rounds; uint16_t secs; int8_t blow; uint32_t epoch; uint32_t boot; uint32_t ms; };
QsLog qsLogs[QS_MAX];
int qsCount = 0;
uint32_t qsNextId = 1, bootId = 0;
uint32_t epochAtSync = 0, millisAtSync = 0;

uint32_t nowEpoch() { return epochAtSync ? epochAtSync + (millis() - millisAtSync) / 1000 : 0; }

void saveLogs()
{
  prefs.putBytes("qs_logs", qsLogs, sizeof(QsLog) * qsCount);
  prefs.putInt("qs_n", qsCount);
  prefs.putUInt("qs_next", qsNextId);
}

void loadLogs()
{
  qsCount = constrain(prefs.getInt("qs_n", 0), 0, QS_MAX);
  if (qsCount > 0 && prefs.getBytesLength("qs_logs") == sizeof(QsLog) * qsCount)
    prefs.getBytes("qs_logs", qsLogs, sizeof(QsLog) * qsCount);
  else qsCount = 0;
  qsNextId = prefs.getUInt("qs_next", 1);
  bootId = prefs.getUInt("boot", 0) + 1;
  prefs.putUInt("boot", bootId);
}

void sendLogs()
{
  for (int i = 0; i < qsCount; i++)
  {
    const QsLog &q = qsLogs[i];
    Serial.printf("QS:LOG,%lu,%d,%d,%d,%lu\n", (unsigned long)q.id, q.rounds, q.secs, q.blow, (unsigned long)q.epoch);
  }
}

void addLog(int rounds, uint32_t secs, int blow)
{
  if (qsCount >= QS_MAX)   // เต็ม => ทิ้งอันเก่าสุด
  {
    for (int i = 1; i < QS_MAX; i++) qsLogs[i - 1] = qsLogs[i];
    qsCount = QS_MAX - 1;
  }
  QsLog q = { qsNextId++, (uint8_t)rounds, (uint16_t)min<uint32_t>(secs, 65535), (int8_t)blow, nowEpoch(), bootId, millis() };
  qsLogs[qsCount++] = q;
  saveLogs();
  sendLogs();
}

void ackLog(uint32_t id)
{
  for (int i = 0; i < qsCount; i++)
    if (qsLogs[i].id == id)
    {
      for (int j = i + 1; j < qsCount; j++) qsLogs[j - 1] = qsLogs[j];
      qsCount--;
      saveLogs();
      Serial.printf("# acked %lu, %d left\n", (unsigned long)id, qsCount);
      return;
    }
}

/// ได้เวลาจริงจากเกม => เติมเวลาให้ผลที่ฝึกในรอบเปิดเครื่องนี้
void setTime(uint32_t unix)
{
  epochAtSync = unix;
  millisAtSync = millis();
  bool changed = false;
  for (int i = 0; i < qsCount; i++)
    if (qsLogs[i].epoch == 0 && qsLogs[i].boot == bootId)
    {
      qsLogs[i].epoch = unix - (millis() - qsLogs[i].ms) / 1000;
      changed = true;
    }
  if (changed) saveLogs();
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
  enter(ST_DONE);
  addLog(ROUNDS, secs, lastBlowPct);   // เก็บไว้ + ส่งทันที (ถ้าเสียบเกมอยู่ เกมจะตอบ ACK)
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

/// เดินเวลาแต่ละเฟส (ไม่วาด)
void updateState(uint32_t dt)
{
  uint32_t now = millis();
  uint32_t el = now - stateAt;
  switch (state)
  {
  case ST_READY:  if (el >= READY_S * 1000UL) enter(ST_INHALE); break;
  case ST_INHALE: if (el >= INHALE_S * 1000UL) enter(ST_HOLD); break;
  case ST_HOLD:   if (el >= HOLD_S * 1000UL) enter(ST_EXHALE); break;
  case ST_EXHALE:
    if (el >= EXHALE_S * 1000UL)
    {
      round_++;
      if (round_ >= ROUNDS) finishSession();
      else enter(ST_INHALE);
    }
    else
    {
      exhaleMs += dt;
      if (level >= BLOW_MIN) blowMs += dt;
    }
    break;
  case ST_DONE:    if (el > DONE_SCREEN_MS) enter(ST_IDLE); break;
  case ST_STOPPED: if (el > 1500) enter(ST_IDLE); break;
  default: break;
  }
}

/// วาดหน้าจอตามสถานะปัจจุบัน (ถูกเรียกครั้งละแถบ)
void drawCurrent(uint32_t now)
{
  float t = (now - stateAt) / 1000.f;
  int lv = (int)(level + 0.5f);
  switch (state)
  {
  case ST_CAL:     ui::drawCalibrating(now); break;
  case ST_IDLE:    ui::drawIdle(now, lv, sessionsDone); break;
  case ST_READY:   ui::drawReady(now, max(1, READY_S - (int)t), min(1.f, t / READY_S)); break;
  case ST_INHALE:  ui::drawPhase(now, ui::PH_INHALE, min(1.f, t / INHALE_S), max(1, INHALE_S - (int)t), round_, ROUNDS, lv, false); break;
  case ST_HOLD:    ui::drawPhase(now, ui::PH_HOLD, min(1.f, t / HOLD_S), max(1, HOLD_S - (int)t), round_, ROUNDS, lv, false); break;
  case ST_EXHALE:  ui::drawPhase(now, ui::PH_EXHALE, min(1.f, t / EXHALE_S), max(1, EXHALE_S - (int)t), round_, ROUNDS, lv, lv >= BLOW_MIN); break;
  case ST_DONE:    ui::drawDone(now - stateAt, lastBlowPct, sessionsDone); break;
  case ST_STOPPED: ui::drawStopped(now); break;
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
  else if (line.startsWith("TIME:"))
  {
    setTime((uint32_t)strtoul(line.c_str() + 5, nullptr, 10));
    sendLogs();
  }
  else if (line == "SYNC")
  {
    sendLogs();
  }
  else if (line.startsWith("QS:ACK,"))
  {
    ackLog((uint32_t)strtoul(line.c_str() + 7, nullptr, 10));
  }
  else if (line.startsWith("COLOR:"))
  {
    prefs.putString("color", line.substring(6));
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
  loadLogs();

  ui::begin(band, BAND_H);
  displayOk = gfx->begin(TFT_SPI_HZ);
  if (displayOk) gfx->fillScreen(0xFFFF);

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
  level += (target - level) * (target > level ? 0.85f : 0.6f);    // ขึ้นเร็ว ลงเร็ว (เป่าปุ๊บขึ้น หยุดปุ๊บลง)
  if (target == 0) baseline += (loud - baseline) * 0.01f;
  digitalWrite(LED_PIN, level > 30 ? LOW : HIGH);

  uint32_t now = millis();
  uint32_t dt = now - lastLoop;
  lastLoop = now;

  updateState(dt);

  // วาดจอทั้งจอใช้เวลา ~50ms และระหว่างนั้นอ่านไมค์ไม่ได้
  // => ตอนฝึก Quick Session วาดทุกรอบ, ตอนอื่น (เล่นเกมในคอม) วาดแค่ ~6 ครั้ง/วินาที ให้ค่าแรงเป่าส่งถี่และไม่ดีเลย์
  static uint32_t lastFrame = 0;
  bool training = state == ST_READY || state == ST_INHALE || state == ST_HOLD || state == ST_EXHALE;
  if (training || now - lastFrame >= 160)
  {
    lastFrame = now;
    showFrame();
  }

  // ข้อความตรวจสอบ (Unity ไม่สนใจบรรทัดที่ขึ้นต้นด้วย #)
  if (now - lastDiag >= 10000)
  {
    lastDiag = now;
    Serial.printf("# display=%s state=%d heap=%u\n", displayOk ? "ok" : "FAILED", (int)state, (unsigned)ESP.getFreeHeap());
  }

  if (now - lastSend >= (unsigned long)SEND_EVERY_MS)
  {
    lastSend = now;
    Serial.println((int)(level + 0.5f));
  }
  // ยังมีผลค้างส่ง => ส่งซ้ำทุก 20 วินาที (เกมที่เปิดอยู่จะบันทึกแล้วตอบ ACK)
  static unsigned long lastLogSend = 0;
  if (qsCount > 0 && now - lastLogSend >= 20000)
  {
    lastLogSend = now;
    sendLogs();
  }
  if (BAT_PIN >= 0 && now - lastBat >= 30000)
  {
    lastBat = now;
    Serial.printf("BAT:%d\n", readBatteryPercent());
  }
}
