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

    // ---------------- ผึ้ง (หันขวา) โทนพาสเทล ไม่มีเส้นขอบ ----------------
    public static float[] BeeBody(int s)
    {
        var px = new float[s * s * 4];
        var body = new C(0xFBE3A0);      // เหลืองอ่อน
        var shade = new C(0xF3CF7E);     // เงาด้านล่าง
        var stripe = new C(0xB49E8E);    // แถบน้ำตาลอมเทาอ่อน
        var eye = new C(0x5E4E47);
        // เหล็กใน
        Paint(px, s, (x, y) => Tri(x, y, -0.6f, 0.05f, -0.6f, -0.11f, -0.78f, -0.04f), stripe);
        // ลำตัว + เงาล่าง + ไฮไลต์บน
        Paint(px, s, (x, y) => Ell(x, y, -0.08f, -0.02f, 0.57f, 0.42f), body);
        Paint(px, s, (x, y) => Ell(x, y, -0.08f, -0.02f, 0.57f, 0.42f) && !Ell(x, y, -0.1f, 0.06f, 0.56f, 0.4f), shade);
        Paint(px, s, (x, y) => Ell(x, y, -0.08f, -0.02f, 0.57f, 0.42f) && (Math.Abs(x + 0.3f) < 0.075f || Math.Abs(x - 0.05f) < 0.075f), stripe);
        Paint(px, s, (x, y) => Ell(x, y, -0.16f, 0.2f, 0.26f, 0.07f), new C(0xFFFFFF, 0.55f));
        // หัว
        Paint(px, s, (x, y) => Dot(x, y, 0.5f, 0.04f, 0.29f), body);
        Paint(px, s, (x, y) => Dot(x, y, 0.5f, 0.04f, 0.29f) && !Dot(x, y, 0.48f, 0.08f, 0.28f), shade);
        Paint(px, s, (x, y) => Ell(x, y, 0.44f, 0.18f, 0.1f, 0.05f), new C(0xFFFFFF, 0.6f));
        // ตา แก้ม ยิ้ม
        Paint(px, s, (x, y) => Ell(x, y, 0.61f, 0.09f, 0.045f, 0.06f), eye);
        Paint(px, s, (x, y) => Dot(x, y, 0.625f, 0.115f, 0.017f), new C(0xFFFFFF));
        Paint(px, s, (x, y) => Ell(x, y, 0.57f, -0.07f, 0.07f, 0.045f), new C(0xF2BDC9, 0.95f));
        Paint(px, s, (x, y) => Arc(x, y, 0.68f, -0.01f, 0.06f, 0.014f, 225, 315), eye);
        // หนวด
        Paint(px, s, (x, y) => Line(x, y, 0.5f, 0.3f, 0.58f, 0.58f, 0.018f) || Line(x, y, 0.58f, 0.28f, 0.74f, 0.52f, 0.018f), stripe);
        Paint(px, s, (x, y) => Dot(x, y, 0.58f, 0.6f, 0.045f) || Dot(x, y, 0.75f, 0.54f, 0.045f), stripe);
        return px;
    }

    /// <summary>ปีกใส สีขาวอมฟ้า ไม่มีขอบ ฐานอยู่ล่างกลาง (pivot 0.5, 0.05)</summary>
    public static float[] BeeWing(int s)
    {
        var px = new float[s * s * 4];
        Paint(px, s, (x, y) => Ell(x, y, 0f, 0.05f, 0.52f, 0.9f), new C(0xD8EAF2, 0.9f));
        Paint(px, s, (x, y) => Ell(x, y, -0.12f, 0.32f, 0.16f, 0.32f), new C(0xFFFFFF, 0.9f));
        Erase(px, s, (x, y) => y < -0.9f);
        return px;
    }

    // ---------------- ดอกไม้ 5 กลีบ แบบเดียวกับ Calm Flow / ไอคอนดอกไม้ในเกม (สีขาว ย้อมด้วย color) ----------------
    public static float[] FlowerPetals(int s)
    {
        var px = new float[s * s * 4];
        const int n = 5;
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI / 2 + i * 2 * Math.PI / n;
            float ux = (float)Math.Cos(a), uy = (float)Math.Sin(a);
            Paint(px, s, (x, y) =>
            {
                float along = x * ux + y * uy - 0.5f;     // กลีบกลมรี กึ่งกลางห่างจากกลางดอก 0.5
                float across = -x * uy + y * ux;
                return (along * along) / (0.45f * 0.45f) + (across * across) / (0.33f * 0.33f) <= 1f;
            }, new C(0xFFFFFF));
        }
        // ไล่เฉด: โคนกลีบเข้มกว่าเล็กน้อย ปลายกลีบสว่าง (ไม่มีเส้นขอบ)
        for (int j = 0; j < s; j++)
            for (int i = 0; i < s; i++)
            {
                int k = (j * s + i) * 4;
                if (px[k + 3] <= 0f) continue;
                float x = (i + 0.5f) / s * 2f - 1f, y = (j + 0.5f) / s * 2f - 1f;
                float d = (float)Math.Sqrt(x * x + y * y);
                float v = 0.86f + 0.14f * Math.Min(1f, d / 0.8f);
                px[k] = px[k + 1] = px[k + 2] = v;
            }
        return px;
    }

    public static float[] FlowerCenter(int s)
    {
        var px = new float[s * s * 4];
        Paint(px, s, (x, y) => Dot(x, y, 0, 0, 0.95f), new C(0xFFFFFF));
        Paint(px, s, (x, y) => Dot(x, y, -0.22f, 0.25f, 0.32f), new C(0xFFFFFF, 0.5f));
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
