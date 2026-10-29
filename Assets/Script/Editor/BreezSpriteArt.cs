using System;

// =====================================================================
// วาดรูป placeholder (หน้าอารมณ์ 5 แบบ, การ์ดมุมโค้ง, ดอกไม้, ลูกศร)
// คืนค่าเป็น RGBA float (แถวล่างขึ้นบน แบบ Texture2D)
// เปลี่ยนเป็นไอคอนจาก Figma ได้ภายหลัง แค่เปลี่ยน Sprite ใน Inspector
// =====================================================================
public static class BreezSpriteArt
{
    public struct Rgba
    {
        public float r, g, b, a;
        public Rgba(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Rgba Hex(int hex, float a = 1f) =>
            new Rgba(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, a);
    }

    delegate bool Shape(float x, float y);

    const int SS = 4; // supersampling ต่อแกน (anti-alias)

    // ---------------------------------------------------------------
    // ภาพหน้าอารมณ์
    // ---------------------------------------------------------------
    public static Rgba MoodColor(string mood)
    {
        switch (mood)
        {
            case "Lovely": return Rgba.Hex(0xF6A3B8);
            case "Happy": return Rgba.Hex(0xF8C83A);
            case "Bored": return Rgba.Hex(0xA7CB46);
            case "Angry": return Rgba.Hex(0xE5566B);
            default: return Rgba.Hex(0x7E63B4); // Stress
        }
    }

    public static float[] Face(string mood, int size)
    {
        var px = new float[size * size * 4];
        var bg = MoodColor(mood);
        var ink = new Rgba(bg.r * 0.45f, bg.g * 0.45f, bg.b * 0.45f);
        var white = new Rgba(1, 1, 1);

        Paint(px, size, (x, y) => x * x + y * y <= 0.92f * 0.92f, bg);

        switch (mood)
        {
            case "Lovely":
                Paint(px, size, (x, y) => Heart(x + 0.33f, y - 0.16f, 0.2f) || Heart(x - 0.33f, y - 0.16f, 0.2f), white);
                Paint(px, size, (x, y) => HalfDisc(x, y, 0f, -0.12f, 0.36f), white);
                break;
            case "Happy":
                Paint(px, size, (x, y) => Arc(x, y, -0.33f, 0.1f, 0.15f, 0.055f, 15, 165) || Arc(x, y, 0.33f, 0.1f, 0.15f, 0.055f, 15, 165), ink);
                Paint(px, size, (x, y) => HalfDisc(x, y, 0f, -0.08f, 0.4f), white);
                Paint(px, size, (x, y) => HalfDisc(x, y, 0f, -0.08f, 0.4f) && y > -0.08f - 0.07f, ink);
                break;
            case "Bored":
                Paint(px, size, (x, y) => Dot(x, y, -0.3f, 0.12f, 0.085f) || Dot(x, y, 0.3f, 0.12f, 0.085f), ink);
                Paint(px, size, (x, y) => Capsule(x, y, -0.22f, -0.3f, 0.22f, -0.3f, 0.055f), ink);
                break;
            case "Angry":
                Paint(px, size, (x, y) => Capsule(x, y, -0.52f, 0.38f, -0.16f, 0.2f, 0.06f) || Capsule(x, y, 0.52f, 0.38f, 0.16f, 0.2f, 0.06f), white);
                Paint(px, size, (x, y) => Dot(x, y, -0.3f, 0.02f, 0.08f) || Dot(x, y, 0.3f, 0.02f, 0.08f), white);
                Paint(px, size, (x, y) => Arc(x, y, 0f, -0.62f, 0.3f, 0.065f, 35, 145), white);
                break;
            default: // Stress
                Paint(px, size, (x, y) => Capsule(x, y, -0.5f, 0.28f, -0.2f, 0.38f, 0.05f) || Capsule(x, y, 0.5f, 0.28f, 0.2f, 0.38f, 0.05f), white);
                Paint(px, size, (x, y) => Dot(x, y, -0.3f, 0.1f, 0.075f) || Dot(x, y, 0.3f, 0.1f, 0.075f), white);
                Paint(px, size, (x, y) => Wave(x, y, -0.3f, 0.36f, 0.07f, 0.05f), white);
                break;
        }
        return px;
    }

