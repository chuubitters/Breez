using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// =====================================================================
// ภาพสำหรับ Breathe & Focus แบบผึ้งเก็บดอกไม้ (วาดด้วยโค้ด บันทึกเป็น PNG)
//   ผึ้ง (ลำตัว + ปีก), ดอกไม้สีขาว (ย้อมสีตอนเล่น), เกสรกลางดอก
// เก็บไว้ที่ Assets/element/B&F/Bee
// =====================================================================
public static class BreezBeeArt
{
    public const string Folder = "Assets/element/B&F/Bee";
    const int SS = 4;

    struct C
    {
        public float r, g, b, a;
        public C(int hex, float a = 1f) { r = ((hex >> 16) & 255) / 255f; g = ((hex >> 8) & 255) / 255f; b = (hex & 255) / 255f; this.a = a; }
    }
    delegate bool Shape(float x, float y);

    // ---------------- ผึ้ง (หันขวา) ----------------
    public static float[] BeeBody(int s)
    {
        var px = new float[s * s * 4];
        var ink = new C(0x3A3233);
        var yellow = new C(0xF8C83A);
        var light = new C(0xFFE07A);
        // เหล็กใน
        Paint(px, s, (x, y) => Tri(x, y, -0.62f, 0.08f, -0.62f, -0.12f, -0.86f, -0.04f), ink);
        // ขอบลำตัว + หัว
        Paint(px, s, (x, y) => Ell(x, y, -0.08f, -0.02f, 0.62f, 0.47f) || Dot(x, y, 0.5f, 0.04f, 0.33f), ink);
        Paint(px, s, (x, y) => Ell(x, y, -0.08f, -0.02f, 0.56f, 0.41f), yellow);
        // แถบดำ
        Paint(px, s, (x, y) => Ell(x, y, -0.08f, -0.02f, 0.56f, 0.41f) && (Math.Abs(x + 0.3f) < 0.07f || Math.Abs(x - 0.04f) < 0.07f), ink);
        // ไฮไลต์
        Paint(px, s, (x, y) => Ell(x, y, -0.1f, 0.2f, 0.3f, 0.08f), new C(0xFFFFFF, 0.45f));
        // หัว
        Paint(px, s, (x, y) => Dot(x, y, 0.5f, 0.04f, 0.27f), yellow);
        Paint(px, s, (x, y) => Dot(x, y, 0.44f, 0.15f, 0.1f), light);
        // ตา + แก้ม + ปาก
        Paint(px, s, (x, y) => Dot(x, y, 0.6f, 0.1f, 0.065f), ink);
        Paint(px, s, (x, y) => Dot(x, y, 0.62f, 0.13f, 0.022f), new C(0xFFFFFF));
        Paint(px, s, (x, y) => Ell(x, y, 0.56f, -0.07f, 0.07f, 0.045f), new C(0xF29BB0, 0.9f));
        Paint(px, s, (x, y) => Arc(x, y, 0.68f, -0.02f, 0.07f, 0.018f, 220, 320), ink);
        // หนวด
        Paint(px, s, (x, y) => Line(x, y, 0.5f, 0.3f, 0.6f, 0.62f, 0.022f) || Line(x, y, 0.58f, 0.28f, 0.76f, 0.56f, 0.022f), ink);
        Paint(px, s, (x, y) => Dot(x, y, 0.6f, 0.64f, 0.05f) || Dot(x, y, 0.77f, 0.58f, 0.05f), ink);
        return px;
    }

    /// <summary>ปีก: ฐานอยู่ล่างกลาง (pivot 0.5, 0.05) เพื่อกระพือด้วยการย่อแกน Y</summary>
    public static float[] BeeWing(int s)
    {
        var px = new float[s * s * 4];
        Paint(px, s, (x, y) => Ell(x, y, 0f, 0.05f, 0.55f, 0.92f), new C(0x9FC6DA, 0.9f));
        Paint(px, s, (x, y) => Ell(x, y, 0f, 0.05f, 0.47f, 0.84f), new C(0xFFFFFF, 0.85f));
        Paint(px, s, (x, y) => Ell(x, y, -0.12f, 0.35f, 0.14f, 0.3f), new C(0xFFFFFF, 0.9f));
        Erase(px, s, (x, y) => y < -0.9f);
        return px;
    }

