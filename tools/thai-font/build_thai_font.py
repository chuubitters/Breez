"""
สร้างฟอนต์ให้ Unity TextMeshPro แสดงภาษาไทยถูกต้อง (TMP ไม่รองรับ GPOS/GSUB:
วรรณยุกต์ซ้อนสระบน / พยัญชนะหางยาว จะทับกัน)

- shape ทุกคลัสเตอร์ พยัญชนะ+สระบน/ล่าง+วรรณยุกต์(+ำ) ด้วย HarfBuzz
- glyph ที่ต้องเลื่อน/เปลี่ยนรูป => composite glyph ที่เลื่อนไว้แล้ว ใส่ใน PUA (U+F700..)
- รองรับหลายน้ำหนักพร้อมกัน: ทุกน้ำหนักใช้ PUA ชุดเดียวกัน => ตาราง C# อันเดียว
- เขียนตาราง C# (ต้นฉบับ -> ข้อความที่แทน PUA แล้ว) ให้ ThaiShaper ใช้

ใช้:
  python build_thai_font.py --family "Breez Seed TH" --table ThaiShapingTable.cs \
      LINESeedSansTH_A_Rg.ttf:Regular:BreezSeedTH-Regular.ttf \
      LINESeedSansTH_A_Bd.ttf:Bold:BreezSeedTH-Bold.ttf
"""
import argparse, itertools
import uharfbuzz as hb
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent

ap = argparse.ArgumentParser()
ap.add_argument('--family', required=True)
ap.add_argument('--table', required=True)
ap.add_argument('--source-name', default='LINE Seed Sans TH App')
ap.add_argument('fonts', nargs='+', help='src.ttf:Style:out.ttf')
args = ap.parse_args()

weights = []
for spec in args.fonts:
    src, style, out = spec.split(':')
    tt = TTFont(src)
    blob = hb.Blob.from_file_path(src)
    weights.append(dict(src=src, style=style, out=out, tt=tt, order=list(tt.getGlyphOrder()),
                        hb=hb.Font(hb.Face(blob)), hmtx=tt['hmtx'],
                        rev={g: cp for cp, g in sorted(tt.getBestCmap().items(), reverse=True)}))

def shape(w, s):
    buf = hb.Buffer(); buf.add_str(s); buf.guess_segment_properties(); hb.shape(w['hb'], buf, {})
    return [(w['order'][i.codepoint], p.x_advance, p.x_offset, p.y_offset)
            for i, p in zip(buf.glyph_infos, buf.glyph_positions)]

consonants = [chr(c) for c in range(0x0E01, 0x0E2F)]
lower = ['', 'ุ', 'ู', 'ฺ']
upper = ['', 'ั', 'ิ', 'ี', 'ึ', 'ื', '็', 'ํ']
tones = ['', '่', '้', '๊', '๋', '์']
am = ['', 'ำ']

pua_next = 0xE000
pua_of = {}       # (glyph, ((dx,dy,adv) ต่อน้ำหนัก)) -> codepoint
new_glyphs = []   # (codepoint, glyph, [(dx,dy,adv) ต่อน้ำหนัก])
table = {}

def plain(w, g, adv, dx, dy):
    return dx == 0 and dy == 0 and g in w['rev'] and w['hmtx'][g][0] == adv

for c in consonants:
    combos = set()
    for l, u, t, a in itertools.product(lower, upper, tones, am):
        if l and u: continue
        if a and (l or u): continue
        s = c + l + u + t + a
        if len(s) > 1: combos.add(s)
    for s in sorted(combos):
        runs = [shape(w, s) for w in weights]
        if len({tuple(g for g, *_ in r) for r in runs}) != 1:
            raise SystemExit(f'glyph sequence differs between weights for {s!r}: {runs}')
        out = []
        for i in range(len(runs[0])):
            g = runs[0][i][0]
            pos = tuple((r[i][2], r[i][3], r[i][1]) for r in runs)   # dx, dy, adv
            if all(plain(w, g, adv, dx, dy) for w, (dx, dy, adv) in zip(weights, pos)) \
                    and len({w['rev'][g] for w in weights}) == 1:
                out.append(chr(weights[0]['rev'][g]))
                continue
            key = (g, pos)
            if key not in pua_of:
                if pua_next > 0xF8FF: raise SystemExit(f'PUA full at {s!r}')
                pua_of[key] = pua_next
                new_glyphs.append((pua_next, g, pos))
                pua_next += 1
            out.append(chr(pua_of[key]))
        out = ''.join(out)
        if out != s: table[s] = out

