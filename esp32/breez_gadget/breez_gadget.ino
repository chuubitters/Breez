/*
  Breez / fuuu — Breath Gadget (ESP32-C3 Super Mini + KY-037)
  ----------------------------------------------------------------
  อ่านเสียงลมเป่าจาก KY-037 แล้วส่งค่าแรงเป่า 0-100 ไปที่ Unity ผ่าน USB
  รูปแบบที่ส่ง (1 บรรทัด/ครั้ง, ~25 ครั้งต่อวินาที):
      57          ค่าแรงเป่า 0-100   (Unity: BreathReceiver.breathLevel)
      BAT:85      แบตเตอรี่ %         (ถ้าเปิดใช้ BAT_PIN)
      # ...       ข้อความดีบัก        (Unity ไม่สนใจ)
  รับจาก Unity:
      NAME:<ชื่อ>   ICON:<cat|flower|cloud|star|heart|moon>   (จำไว้ใน flash)
      CAL           คาลิเบรตใหม่ (อยู่เงียบๆ 2 วินาที)

  การต่อสาย KY-037 -> ESP32-C3 Super Mini
      +  (VCC) -> 3V3      (ห้ามใช้ 5V: ขา ADC ของ ESP32 รับได้ไม่เกิน 3.3V)
      G  (GND) -> GND
      A0 (AO)  -> GPIO3
      D0 (DO)  -> ไม่ต้องต่อ

  Arduino IDE: Board = "ESP32C3 Dev Module" (หรือ "Nologo ESP32C3 Super Mini")
               USB CDC On Boot = Enabled   <-- สำคัญ ไม่งั้น Serial ไม่ออกทาง USB
*/

#include <Preferences.h>

// ---------------- ตั้งค่าได้ ----------------
const int   MIC_PIN        = 3;     // KY-037 AO
const int   LED_PIN        = 8;     // LED บนบอร์ด Super Mini (active LOW)
const int   BAT_PIN        = -1;    // ถ้าต่อ voltage divider 1:1 เข้าขา ADC ให้ใส่เลขขา เช่น 4
const long  BAUD           = 115200;
const int   SEND_EVERY_MS  = 40;    // ส่งค่า 25 ครั้ง/วินาที
const int   WINDOW_MS      = 20;    // ช่วงเวลาวัดความดัง (peak-to-peak)

// ความไว: ค่าความดังเหนือเสียงพื้นหลังกี่หน่วย ADC ถึงจะเป็น 100
// เป่าแรงแล้วได้ไม่ถึง 100 -> ลดค่า / เป่าเบาๆ ก็ 100 -> เพิ่มค่า
int   FULL_SCALE     = 900;
int   NOISE_GATE     = 40;          // ต่ำกว่านี้ถือว่าเงียบ (ตัดเสียงรบกวน)
// --------------------------------------------

Preferences prefs;
String gadgetName = "Breath Gadget";
String gadgetIcon = "cat";

float baseline = 0;        // ความดังพื้นหลัง (ตอนไม่ได้เป่า)
float level = 0;           // ค่าที่ผ่านการ smooth แล้ว 0-100
unsigned long lastSend = 0, lastBat = 0;
String rxLine;

// วัดความดังแบบ peak-to-peak ในช่วง WINDOW_MS
int readLoudness() {
  int lo = 4095, hi = 0;
  unsigned long start = millis();
  while (millis() - start < (unsigned long)WINDOW_MS) {
    int v = analogRead(MIC_PIN);
    if (v < lo) lo = v;
    if (v > hi) hi = v;
  }
  return hi - lo;
}

void calibrate() {
  Serial.println("# calibrating, keep quiet...");
  digitalWrite(LED_PIN, LOW);
  long sum = 0;
  const int n = 60;                 // ~1.2 วินาที
  for (int i = 0; i < n; i++) sum += readLoudness();
  baseline = (float)sum / n;
  digitalWrite(LED_PIN, HIGH);
  Serial.printf("# baseline=%.0f\n", baseline);
}

void handleLine(String line) {
  line.trim();
  if (line.startsWith("NAME:")) {
    gadgetName = line.substring(5);
    prefs.putString("name", gadgetName);
    Serial.println("# name=" + gadgetName);
    // TODO: วาดชื่อบนจอ 1.28" ที่นี่ (เช่น TFT_eSPI / GC9A01)
  } else if (line.startsWith("ICON:")) {
    gadgetIcon = line.substring(5);
    prefs.putString("icon", gadgetIcon);
    Serial.println("# icon=" + gadgetIcon);
    // TODO: วาดไอคอนบนจอที่นี่
  } else if (line == "CAL") {
    calibrate();
  }
}

void readCommands() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') {
      if (rxLine.length()) handleLine(rxLine);
      rxLine = "";
    } else if (rxLine.length() < 64) {
      rxLine += c;
    }
  }
}

int readBatteryPercent() {
  int mv = analogReadMilliVolts(BAT_PIN) * 2;       // divider 1:1
  return constrain(map(mv, 3300, 4200, 0, 100), 0, 100);
}

void setup() {
  Serial.begin(BAUD);
#if ARDUINO_USB_CDC_ON_BOOT
  Serial.setTxTimeoutMs(0);         // ไม่ค้างถ้ายังไม่ได้เปิด Unity
#endif
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, HIGH);
  analogReadResolution(12);         // 0-4095 (ค่าเริ่มต้นอ่านได้ถึง ~3.1V อยู่แล้ว)

  prefs.begin("breez", false);
  gadgetName = prefs.getString("name", gadgetName);
  gadgetIcon = prefs.getString("icon", gadgetIcon);

  delay(500);
  calibrate();
}

void loop() {
  readCommands();

  int loud = readLoudness();
  float over = loud - baseline - NOISE_GATE;
  float target = over <= 0 ? 0 : min(100.0f, over * 100.0f / FULL_SCALE);

  // ขึ้นเร็ว ลงช้า ให้กราฟลื่น ไม่กระตุก
  level += (target - level) * (target > level ? 0.6f : 0.25f);

  // ปรับ baseline ช้าๆ ตอนเงียบ (รองรับห้องที่เสียงพื้นหลังเปลี่ยน)
  if (target == 0) baseline += (loud - baseline) * 0.01f;

  digitalWrite(LED_PIN, level > 30 ? LOW : HIGH);   // ไฟติดตอนเป่า

  unsigned long now = millis();
  if (now - lastSend >= (unsigned long)SEND_EVERY_MS) {
    lastSend = now;
    Serial.println((int)(level + 0.5f));
  }
  if (BAT_PIN >= 0 && now - lastBat >= 30000) {
    lastBat = now;
    Serial.printf("BAT:%d\n", readBatteryPercent());
  }
}