    // ---------------------------------------------------------------
    // รูปทรงอื่นๆ
    // ---------------------------------------------------------------

    /// <summary>สี่เหลี่ยมมุมโค้งสีขาว (ใช้แบบ Sliced, border = radius)</summary>
    public static float[] RoundedRect(int size, int radius)
    {
        var px = new float[size * size * 4];
        float r = radius, s = size;
        Paint(px, size, (x, y) =>
        {
            // พิกัด pixel
            float px_ = (x + 1f) * 0.5f * s, py = (y + 1f) * 0.5f * s;
            float cx = Math.Max(r, Math.Min(s - r, px_));
            float cy = Math.Max(r, Math.Min(s - r, py));
            float dx = px_ - cx, dy = py - cy;
            return dx * dx + dy * dy <= r * r;
        }, new Rgba(1, 1, 1));
        return px;
    }

    public static float[] Circle(int size)
    {
        var px = new float[size * size * 4];
        Paint(px, size, (x, y) => x * x + y * y <= 0.96f * 0.96f, new Rgba(1, 1, 1));
        return px;
    }

    /// <summary>ดอกไม้ 5 กลีบ มีรูตรงกลาง (สีขาว ย้อมสีด้วย Image.color)</summary>
    public static float[] Flower(int size)
    {
        var px = new float[size * size * 4];
        Paint(px, size, (x, y) =>
        {
            if (x * x + y * y < 0.2f * 0.2f) return false;
            if (x * x + y * y < 0.5f * 0.5f) return true;
            for (int i = 0; i < 5; i++)
            {
                double a = Math.PI / 2 + i * 2 * Math.PI / 5;
                float cx = (float)Math.Cos(a) * 0.5f, cy = (float)Math.Sin(a) * 0.5f;
                if (Dot(x, y, cx, cy, 0.42f)) return true;
            }
            return false;
        }, new Rgba(1, 1, 1));
        return px;
    }

    /// <summary>ลูกศรชี้ซ้าย สีขาว</summary>
    public static float[] ArrowLeft(int size)
    {
        var px = new float[size * size * 4];
        Paint(px, size, (x, y) =>
            Capsule(x, y, -0.75f, 0f, 0.8f, 0f, 0.09f) ||
            Capsule(x, y, -0.75f, 0f, -0.25f, 0.5f, 0.09f) ||
            Capsule(x, y, -0.75f, 0f, -0.25f, -0.5f, 0.09f), new Rgba(1, 1, 1));
        return px;
    }

    // ---------------------------------------------------------------
    // Windy Mountain: แดนดิไลออน (สีขาว ย้อมสีด้วย SpriteRenderer.color)
    // ---------------------------------------------------------------

    /// <summary>เมล็ดแดนดิไลออน ฐานอยู่ล่างกลาง (ตั้ง pivot = 0.5, 0)</summary>
    public static float[] DandelionSeed(int size)
    {
        var px = new float[size * size * 4];
        var w = new Rgba(1, 1, 1);
        Paint(px, size, (x, y) =>
        {
            if (Ellipse(x, y, 0f, -0.9f, 0.05f, 0.1f)) return true;          // ตัวเมล็ด
            if (Capsule(x, y, 0f, -0.85f, 0f, 0.15f, 0.022f)) return true;   // ก้าน
            for (int k = 0; k < 11; k++)                                    // ขนร่ม
            {
                double a = (20 + k * 14) * Math.PI / 180;
                float tx = (float)Math.Cos(a) * 0.72f, ty = 0.15f + (float)Math.Sin(a) * 0.78f;
                if (Capsule(x, y, 0f, 0.15f, tx, ty, 0.014f)) return true;
                if (Dot(x, y, tx, ty, 0.03f)) return true;
            }
            return false;
        }, w);
        return px;
    }

