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
static const uint16_t BG          = rgb(0xFFFDF6);   // ขาวครีม
static const uint16_t TRACK       = rgb(0xF0F2E4);
static const uint16_t INK         = rgb(0x2E3B35);   // สีตัวหนังสือเดียวกับเกม
static const uint16_t MUTED       = rgb(0x8A948B);
static const uint16_t LOGO_GRAY   = rgb(0x585858);   // สีโลโก้ breez
static const uint16_t GREEN_DARK  = rgb(0x5E7D45);
static const uint16_t SAGE        = rgb(0xA9C77E);
static const uint16_t GREEN       = rgb(0xD4E59D);   // เขียวอ่อน (ปุ่ม Home ในเกม)
static const uint16_t GREEN_PALE  = rgb(0xF0F6DC);
static const uint16_t LEAF        = rgb(0xB9D48C);
static const uint16_t YELLOW      = rgb(0xF6E3A0);   // เหลืองอ่อน
static const uint16_t YELLOW_PALE = rgb(0xFFF7DA);
static const uint16_t GOLD        = rgb(0xEDC965);
static const uint16_t PINK        = rgb(0xF5C6D2);   // ชมพูอ่อน (ปุ่ม Replay ในเกม)
static const uint16_t PINK_PALE   = rgb(0xFDEBF0);
static const uint16_t PINK_DEEP   = rgb(0xE79AB0);
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