# ---------- เพิ่ม glyph + เปลี่ยนชื่อ แต่ละน้ำหนัก ----------
for wi, w in enumerate(weights):
    tt, order, glyf, hmtx = w['tt'], w['order'], w['tt']['glyf'], w['hmtx']
    for cp, g, pos in new_glyphs:
        dx, dy, adv = pos[wi]
        name = f'breez.{cp:04X}'
        comp = GlyphComponent(); comp.glyphName = g; comp.x, comp.y = dx, dy; comp.flags = 0x4
        glyph = Glyph(); glyph.numberOfContours = -1; glyph.components = [comp]
        glyf[name] = glyph
        order.append(name)
    tt.setGlyphOrder(order)
    for cp, g, pos in new_glyphs:
        name = f'breez.{cp:04X}'
        glyf[name].recalcBounds(glyf)
        hmtx[name] = (pos[wi][2], getattr(glyf[name], 'xMin', 0))
        for sub in tt['cmap'].tables:
            if sub.isUnicode(): sub.cmap[cp] = name
    if 'post' in tt and tt['post'].formatType == 2.0:
        tt['post'].extraNames = []; tt['post'].mapping = {}

    fam, style = args.family, w['style']
    ps = fam.replace(' ', '') + '-' + style.replace(' ', '')
    tt['name'].names = [r for r in tt['name'].names if r.nameID not in (16, 17, 21, 22)]
    for rec in tt['name'].names:
        if rec.nameID == 1: rec.string = fam if style in ('Regular', 'Bold') else f'{fam} {style}'
        elif rec.nameID == 2: rec.string = style if style in ('Regular', 'Bold') else 'Regular'
        elif rec.nameID == 4: rec.string = f'{fam} {style}'
        elif rec.nameID == 6: rec.string = ps
        elif rec.nameID == 3: rec.string = f'{ps} (modified from {args.source_name}, SIL OFL 1.1)'
    tt.save(w['out'])

# ---------- ตาราง C# ----------
def lit(s): return '"' + ''.join(f'\\u{ord(ch):04X}' for ch in s) + '"'
keys = sorted(table)
with open(args.table, 'w', encoding='utf-8') as f:
    f.write(f'// <auto-generated> สร้างจาก tools/thai-font/build_thai_font.py ({args.source_name} + HarfBuzz) ห้ามแก้ด้วยมือ </auto-generated>\n')
    f.write(f'// คลัสเตอร์ภาษาไทย -> glyph ที่จัดตำแหน่งไว้แล้วใน PUA ของฟอนต์ {args.family} (ทุกน้ำหนักใช้ชุดเดียวกัน)\n')
    f.write('internal static class ThaiShapingTable\n{\n')
    f.write(f'    internal const int Count = {len(keys)};\n')
    f.write('    internal static readonly string[] Keys =\n    {\n')
    for i in range(0, len(keys), 8): f.write('        ' + ', '.join(lit(k) for k in keys[i:i+8]) + ',\n')
    f.write('    };\n    internal static readonly string[] Values =\n    {\n')
    for i in range(0, len(keys), 8): f.write('        ' + ', '.join(lit(table[k]) for k in keys[i:i+8]) + ',\n')
    f.write('    };\n}\n')
print(f'clusters remapped: {len(table)}, new PUA glyphs: {len(new_glyphs)} (U+E000..U+{pua_next-1:04X})')