    /// <summary>ก้านดอก (สี่เหลี่ยมผอมปลายมน) กว้าง x สูง</summary>
    public static float[] Rect(int width, int height, int radius)
    {
        var px = new float[width * height * 4];
        for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
            {
                float cx = Math.Max(radius, Math.Min(width - radius, i + 0.5f));
                float cy = Math.Max(radius, Math.Min(height - radius, j + 0.5f));
                float dx = i + 0.5f - cx, dy = j + 0.5f - cy;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                float a = Math.Max(0f, Math.Min(1f, radius - d + 0.5f));
                int k = (j * width + i) * 4;
                px[k] = px[k + 1] = px[k + 2] = 1f;
                px[k + 3] = radius > 0 ? a : 1f;
            }
        return px;
    }

    /// <summary>ใบแดนดิไลออนหยักๆ ฐานอยู่ล่างกลาง</summary>
    public static float[] DandelionLeaf(int size)
    {
        var px = new float[size * size * 4];
        Paint(px, size, (x, y) =>
        {
            if (y < -0.98f || y > 0.98f) return false;
            float t = (y + 1f) / 2f;                                    // 0 ฐาน -> 1 ปลาย
            float baseW = 0.3f * (float)Math.Sin(Math.PI * Math.Pow(t, 0.8));
            float saw = (float)((t * 7f) % 1.0);                         // ฟันเลื่อยชี้ลง
            float wv = baseW * (0.55f + 0.45f * saw);
            return Math.Abs(x) < wv;
        }, new Rgba(1, 1, 1));
        return px;
    }

