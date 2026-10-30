// =====================================================================
// Breez UI สำหรับจอกลม GC9A01 240x240
//   วาดทุกอย่างลง framebuffer (RGB565) ก่อน แล้วส่งขึ้นจอทีเดียว => ไม่กระพริบ
//   ขอบวงกลม / วงแหวน / ตัวหนังสือ เรียบ (anti-aliased) ใช้ตารางระยะทางแทน float
//   ไฟล์นี้ไม่ผูกกับ Arduino (ทดสอบ/พรีวิวบนคอมได้)
//   ธีม: ขาว · เขียวอ่อน · เหลืองอ่อน (เหมือนในเกม)
// =====================================================================
#pragma once
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <math.h>
#include "ui_text.h"

#ifndef pgm_read_byte
#define pgm_read_byte(p) (*(const uint8_t *)(p))
#endif

namespace ui {

static const int W = 240, H = 240, C = 120;

// ---------- สี ----------
constexpr uint16_t rgb(uint32_t hex)
{
  return (uint16_t)(((hex >> 8) & 0xF800) | ((hex >> 5) & 0x07E0) | ((hex >> 3) & 0x001F));
}
static const uint16_t BG          = rgb(0xFBFCF4);   // ขาวอมครีม
static const uint16_t TRACK       = rgb(0xECF0DF);
static const uint16_t INK         = rgb(0x2E3B35);   // สีตัวหนังสือเดียวกับเกม
static const uint16_t MUTED       = rgb(0x7D8B7F);
static const uint16_t GREEN_DARK  = rgb(0x5E7D45);
static const uint16_t SAGE        = rgb(0xA9C77E);
static const uint16_t GREEN       = rgb(0xD4E59D);   // เขียวอ่อน (ปุ่ม Home ในเกม)
static const uint16_t GREEN_PALE  = rgb(0xF1F7DE);
static const uint16_t YELLOW      = rgb(0xF6E3A0);   // เหลืองอ่อน
static const uint16_t YELLOW_PALE = rgb(0xFFF9E3);
static const uint16_t GOLD        = rgb(0xE9C55A);
static const uint16_t WHITE       = rgb(0xFFFFFF);

// วาดเป็นแถบ (band) ทีละ BAND แถว แล้วส่งขึ้นจอ => ใช้ RAM น้อย (ไม่ต้องจองทั้งจอ 115KB)
static uint16_t *fb = nullptr;           // บัฟเฟอร์ของแถบปัจจุบัน (W * (bandY1-bandY0))
static int bandY0 = 0, bandY1 = 240;
static uint16_t DIST[120 * 120];         // ระยะจากจุดกลาง หน่วย 1/16 พิกเซล (1 ควอดแรนต์)
static uint8_t ANG[120 * 120];           // มุมในควอดแรนต์ 0..255 = 0..90°

/// a = 0..32
static inline uint16_t blend(uint16_t fg, uint16_t bg, uint32_t a)
{
  if (a >= 32) return fg;
  if (a == 0) return bg;
  uint32_t f = (fg | ((uint32_t)fg << 16)) & 0x07E0F81F;
  uint32_t b = (bg | ((uint32_t)bg << 16)) & 0x07E0F81F;
  uint32_t r = ((((f - b) * a) >> 5) + b) & 0x07E0F81F;
  return (uint16_t)(r | (r >> 16));
}
static inline uint16_t mix(uint16_t a, uint16_t b, int t32) { return blend(b, a, (uint32_t)(t32 < 0 ? 0 : t32 > 32 ? 32 : t32)); }

static inline uint16_t &px(int x, int y) { return fb[(y - bandY0) * W + x]; }

static inline void put(int x, int y, uint16_t c, uint32_t a)
{
  if ((unsigned)x >= (unsigned)W || y < bandY0 || y >= bandY1 || a == 0) return;
  uint16_t &p = px(x, y);
  p = blend(c, p, a);
}

static inline int clampi(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }

// ---------- เตรียมตาราง ----------
/// ตั้งแถบที่จะวาด: buf ขนาด W*h, ครอบคลุมแถว y0..y0+h-1
static void setBand(uint16_t *buf, int y0, int h)
{
  fb = buf;
  bandY0 = y0;
  bandY1 = y0 + h > H ? H : y0 + h;
}

static void begin(uint16_t *framebuffer, int bandHeight = 240)
{
  setBand(framebuffer, 0, bandHeight);
  for (int y = 0; y < 120; y++)
    for (int x = 0; x < 120; x++)
    {
      float fx = x + 0.5f, fy = y + 0.5f;
      DIST[y * 120 + x] = (uint16_t)(sqrtf(fx * fx + fy * fy) * 16.0f + 0.5f);
      int a = (int)(atan2f(fx, fy) * 256.0f / 1.5707963f);
      ANG[y * 120 + x] = (uint8_t)clampi(a, 0, 255);
    }
}

static inline int distAt(int X, int Y)
{
  int qx = X < C ? (C - 1 - X) : (X - C);
  int qy = Y < C ? (C - 1 - Y) : (Y - C);
  return DIST[qy * 120 + qx];
}

/// มุมตามเข็มนาฬิกาจากด้านบน 0..1023
static inline int angleAt(int X, int Y)
{
  int qx = X < C ? (C - 1 - X) : (X - C);
  int qy = Y < C ? (C - 1 - Y) : (Y - C);
  int a = ANG[qy * 120 + qx];
  if (X >= C) return Y < C ? a : 512 - a;
  return Y >= C ? 512 + a : (1024 - a) & 1023;
}

// ---------- รูปทรง ----------
static void clear(uint16_t c)
{
  int n = W * (bandY1 - bandY0);
  for (int i = 0; i < n; i++) fb[i] = c;
}

/// วงกลมตรงกลางจอ ไล่สีจากกลาง (cin) ไปขอบ (cout)  r = รัศมีพิกเซล
static void disc(float r, uint16_t cin, uint16_t cout)
{
  int R = (int)(r * 16.0f);
  if (R <= 0) return;
  int y0 = clampi(C - (int)r - 2, 0, H - 1), y1 = clampi(C + (int)r + 2, 0, H - 1);
  int ys = y0 < bandY0 ? bandY0 : y0, ye = y1 > bandY1 - 1 ? bandY1 - 1 : y1;
  for (int y = ys; y <= ye; y++)
    for (int x = y0; x <= y1; x++)
    {
      int d = distAt(x, y);
      if (d > R + 8) continue;
      int a = clampi((R - d + 8) * 2, 0, 32);
      uint16_t col = mix(cin, cout, d * 32 / R);
      uint16_t &p = px(x, y);
      p = blend(col, p, a);
    }
}

/// แสงฟุ้งรอบวงกลม (กว้าง w พิกเซล) strength 0..32
static void halo(float r, float w, uint16_t color, int strength)
{
  int R = (int)(r * 16.0f), Wd = (int)(w * 16.0f);
  if (strength <= 0 || Wd <= 0) return;
  int lim = (int)(r + w) + 2;
  int y0 = clampi(C - lim, 0, H - 1), y1 = clampi(C + lim, 0, H - 1);
  int ys = y0 < bandY0 ? bandY0 : y0, ye = y1 > bandY1 - 1 ? bandY1 - 1 : y1;
  for (int y = ys; y <= ye; y++)
    for (int x = y0; x <= y1; x++)
    {
      int d = distAt(x, y);
      if (d < R - 8 || d > R + Wd) continue;
      int f = clampi((R + Wd - d) * 32 / Wd, 0, 32);
      int a = (f * f >> 5) * strength >> 5;
      uint16_t &p = px(x, y);
      p = blend(color, p, (uint32_t)a);
    }
}

/// วงแหวนรัศมี rIn..rOut  ช่วงมุม from..to (0..1 = รอบวง เริ่มด้านบน ตามเข็มนาฬิกา)
static void ring(float rIn, float rOut, uint16_t color, float from = 0.f, float to = 1.f)
{
  int Ri = (int)(rIn * 16.0f), Ro = (int)(rOut * 16.0f);
  bool full = to - from >= 0.999f;
  int a0 = (int)(from * 1024.0f), a1 = (int)(to * 1024.0f);
  if (!full && a1 <= a0) return;
  int lim = (int)rOut + 2;
  int y0 = clampi(C - lim, 0, H - 1), y1 = clampi(C + lim, 0, H - 1);
  int ys = y0 < bandY0 ? bandY0 : y0, ye = y1 > bandY1 - 1 ? bandY1 - 1 : y1;
  for (int y = ys; y <= ye; y++)
    for (int x = y0; x <= y1; x++)
    {
      int d = distAt(x, y);
      if (d < Ri - 8 || d > Ro + 8) continue;
      if (!full)
      {
        int ang = angleAt(x, y);
        // ช่วงมุมอาจเกิน 1024 (วนรอบ)
        if (!((ang >= a0 && ang < a1) || (ang + 1024 >= a0 && ang + 1024 < a1))) continue;
      }
      int a = clampi((d - Ri + 8) * 2, 0, 32);
      int b = clampi((Ro - d + 8) * 2, 0, 32);
      uint16_t &p = px(x, y);
      p = blend(color, p, (uint32_t)(a < b ? a : b));
    }
}

/// วงกลมเล็กที่ตำแหน่งใดก็ได้
static void dot(float cx, float cy, float r, uint16_t color, int alpha = 32)
{
  int x0 = (int)(cx - r - 1), x1 = (int)(cx + r + 1), y0 = (int)(cy - r - 1), y1 = (int)(cy + r + 1);
  for (int y = y0; y <= y1; y++)
    for (int x = x0; x <= x1; x++)
    {
      float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
      float cov = r - sqrtf(dx * dx + dy * dy) + 0.5f;
      if (cov <= 0.f) continue;
      int a = cov >= 1.f ? 32 : (int)(cov * 32.f);
      put(x, y, color, (uint32_t)(a * alpha >> 5));
    }
}

/// จุดบนเส้นรอบวง ที่ตำแหน่ง frac (0..1 เริ่มด้านบน ตามเข็มนาฬิกา)
static void dotOnRing(float radius, float frac, float r, uint16_t color)
{
  float t = frac * 6.2831853f;
  dot(C + sinf(t) * radius, C - cosf(t) * radius, r, color);
}

/// ปุ่มแคปซูล (ขอบมน) กึ่งกลางที่ cx,cy
static void pill(float cx, float cy, float w, float h, uint16_t color)
{
  float r = h * 0.5f, half = w * 0.5f - r;
  int x0 = (int)(cx - w / 2 - 1), x1 = (int)(cx + w / 2 + 1), y0 = (int)(cy - r - 1), y1 = (int)(cy + r + 1);
  for (int y = y0; y <= y1; y++)
    for (int x = x0; x <= x1; x++)
    {
      float px = x + 0.5f - cx, py = y + 0.5f - cy;
      float qx = fabsf(px) - half;
      if (qx < 0) qx = 0;
      float cov = r - sqrtf(qx * qx + py * py) + 0.5f;
      if (cov <= 0.f) continue;
      put(x, y, color, cov >= 1.f ? 32 : (uint32_t)(cov * 32.f));
    }
}

// ---------- ตัวหนังสือ (bitmap จาก ui_text.h) ----------
static void blit(const UiBitmap &b, int x0, int y0, uint16_t color, int alpha = 32)
{
  int stride = (b.w + 1) / 2;
  for (int y = 0; y < b.h; y++)
  {
    int Y = y0 + y;
    if (Y < bandY0 || Y >= bandY1) continue;
    for (int x = 0; x < b.w; x++)
    {
      uint8_t v = pgm_read_byte(&b.data[y * stride + (x >> 1)]);
      int a4 = (x & 1) ? (v & 15) : (v >> 4);
      if (!a4) continue;
      int a = (a4 * 32 + 7) / 15;
      put(x0 + x, Y, color, (uint32_t)(a * alpha >> 5));
    }
  }
}

/// ข้อความ จัดกึ่งกลางที่ cx,cy
static void text(const UiBitmap &b, int cx, int cy, uint16_t color, int alpha = 32)
{
  blit(b, cx + b.dx, cy + b.dy, color, alpha);
}

static int glyphIndex(const char *chars, char ch)
{
  const char *p = strchr(chars, ch);
  return p ? (int)(p - chars) : -1;
}

static int numberWidth(const UiBitmap *set, const char *chars, const char *s)
{
  int w = 0;
  for (; *s; s++)
  {
    int i = glyphIndex(chars, *s);
    w += i < 0 ? 4 : set[i].adv;
  }
  return w;
}

/// ตัวเลข/ข้อความสั้นจากชุดตัวอักษร ชิดซ้ายที่ x (y = กึ่งกลางแนวตั้ง) คืนค่าความกว้าง
static int numberAt(const UiBitmap *set, const char *chars, const char *s, int x, int cy, uint16_t color, int alpha = 32)
{
  int start = x;
  for (; *s; s++)
  {
    int i = glyphIndex(chars, *s);
    if (i < 0) { x += 4; continue; }
    blit(set[i], x + set[i].dx, cy + set[i].dy, color, alpha);
    x += set[i].adv;
  }
  return x - start;
}

static void number(const UiBitmap *set, const char *chars, const char *s, int cx, int cy, uint16_t color, int alpha = 32)
{
  numberAt(set, chars, s, cx - numberWidth(set, chars, s) / 2, cy, color, alpha);
}

static void bigNumber(int v, int cx, int cy, uint16_t color, int alpha = 32)
{
  char buf[8];
  snprintf(buf, sizeof buf, "%d", v);
  number(DIG_BIG, DIG_BIG_CHARS, buf, cx, cy, color, alpha);
}

/// ข้อความ + ตัวเลข ต่อกันในบรรทัดเดียว จัดกึ่งกลาง เช่น "ฝึกแล้ว 12 ครั้ง"
static void labelNumberLabel(const UiBitmap *before, const char *num, const UiBitmap *after, int cx, int cy, uint16_t color, uint16_t numColor)
{
  const int gap = 5;
  int wn = numberWidth(DIG_SMALL, DIG_SMALL_CHARS, num);
  int total = (before ? before->w + gap : 0) + wn + (after ? gap + after->w : 0);
  int x = cx - total / 2;
  if (before) { blit(*before, x, cy + before->dy, color); x += before->w + gap; }
  numberAt(DIG_SMALL, DIG_SMALL_CHARS, num, x, cy, numColor);
  x += wn;
  if (after) { x += gap; blit(*after, x, cy + after->dy, color); }
}

static float easeInOut(float t)
{
  if (t <= 0.f) return 0.f;
  if (t >= 1.f) return 1.f;
  return 0.5f - 0.5f * cosf(t * 3.1415927f);
}

// =====================================================================
// หน้าจอ
// =====================================================================
static const float RING_IN = 108.f, RING_OUT = 116.f;
static const float DISC_MIN = 46.f, DISC_MAX = 88.f;

enum Phase { PH_INHALE = 0, PH_HOLD = 1, PH_EXHALE = 2 };

struct PhaseStyle { uint16_t discIn, discOut, arc, glow; const UiBitmap *label, *sub; };

static PhaseStyle styleFor(int phase)
{
  switch (phase)
  {
  case PH_INHALE: return { GREEN_PALE, GREEN, SAGE, GREEN, &TXT_IN, &TXT_IN_SUB };
  case PH_HOLD:   return { YELLOW_PALE, YELLOW, GOLD, YELLOW, &TXT_HOLD, &TXT_HOLD_SUB };
  default:        return { WHITE, rgb(0xDCEBB8), rgb(0x8DB866), YELLOW, &TXT_OUT, &TXT_OUT_SUB };
  }
}

/// หน้าแรก: โลโก้ + วงกลมหายใจเบาๆ + วงแหวนแสดงแรงเป่าสด + ปุ่มเริ่ม
static void drawIdle(uint32_t ms, int level, int sessionsDone)
{
  clear(BG);
  ring(RING_IN, RING_OUT, TRACK);
  if (level > 0) ring(RING_IN, RING_OUT, SAGE, 0.f, level / 100.f);

  float breathe = 0.5f - 0.5f * cosf((ms % 6000) / 6000.f * 6.2831853f);
  float r = 60.f + 5.f * breathe + level * 0.18f;
  halo(r, 14.f, YELLOW, 18 + (int)(10 * breathe));
  disc(r, GREEN_PALE, GREEN);

  text(TXT_BREEZ, C, 98, GREEN_DARK);
  text(TXT_QS_TITLE, C, 132, INK);
  text(TXT_QS_478, C, 153, MUTED);

  pill(C, 199, 128, 30, YELLOW);
  text(TXT_PRESS, C, 199, INK);

  if (sessionsDone > 0)
  {
    char buf[8];
    snprintf(buf, sizeof buf, "%d", sessionsDone);
    labelNumberLabel(&TXT_TOTAL, buf, &TXT_TIMES, C, 37, MUTED, GREEN_DARK);
  }
}

/// กำลังคาลิเบรตไมค์
static void drawCalibrating(uint32_t ms)
{
  clear(BG);
  ring(RING_IN, RING_OUT, TRACK);
  float head = (ms % 1400) / 1400.f;
  ring(RING_IN, RING_OUT, SAGE, head, head + 0.22f);
  disc(58.f, GREEN_PALE, GREEN);
  text(TXT_CAL, C, 108, INK);
  text(TXT_CAL_SUB, C, 136, MUTED);
}

/// นับถอยหลังก่อนเริ่ม 3 2 1
static void drawReady(int secondsLeft, float frac)
{
  clear(BG);
  ring(RING_IN, RING_OUT, TRACK);
  ring(RING_IN, RING_OUT, SAGE, 0.f, 1.f - frac);
  float pop = 1.f - (frac * 3.f - (int)(frac * 3.f));   // เด้งทุกวินาที
  halo(DISC_MIN + 4.f, 12.f, YELLOW, 14 + (int)(10 * pop));
  disc(DISC_MIN + 4.f, GREEN_PALE, GREEN);
  text(TXT_READY, C, 50, INK);
  bigNumber(secondsLeft, C, 122, INK);
  text(TXT_READY_SUB, C, 196, MUTED);
}

/// ระหว่างฝึก 4-7-8
///   phaseFrac 0..1, secondsLeft ในเฟสนี้, round 0-based, level = แรงเป่า 0..100
static void drawPhase(int phase, float phaseFrac, int secondsLeft, int round, int rounds, int level, bool blowing)
{
  PhaseStyle s = styleFor(phase);
  clear(BG);

  // วงแหวนความคืบหน้าของเฟส + จุดหัว
  ring(RING_IN, RING_OUT, TRACK);
  if (phaseFrac > 0.005f)
  {
    ring(RING_IN, RING_OUT, s.arc, 0.f, phaseFrac);
    dotOnRing((RING_IN + RING_OUT) * 0.5f, 0.f, (RING_OUT - RING_IN) * 0.5f, s.arc);
    dotOnRing((RING_IN + RING_OUT) * 0.5f, phaseFrac, (RING_OUT - RING_IN) * 0.5f + 1.5f, s.arc);
  }

  // วงกลมหายใจ: เข้า = ขยาย, กลั้น = ค้าง (ขยับเบาๆ), ออก = หด
  float r;
  if (phase == PH_INHALE) r = DISC_MIN + (DISC_MAX - DISC_MIN) * easeInOut(phaseFrac);
  else if (phase == PH_HOLD) r = DISC_MAX - 1.5f + 1.5f * sinf(phaseFrac * 7.f * 3.1415927f);
  else r = DISC_MAX - (DISC_MAX - DISC_MIN) * easeInOut(phaseFrac);

  int glow = 16;
  if (phase == PH_EXHALE) glow = blowing ? 18 + level * 14 / 100 : 10;   // เป่าอยู่ => แสงฟุ้งขึ้น
  halo(r, phase == PH_EXHALE && blowing ? 16.f : 11.f, s.glow, glow);
  disc(r, s.discIn, s.discOut);

  text(*s.label, C, 82, INK);
  bigNumber(secondsLeft, C, 126, INK);
  text(*s.sub, C, 166, phase == PH_EXHALE && blowing ? GREEN_DARK : MUTED);

  // จุดบอกรอบ ● ● ○ ○
  const float gap = 15.f;
  float x0 = C - gap * (rounds - 1) * 0.5f;
  for (int i = 0; i < rounds; i++)
  {
    uint16_t c = i < round ? SAGE : (i == round ? s.arc : TRACK);
    dot(x0 + i * gap, 202.f, i == round ? 4.5f : 3.5f, c);
  }
}

/// จบครบทุกรอบ
static void drawDone(uint32_t ms, int blowPct, int sessionsDone)
{
  clear(BG);
  ring(RING_IN, RING_OUT, SAGE);
  // จุดเหลืองหมุนรอบวงแหวน (ฉลอง)
  for (int i = 0; i < 6; i++)
    dotOnRing(RING_IN - 12.f, i / 6.f + (ms % 9000) / 9000.f, i % 2 ? 3.f : 4.5f, i % 2 ? GOLD : YELLOW);
  halo(78.f, 12.f, YELLOW, 22);
  disc(78.f, GREEN_PALE, GREEN);

  text(TXT_DONE, C, 92, GREEN_DARK);
  text(TXT_DONE_SUB, C, 124, INK);
  if (blowPct >= 0)
  {
    char buf[8];
    snprintf(buf, sizeof buf, "%d%%", blowPct);
    labelNumberLabel(&TXT_BLOW, buf, nullptr, C, 150, MUTED, GREEN_DARK);
  }
  pill(C, 199, 150, 28, YELLOW);
  text(TXT_AGAIN, C, 199, INK);
  (void)sessionsDone;
}

/// กดหยุดกลางคัน
static void drawStopped()
{
  clear(BG);
  ring(RING_IN, RING_OUT, TRACK);
  disc(60.f, YELLOW_PALE, YELLOW);
  text(TXT_STOPPED, C, 120, INK);
}

} // namespace ui