    // ---------------- ดอกไม้ (สีขาว ย้อมด้วย SpriteRenderer.color) ----------------
    public static float[] FlowerPetals(int s)
    {
        var px = new float[s * s * 4];
        const int n = 6;
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI / 2 + i * 2 * Math.PI / n;
            float cx = (float)Math.Cos(a) * 0.5f, cy = (float)Math.Sin(a) * 0.5f;
            float ca = (float)Math.Cos(-a), sa = (float)Math.Sin(-a);
            Paint(px, s, (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float rx = dx * ca - dy * sa, ry = dx * sa + dy * ca;   // หมุนให้แกนยาวชี้ออกจากกลางดอก
                return (rx * rx) / (0.46f * 0.46f) + (ry * ry) / (0.3f * 0.3f) <= 1f;
            }, new C(0xE6E6E6));
            Paint(px, s, (x, y) =>
            {
                float dx = x - cx * 1.08f, dy = y - cy * 1.08f;
                float rx = dx * ca - dy * sa, ry = dx * sa + dy * ca;
                return (rx * rx) / (0.36f * 0.36f) + (ry * ry) / (0.22f * 0.22f) <= 1f;
            }, new C(0xFFFFFF));
        }
        return px;
    }

    public static float[] FlowerCenter(int s)
    {
        var px = new float[s * s * 4];
        Paint(px, s, (x, y) => Dot(x, y, 0, 0, 0.95f), new C(0xFFFFFF));
        Paint(px, s, (x, y) => Dot(x, y, -0.25f, 0.28f, 0.28f), new C(0xFFFFFF, 0.6f));
        for (int i = 0; i < 6; i++)
        {
            double a = i * Math.PI / 3 + 0.3;
            float cx = (float)Math.Cos(a) * 0.5f, cy = (float)Math.Sin(a) * 0.5f;
            Paint(px, s, (x, y) => Dot(x, y, cx, cy, 0.1f), new C(0xC9C9C9));
        }
        return px;
    }

    // ---------------- บันทึกเป็น sprite ----------------
    public static Sprite Save(string name, float[] rgba, int size, float ppu, Vector2 pivot)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/element")) AssetDatabase.CreateFolder("Assets", "element");
            if (!AssetDatabase.IsValidFolder("Assets/element/B&F")) AssetDatabase.CreateFolder("Assets/element", "B&F");
            AssetDatabase.CreateFolder("Assets/element/B&F", "Bee");
        }
        string path = $"{Folder}/{name}.png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.spritePixelsPerUnit = ppu;
        var st = new TextureImporterSettings();
        imp.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.Custom;
        st.spritePivot = pivot;
        imp.SetTextureSettings(st);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------------- เครื่องมือวาด ----------------
    static void Paint(float[] px, int size, Shape inside, C c)
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
                px[k] = (c.r * cov + px[k] * da * (1 - cov)) / outA;
                px[k + 1] = (c.g * cov + px[k + 1] * da * (1 - cov)) / outA;
                px[k + 2] = (c.b * cov + px[k + 2] * da * (1 - cov)) / outA;
                px[k + 3] = outA;
            }
    }

    static void Erase(float[] px, int size, Shape inside)
    {
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = ((i + 0.5f) / size) * 2f - 1f, y = ((j + 0.5f) / size) * 2f - 1f;
                if (inside(x, y)) px[(j * size + i) * 4 + 3] = 0f;
            }
    }

    static bool Dot(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

    static bool Ell(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return dx * dx + dy * dy <= 1f;
    }

    static bool Line(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        float vx = bx - ax, vy = by - ay;
        float t = Math.Max(0, Math.Min(1, ((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy)));
        float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
        return dx * dx + dy * dy <= r * r;
    }

    static bool Tri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
        float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
        float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static bool Arc(float x, float y, float cx, float cy, float r, float half, float fromDeg, float toDeg)
    {
        float dx = x - cx, dy = y - cy;
        float d = (float)Math.Sqrt(dx * dx + dy * dy);
        double ang = Math.Atan2(dy, dx) * 180 / Math.PI;
        if (ang < 0) ang += 360;
        return ang >= fromDeg && ang <= toDeg && Math.Abs(d - r) <= half;
    }
}