    /// <summary>ฉากหลังภูเขาโทนพาสเทล (ไม่จัตุรัส) w x h</summary>
    public static float[] MountainBackground(int w, int h)
    {
        var px = new float[w * h * 4];
        Rgba skyTop = Rgba.Hex(0xDDEFF6), skyBottom = Rgba.Hex(0xFBF6EC);
        var layers = new[]
        {
            new { c = Rgba.Hex(0xE3DDF2), baseY = 0.46f, amp = 0.16f, f1 = 1.3f, f2 = 3.7f, seed = 0.3f },
            new { c = Rgba.Hex(0xD3E3C2), baseY = 0.32f, amp = 0.12f, f1 = 1.9f, f2 = 5.1f, seed = 1.7f },
            new { c = Rgba.Hex(0xBBD3A3), baseY = 0.17f, amp = 0.07f, f1 = 2.6f, f2 = 7.3f, seed = 4.1f },
        };
        float sunX = 0.72f, sunY = 0.7f, sunR = 0.085f;

        for (int j = 0; j < h; j++)
        {
            float v = (float)j / (h - 1);                                // 0 ล่าง -> 1 บน
            for (int i = 0; i < w; i++)
            {
                float u = (float)i / (w - 1);
                // ท้องฟ้า
                float r = Lerp(skyBottom.r, skyTop.r, v), g = Lerp(skyBottom.g, skyTop.g, v), b = Lerp(skyBottom.b, skyTop.b, v);
                // ดวงอาทิตย์นวลๆ
                float dx = (u - sunX) * w / h, dy = v - sunY;
                float sd = (float)Math.Sqrt(dx * dx + dy * dy);
                float glow = Math.Max(0f, 1f - sd / (sunR * 3.5f)) * 0.35f;
                float disc = Math.Max(0f, Math.Min(1f, (sunR - sd) * h + 0.5f));
                r = Lerp(r, 1f, glow); g = Lerp(g, 0.98f, glow); b = Lerp(b, 0.9f, glow);
                r = Lerp(r, 1f, disc * 0.9f); g = Lerp(g, 0.97f, disc * 0.9f); b = Lerp(b, 0.86f, disc * 0.9f);
                // ภูเขาแต่ละชั้น (ขอบนุ่มด้วย coverage)
                foreach (var L in layers)
                {
                    double ridge = L.baseY + L.amp * (0.6 * Math.Sin(u * Math.PI * L.f1 + L.seed) + 0.4 * Math.Sin(u * Math.PI * L.f2 + L.seed * 2.3));
                    float cov = (float)Math.Max(0, Math.Min(1, (ridge - v) * h + 0.5));
                    if (cov <= 0) continue;
                    float shade = (float)Math.Max(0, Math.Min(1, (ridge - v) * 3));   // เข้มลงด้านล่างเล็กน้อย
                    float lr = L.c.r * (1 - 0.06f * shade), lg = L.c.g * (1 - 0.06f * shade), lb = L.c.b * (1 - 0.06f * shade);
                    r = Lerp(r, lr, cov); g = Lerp(g, lg, cov); b = Lerp(b, lb, cov);
                }
                int k = (j * w + i) * 4;
                px[k] = r; px[k + 1] = g; px[k + 2] = b; px[k + 3] = 1f;
            }
        }
        return px;
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    // ---------------------------------------------------------------
    // ไอคอนอุปกรณ์ (สีขาว ย้อมสีด้วย Image.color)
    // ---------------------------------------------------------------
    public static readonly string[] GadgetIcons = { "cat", "flower", "cloud", "star", "heart", "moon" };

    public static float[] GadgetIcon(string id, int size)
    {
        var px = new float[size * size * 4];
        var w = new Rgba(1, 1, 1);
        switch (id)
        {
            case "cat":
                Paint(px, size, (x, y) =>
                    Ellipse(x, y, 0f, -0.12f, 0.62f, 0.52f) ||
                    Triangle(x, y, -0.58f, 0.05f, -0.5f, 0.68f, -0.12f, 0.3f) ||
                    Triangle(x, y, 0.58f, 0.05f, 0.5f, 0.68f, 0.12f, 0.3f) ||
                    Capsule(x, y, -0.92f, -0.1f, -0.66f, -0.1f, 0.045f) ||
                    Capsule(x, y, -0.92f, -0.3f, -0.62f, -0.28f, 0.045f) ||
                    Capsule(x, y, 0.92f, -0.1f, 0.66f, -0.1f, 0.045f) ||
                    Capsule(x, y, 0.92f, -0.3f, 0.62f, -0.28f, 0.045f), w);
                Erase(px, size, (x, y) =>
                    Capsule(x, y, -0.24f, 0.0f, -0.24f, -0.16f, 0.06f) ||
                    Capsule(x, y, 0.24f, 0.0f, 0.24f, -0.16f, 0.06f) ||
                    Ellipse(x, y, 0f, -0.3f, 0.09f, 0.065f));
                break;
            case "flower":
                return Flower(size);
            case "cloud":
                Paint(px, size, (x, y) =>
                    Dot(x, y, -0.4f, -0.12f, 0.36f) || Dot(x, y, 0.05f, 0.12f, 0.48f) ||
                    Dot(x, y, 0.5f, -0.12f, 0.34f) ||
                    (y > -0.46f && y < -0.12f && x > -0.4f && x < 0.5f), w);
                break;
            case "star":
                Paint(px, size, (x, y) => Star(x, y + 0.06f, 0.92f, 0.4f), w);
                break;
            case "heart":
                Paint(px, size, (x, y) => Heart(x, y + 0.08f, 0.62f), w);
                break;
            default: // moon
                Paint(px, size, (x, y) => Dot(x, y, 0f, 0f, 0.8f) && !Dot(x, y, 0.38f, 0.26f, 0.66f), w);
                break;
        }
        return px;
    }

    // ---------------------------------------------------------------
    // เครื่องมือวาด (พิกัด -1..1, y ชี้ขึ้น)
    // ---------------------------------------------------------------

    static void Erase(float[] px, int size, Shape inside)
    {
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                int hits = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float x = ((i + (sx + 0.5f) / SS) / size) * 2f - 1f;
                        float y = ((j + (sy + 0.5f) / SS) / size) * 2f - 1f;
                        if (inside(x, y)) hits++;
                    }
                if (hits > 0) px[(j * size + i) * 4 + 3] *= 1f - (float)hits / (SS * SS);
            }
    }