/// วงกลมที่ตำแหน่งใดก็ได้ (ขอบเรียบ) — คิดรากที่สองเฉพาะพิกเซลขอบ => เร็วพอวาดกลีบดอกใหญ่ๆ
static void dot(float cx, float cy, float r, uint16_t color, int alpha = 32)
{
  if (r <= 0.f || alpha <= 0) return;
  int x0 = (int)(cx - r - 1), x1 = (int)(cx + r + 1), y0 = (int)(cy - r - 1), y1 = (int)(cy + r + 1);
  if (y0 < bandY0) y0 = bandY0;
  if (y1 > bandY1 - 1) y1 = bandY1 - 1;
  if (x0 < 0) x0 = 0;
  if (x1 > W - 1) x1 = W - 1;
  if (y0 > y1) return;
  int cx16 = (int)(cx * 16.f), cy16 = (int)(cy * 16.f), r16 = (int)(r * 16.f);
  int inner = r16 - 8, outer = r16 + 8;
  int32_t in2 = inner > 0 ? (int32_t)inner * inner : 0, out2 = (int32_t)outer * outer;
  for (int y = y0; y <= y1; y++)
  {
    int dy = y * 16 + 8 - cy16;
    int32_t dy2 = (int32_t)dy * dy;
    if (dy2 >= out2) continue;
    for (int x = x0; x <= x1; x++)
    {
      int dx = x * 16 + 8 - cx16;
      int32_t d2 = (int32_t)dx * dx + dy2;
      if (d2 >= out2) continue;
      int a;
      if (d2 <= in2) a = 32;
      else a = clampi((outer - (int)sqrtf((float)d2)) * 2, 0, 32);
      uint16_t &p = px(x, y);
      p = blend(color, p, (uint32_t)(a * alpha >> 5));
    }
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
  if (y0 < bandY0) y0 = bandY0;
  if (y1 > bandY1 - 1) y1 = bandY1 - 1;
  for (int y = y0; y <= y1; y++)
    for (int x = x0; x <= x1; x++)
    {
      float px_ = x + 0.5f - cx, py = y + 0.5f - cy;
      float qx = fabsf(px_) - half;
      if (qx < 0) qx = 0;
      float d2 = qx * qx + py * py;
      if (d2 > (r + 0.5f) * (r + 0.5f)) continue;
      float cov = d2 < (r - 0.5f) * (r - 0.5f) ? 1.f : r - sqrtf(d2) + 0.5f;
      put(x, y, color, cov >= 1.f ? 32 : (uint32_t)(cov * 32.f));
    }
}

// ---------- ดอกไม้ ----------
/// ดอกไม้ 5-6 กลีบ: กลีบ = วงกลมเรียงรอบจุดกลาง  open 0..1 = บานแค่ไหน  rot = หมุน (รอบ)
static void flower(float cx, float cy, float size, uint16_t petal, uint16_t center, int petals = 5, float rot = 0.f, float open = 1.f, int alpha = 32)
{
  float pr = size * (0.34f + 0.12f * open);       // รัศมีกลีบ
  float pd = size * (0.30f + 0.32f * open);       // ระยะกลีบจากจุดกลาง
  for (int i = 0; i < petals; i++)
  {
    float a = (rot + (float)i / petals) * 6.2831853f;
    dot(cx + sinf(a) * pd, cy - cosf(a) * pd, pr, petal, alpha);
  }
  dot(cx, cy, size * 0.30f, center, alpha);
}

/// ใบไม้เล็ก (2 วงกลมซ้อนกัน) ชี้ไปทางมุม ang (รอบ)
static void leaf(float cx, float cy, float len, float ang, uint16_t color)
{
  float a = ang * 6.2831853f, sx = sinf(a), sy = -cosf(a);
  dot(cx + sx * len * 0.35f, cy + sy * len * 0.35f, len * 0.30f, color);
  dot(cx + sx * len * 0.65f, cy + sy * len * 0.65f, len * 0.22f, color);
}

/// ช่อดอกไม้ประดับขอบจอ ตำแหน่ง frac บนวงกลมรัศมี radius
static void wreathFlower(float radius, float frac, float size, uint16_t petal, uint16_t center, float rot, float sway)
{
  float t = frac * 6.2831853f;
  float x = C + sinf(t) * radius, y = C - cosf(t) * radius;
  leaf(x, y, size * 1.5f, frac + 0.22f + sway, LEAF);
  leaf(x, y, size * 1.3f, frac - 0.22f - sway, LEAF);
  flower(x, y, size, petal, center, 5, rot, 1.f);
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
// หน้าจอ  (ธีม: ครีม · เขียวอ่อน · เหลืองอ่อน · ชมพูอ่อน + ดอกไม้ + โลโก้ breez)
// =====================================================================
static const float RING_IN = 109.f, RING_OUT = 115.f;

enum Phase { PH_INHALE = 0, PH_HOLD = 1, PH_EXHALE = 2 };

struct PhaseStyle { uint16_t petal, petalPale, center, arc; const UiBitmap *label, *sub; };

static PhaseStyle styleFor(int phase)
{
  switch (phase)
  {
  case PH_INHALE: return { GREEN, GREEN_PALE, YELLOW_PALE, SAGE, &TXT_IN, &TXT_IN_SUB };
  case PH_HOLD:   return { YELLOW, YELLOW_PALE, WHITE, GOLD, &TXT_HOLD, &TXT_HOLD_SUB };
  default:        return { PINK, PINK_PALE, YELLOW_PALE, PINK_DEEP, &TXT_OUT, &TXT_OUT_SUB };
  }
}

/// พื้นหลัง + วงแหวน + ช่อดอกไม้มุมซ้ายล่าง/ขวาบน
static void backdrop(uint32_t ms, bool flowers = true)
{
  clear(BG);
  ring(RING_IN, RING_OUT, TRACK);
  if (!flowers) return;
  float sway = 0.015f * sinf((ms % 5000) / 5000.f * 6.2831853f);
  float rot = (ms % 30000) / 30000.f;
  wreathFlower(101.f, 0.125f, 11.f, PINK, YELLOW, rot, sway);          // ขวาบน
  dotOnRing(100.f, 0.17f, 3.f, YELLOW);
  dotOnRing(100.f, 0.08f, 2.5f, GREEN);
  wreathFlower(101.f, 0.625f, 11.f, YELLOW, PINK, -rot, -sway);        // ซ้ายล่าง
  dotOnRing(100.f, 0.67f, 3.f, PINK);
  dotOnRing(100.f, 0.58f, 2.5f, GREEN);
}

/// ดอกไม้หายใจตรงกลาง: กลีบ 6 กลีบ บาน/หุบตาม open (0..1)
static void breathFlower(float open, uint16_t petal, uint16_t petalPale, uint16_t center, float rot, int glowAlpha = 0, uint16_t glow = YELLOW)
{
  float size = 52.f + 34.f * open;                // ทั้งดอกกว้าง ~ size*1.1
  if (glowAlpha > 0) halo(size * 0.95f, 14.f, glow, glowAlpha);
  // กลีบชั้นหลัง (สีอ่อน เยื้อง) + ชั้นหน้า
  float pr = size * (0.36f + 0.10f * open), pd = size * (0.34f + 0.26f * open);
  for (int i = 0; i < 6; i++)
  {
    float a = (rot + (i + 0.5f) / 6.f) * 6.2831853f;
    dot(C + sinf(a) * pd * 1.02f, C - cosf(a) * pd * 1.02f, pr * 0.92f, petalPale);
  }
  for (int i = 0; i < 6; i++)
  {
    float a = (rot + (float)i / 6.f) * 6.2831853f;
    dot(C + sinf(a) * pd, C - cosf(a) * pd, pr, petal);
  }
  // ใจกลางดอก
  disc(size * 0.52f, WHITE, center);
}

/// กลีบดอกปลิว (ตอนเป่า / ตอนจบ)
static void flyingPetals(uint32_t ms, float fromR, int count, int strength, const uint16_t *colors, int ncolors)
{
  for (int i = 0; i < count; i++)
  {
    uint32_t cycle = 1800 + (i * 337) % 900;
    float t = ((ms + i * 613) % cycle) / (float)cycle;               // 0..1
    float ang = (i * 0.618f + t * 0.08f) * 6.2831853f;
    float d = fromR + t * (104.f - fromR);
    int a = (int)((1.f - t) * strength);
    dot(C + sinf(ang) * d, C - cosf(ang) * d, 3.5f - t * 1.5f, colors[i % ncolors], a);
  }
}

/// หน้าแรก: โลโก้ breez + ดอกไม้ + วงแหวนแรงเป่าสด + ปุ่มเริ่ม
static void drawIdle(uint32_t ms, int level, int sessionsDone)
{
  backdrop(ms);
  if (level > 0)
  {
    ring(RING_IN, RING_OUT, SAGE, 0.f, level / 100.f);
    dotOnRing((RING_IN + RING_OUT) * 0.5f, level / 100.f, 4.5f, PINK_DEEP);
  }

  float breathe = 0.5f - 0.5f * cosf((ms % 6000) / 6000.f * 6.2831853f);
  // ดอกไม้ด้านบน: หายใจช้าๆ และบานขึ้นเมื่อเป่า
  float open = 0.55f + 0.2f * breathe + level * 0.004f;
  float rot = (ms % 40000) / 40000.f;
  dot(C, 52, 30.f + 6.f * open + level * 0.08f, YELLOW_PALE);
  leaf(C - 6, 60, 30.f, 0.68f, LEAF);
  leaf(C + 6, 60, 30.f, 0.32f, LEAF);
  // ดอกไม้กลาง: ใช้ dot วาดที่ y=56 (ไม่ใช่กลางจอ) => วาดเอง
  {
    float size = 20.f + 6.f * open;
    float pr = size * (0.34f + 0.12f * open), pd = size * (0.30f + 0.32f * open);
    for (int i = 0; i < 5; i++)
    {
      float an = (rot + (i + 0.5f) / 5.f) * 6.2831853f;
      dot(C + sinf(an) * pd, 52 - cosf(an) * pd, pr * 0.85f, PINK_PALE);
    }
    flower(C, 52, size, PINK, YELLOW, 5, rot, open);
  }

  text(TXT_LOGO, C, 106, LOGO_GRAY);
  text(TXT_QS_LINE, C, 140, GREEN_DARK);

  pill(C, 176, 132, 32, PINK);
  text(TXT_PRESS, C, 176, INK);

  if (sessionsDone > 0)
  {
    char buf[8];
    snprintf(buf, sizeof buf, "%d", sessionsDone);
    labelNumberLabel(&TXT_TOTAL, buf, &TXT_TIMES, C, 207, MUTED, GREEN_DARK);
  }
}

/// กำลังคาลิเบรตไมค์
static void drawCalibrating(uint32_t ms)
{
  backdrop(ms);
  float head = (ms % 1400) / 1400.f;
  ring(RING_IN, RING_OUT, PINK, head, head + 0.22f);
  dotOnRing((RING_IN + RING_OUT) * 0.5f, head + 0.22f, 4.5f, PINK_DEEP);
  text(TXT_LOGO_SM, C, 76, LOGO_GRAY);
  text(TXT_CAL, C, 120, INK);
  text(TXT_CAL_SUB, C, 148, MUTED);
}

/// นับถอยหลังก่อนเริ่ม 3 2 1 (ดอกตูมรอบาน)
static void drawReady(uint32_t ms, int secondsLeft, float frac)
{
  backdrop(ms, false);
  ring(RING_IN, RING_OUT, PINK, 0.f, 1.f - frac);
  float beat = frac * 3.f - (int)(frac * 3.f);
  float pop = 1.f - beat;
  breathFlower(0.05f + 0.05f * pop, PINK, PINK_PALE, YELLOW_PALE, (ms % 30000) / 30000.f, 10 + (int)(10 * pop), YELLOW);
  text(TXT_READY, C, 44, INK);
  bigNumber(secondsLeft, C, 122, INK);
  text(TXT_READY_SUB, C, 198, MUTED);
}

/// ระหว่างฝึก 4-7-8: ดอกไม้บานตอนหายใจเข้า ค้างตอนกลั้น หุบตอนหายใจออก
static void drawPhase(uint32_t ms, int phase, float phaseFrac, int secondsLeft, int round, int rounds, int level, bool blowing)
{
  PhaseStyle s = styleFor(phase);
  backdrop(ms, false);

  // วงแหวนความคืบหน้าของเฟส
  if (phaseFrac > 0.005f)
  {
    ring(RING_IN, RING_OUT, s.arc, 0.f, phaseFrac);
    dotOnRing((RING_IN + RING_OUT) * 0.5f, 0.f, (RING_OUT - RING_IN) * 0.5f, s.arc);
    dotOnRing((RING_IN + RING_OUT) * 0.5f, phaseFrac, 5.5f, s.arc);
    dotOnRing((RING_IN + RING_OUT) * 0.5f, phaseFrac, 2.5f, WHITE);
  }

  float open;
  if (phase == PH_INHALE) open = easeInOut(phaseFrac);
  else if (phase == PH_HOLD) open = 0.97f + 0.03f * sinf(phaseFrac * 7.f * 3.1415927f);
  else open = 1.f - easeInOut(phaseFrac);

  float rot = (ms % 60000) / 60000.f + open * 0.05f;
  int glow = phase == PH_EXHALE ? (blowing ? 16 + level * 14 / 100 : 6) : 10;
  breathFlower(open, s.petal, s.petalPale, s.center, rot, glow, phase == PH_EXHALE ? PINK : YELLOW);

  // เป่าอยู่ => กลีบดอกปลิวออก
  if (phase == PH_EXHALE && blowing)
  {
    static const uint16_t cols[] = { PINK, PINK_DEEP, YELLOW, GREEN };
    flyingPetals(ms, 40.f + 40.f * open, 10, 14 + level * 18 / 100, cols, 4);
  }

  text(*s.label, C, 80, INK);
  bigNumber(secondsLeft, C, 124, INK);
  text(*s.sub, C, 164, phase == PH_EXHALE && blowing ? rgb(0xC9708A) : INK, 24);

  // ดอกไม้จิ๋วบอกรอบ ✿ ✿ ❀ ❀
  const float gap = 17.f;
  float x0 = C - gap * (rounds - 1) * 0.5f;
  for (int i = 0; i < rounds; i++)
  {
    if (i < round) flower(x0 + i * gap, 203.f, 6.f, SAGE, YELLOW, 5, 0.f, 1.f);
    else if (i == round) flower(x0 + i * gap, 203.f, 7.5f, s.arc, WHITE, 5, rot, 1.f);
    else flower(x0 + i * gap, 203.f, 5.5f, TRACK, WHITE, 5, 0.f, 0.6f);
  }
}

/// จบครบทุกรอบ: ดอกไม้บานเต็มที่ 3 สี + กลีบปลิว + โลโก้
static void drawDone(uint32_t ms, int blowPct, int sessionsDone)
{
  backdrop(ms);
  ring(RING_IN, RING_OUT, GREEN);
  static const uint16_t cols[] = { PINK, YELLOW, GREEN, PINK_DEEP };
  flyingPetals(ms, 70.f, 14, 26, cols, 4);

  float size = 84.f, rot = (ms % 60000) / 60000.f;
  float pr = size * 0.46f, pd = size * 0.60f;
  for (int i = 0; i < 6; i++)
  {
    float a = (rot + i / 6.f) * 6.2831853f;
    dot(C + sinf(a) * pd, C + 4 - cosf(a) * pd, pr, i % 3 == 0 ? PINK_PALE : i % 3 == 1 ? YELLOW_PALE : GREEN_PALE);
  }
  dot(C, C + 4, size * 0.62f, WHITE);

  text(TXT_LOGO_SM, C, 62, LOGO_GRAY);
  text(TXT_DONE, C, 104, GREEN_DARK);
  text(TXT_DONE_SUB, C, 136, INK);
  if (blowPct >= 0)
  {
    char buf[8];
    snprintf(buf, sizeof buf, "%d%%", blowPct);
    labelNumberLabel(&TXT_BLOW, buf, nullptr, C, 160, MUTED, PINK_DEEP);
  }
  pill(C, 198, 156, 28, PINK);
  text(TXT_AGAIN, C, 198, INK);
  (void)sessionsDone;
}

/// กดหยุดกลางคัน (ดอกตูม)
static void drawStopped(uint32_t ms)
{
  backdrop(ms);
  breathFlower(0.1f, YELLOW, YELLOW_PALE, WHITE, 0.f);
  text(TXT_STOPPED, C, 120, INK);
}

} // namespace ui
