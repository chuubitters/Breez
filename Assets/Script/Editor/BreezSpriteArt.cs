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
}