    static bool Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    static bool Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
        float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
        float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static bool Star(float x, float y, float outer, float inner)
    {
        // 10 จุดสลับรัศมี แล้วเช็คด้วย triangle fan จากจุดศูนย์กลาง
        for (int k = 0; k < 10; k++)
        {
            double a1 = Math.PI / 2 + k * Math.PI / 5, a2 = Math.PI / 2 + (k + 1) * Math.PI / 5;
            float r1 = k % 2 == 0 ? outer : inner, r2 = k % 2 == 0 ? inner : outer;
            if (Triangle(x, y, 0, 0,
                (float)Math.Cos(a1) * r1, (float)Math.Sin(a1) * r1,
                (float)Math.Cos(a2) * r2, (float)Math.Sin(a2) * r2)) return true;
        }
        return false;
    }

    static void Paint(float[] px, int size, Shape inside, Rgba c)
    {
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                int hits = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float x = ((i + (sx + 0.5f) / SS) / size) * 2f - 1f;
                        float y = ((j + (sy + 0.5f) / SS) / size) * 2f - 1f;
                        if (inside(x, y)) hits++;
                    }
                if (hits == 0) continue;

                float cov = (float)hits / (SS * SS) * c.a;
                int k = (j * size + i) * 4;
                float da = px[k + 3];
                float outA = cov + da * (1 - cov);
                if (outA <= 0) continue;
                px[k + 0] = (c.r * cov + px[k + 0] * da * (1 - cov)) / outA;
                px[k + 1] = (c.g * cov + px[k + 1] * da * (1 - cov)) / outA;
                px[k + 2] = (c.b * cov + px[k + 2] * da * (1 - cov)) / outA;
                px[k + 3] = outA;
            }
    }

    static bool Dot(float x, float y, float cx, float cy, float r) =>
        (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

    static bool HalfDisc(float x, float y, float cx, float cy, float r) =>
        y <= cy && Dot(x, y, cx, cy, r);

    static bool Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        float vx = bx - ax, vy = by - ay;
        float t = ((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy);
        t = Math.Max(0, Math.Min(1, t));
        float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
        return dx * dx + dy * dy <= r * r;
    }

    /// <summary>เส้นโค้งหนา (มุมเป็นองศา 0 = ขวา, 90 = บน) ปลายมน</summary>
    static bool Arc(float x, float y, float cx, float cy, float r, float half, float fromDeg, float toDeg)
    {
        float dx = x - cx, dy = y - cy;
        float d = (float)Math.Sqrt(dx * dx + dy * dy);
        double ang = Math.Atan2(dy, dx) * 180 / Math.PI;
        if (ang < 0) ang += 360;
        if (ang >= fromDeg && ang <= toDeg && Math.Abs(d - r) <= half) return true;
        // ปลายมน
        foreach (var a in new[] { fromDeg, toDeg })
        {
            double rad = a * Math.PI / 180;
            if (Dot(x, y, cx + (float)Math.Cos(rad) * r, cy + (float)Math.Sin(rad) * r, half)) return true;
        }
        return false;
    }

    /// <summary>ปากหยัก (คลื่น) ระหว่าง x = ±halfWidth</summary>
    static bool Wave(float x, float y, float cy, float halfWidth, float amp, float thick)
    {
        if (Math.Abs(x) > halfWidth + thick) return false;
        float xc = Math.Max(-halfWidth, Math.Min(halfWidth, x));
        float wy = cy + amp * (float)Math.Sin(xc / halfWidth * Math.PI * 2);
        float dx = x - xc, dy = y - wy;
        return dx * dx + dy * dy <= thick * thick * 1.1f;
    }

    /// <summary>รูปหัวใจ ขนาด s รอบจุด (0,0)</summary>
    static bool Heart(float x, float y, float s)
    {
        x /= s; y /= s;
        y += 0.25f;
        float a = x * x + y * y - 1;
        return a * a * a - x * x * y * y * y <= 0;
    }

    // ---------------------------------------------------------------
    // Calm Flow: ดอกไม้บาน (สีขาว/เทา ย้อมสีด้วย Image.color)
    // ---------------------------------------------------------------

    /// <summary>กลีบดอก โคนอยู่ล่างกลาง (pivot 0.5, 0) ไล่เฉดจากโคนเข้มไปปลายสว่าง</summary>
    public static float[] Petal(int size)
    {
        var px = new float[size * size * 4];
        Paint(px, size, (x, y) =>
        {
            float t = (y + 1f) / 2f;                                   // 0 โคน -> 1 ปลาย
            if (t < 0f || t > 0.985f) return false;
            float w = 0.78f * (float)Math.Pow(Math.Sin(Math.PI * Math.Pow(t, 0.72)), 0.8);
            return Math.Abs(x) < w;
        }, new Rgba(1, 1, 1));
        Shade(px, size, (x, y) =>
        {
            float t = (y + 1f) / 2f;
            float vein = Math.Max(0f, 1f - Math.Abs(x) * 9f) * 0.06f * (1f - t);
            return 0.80f + 0.20f * (float)Math.Pow(t, 0.6) + vein;
        });
        return px;
    }

    /// <summary>ใบไม้ปลายแหลม โคนอยู่ล่างกลาง (pivot 0.5, 0)</summary>
    public static float[] BloomLeaf(int size)
    {
        var px = new float[size * size * 4];
        Paint(px, size, (x, y) =>
        {
            float t = (y + 1f) / 2f;
            if (t < 0f || t > 0.99f) return false;
            float w = 0.5f * (float)Math.Sin(Math.PI * Math.Pow(t, 0.85));
            return Math.Abs(x) < w;
        }, new Rgba(1, 1, 1));
        Shade(px, size, (x, y) => Math.Abs(x) < 0.025f && y > -0.9f && y < 0.85f ? 0.82f : 0.92f + 0.08f * (y + 1f) / 2f);
        return px;
    }

    /// <summary>แสงฟุ้งกลม (ขอบจาง) สำหรับประกายตอนดอกบานเต็มที่</summary>
    public static float[] Glow(int size)
    {
        var px = new float[size * size * 4];
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = (i + 0.5f) / size * 2f - 1f, y = (j + 0.5f) / size * 2f - 1f;
                float d = (float)Math.Sqrt(x * x + y * y);
                float a = Math.Max(0f, 1f - d);
                int k = (j * size + i) * 4;
                px[k] = px[k + 1] = px[k + 2] = 1f;
                px[k + 3] = a * a * (3f - 2f * a);
            }
        return px;
    }

    /// <summary>วงแหวนสีขาว (ใช้กับ Image แบบ Filled Radial) thickness = ความหนาเทียบรัศมี 0-1</summary>
    public static float[] Ring(int size, float thickness)
    {
        var px = new float[size * size * 4];
        float inner = 0.96f - thickness;
        Paint(px, size, (x, y) => { float d = x * x + y * y; return d <= 0.96f * 0.96f && d >= inner * inner; }, new Rgba(1, 1, 1));
        return px;
    }

    delegate float ShadeFn(float x, float y);

    static void Shade(float[] px, int size, ShadeFn f)
    {
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                int k = (j * size + i) * 4;
                if (px[k + 3] <= 0f) continue;
                float x = (i + 0.5f) / size * 2f - 1f, y = (j + 0.5f) / size * 2f - 1f;
                float v = Math.Max(0f, Math.Min(1f, f(x, y)));
                px[k] *= v; px[k + 1] *= v; px[k + 2] *= v;
            }
    }
}
