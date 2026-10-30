#!/usr/bin/env python3
"""
สร้าง esp32/breez_gadget/ui_text.h : ข้อความภาษาไทย/ตัวเลข สำหรับจอ GC9A01
- ใช้ฟอนต์เดียวกับเกม (Assets/Fonts/Breez/BreezSeedTH-*.ttf) จัดสระ/วรรณยุกต์ถูกต้อง (raqm)
- เก็บเป็น alpha 4 บิต (2 พิกเซล/ไบต์) วาดแบบขอบเรียบ (anti-aliased) บนจอ
แก้ข้อความ: แก้ LABELS ด้านล่าง แล้วรัน  python3 tools/display/make_text_bitmaps.py
"""
import os
from PIL import Image, ImageDraw, ImageFont, features

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REG = os.path.join(ROOT, "Assets/Fonts/Breez/BreezSeedTH-Regular.ttf")
BOLD = os.path.join(ROOT, "Assets/Fonts/Breez/BreezSeedTH-Bold.ttf")
OUT = os.path.join(ROOT, "esp32/breez_gadget/ui_text.h")
assert features.check("raqm"), "ต้องมี libraqm เพื่อจัดสระภาษาไทย"

# id, ข้อความ, ตัวหนา?, ขนาด(px)
LABELS = [
    ("BREEZ",      "Breez",                    True,  46),
    ("QS_TITLE",   "Quick Session",            True,  20),
    ("QS_478",     "ฝึกหายใจ 4-7-8",            False, 18),
    ("PRESS",      "กดปุ่มเพื่อเริ่ม",             False, 17),
    ("READY",      "เตรียมตัว",                 True,  24),
    ("READY_SUB",  "นั่งสบายๆ ผ่อนคลาย",          False, 15),
    ("IN",         "หายใจเข้า",                 True,  24),
    ("HOLD",       "กลั้นหายใจ",                True,  24),
    ("OUT",        "หายใจออก",                 True,  24),
    ("IN_SUB",     "ช้าๆ ทางจมูก",               False, 15),
    ("HOLD_SUB",   "ค้างไว้",                    False, 15),
    ("OUT_SUB",    "เป่าลมออกยาวๆ",             False, 15),
    ("ROUND",      "รอบ",                       False, 16),
    ("DONE",       "เยี่ยมมาก!",                  True,  30),
    ("DONE_SUB",   "ฝึกครบ 4 รอบแล้ว",           False, 17),
    ("BLOW",       "เป่าลมได้ดี",                 False, 15),
    ("TOTAL",      "ฝึกแล้ว",                    False, 15),
    ("TIMES",      "ครั้ง",                      False, 15),
    ("AGAIN",      "กดปุ่มเพื่อฝึกอีกครั้ง",          False, 15),
    ("CAL",        "กำลังปรับเสียง",              True,  22),
    ("CAL_SUB",    "อยู่เงียบๆ สักครู่นะ",           False, 16),
    ("STOPPED",    "หยุดฝึกแล้ว",                True,  24),
]
# ชุดตัวเลข: id, ตัวหนา?, ขนาด, ตัวอักษร
DIGITS = [
    ("BIG",   True,  70, "0123456789"),
    ("SMALL", False, 16, "0123456789/%"),
]

def render(text, font):
    W, H = 400, 200
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    d.text((W // 2, H // 2), text, font=font, fill=255, anchor="mm")
    box = img.getbbox()
    if box is None:
        return 0, 0, 0, 0, b""
    x0, y0, x1, y1 = box
    crop = img.crop(box)
    return crop.width, crop.height, x0 - W // 2, y0 - H // 2, pack(crop)

def render_glyph(ch, font):
    """anchor = left/middle ของตัวอักษร (ต่อเป็นตัวเลขหลายหลักได้)"""
    W, H = 300, 200
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    d.text((50, H // 2), ch, font=font, fill=255, anchor="lm")
    box = img.getbbox()
    x0, y0, x1, y1 = box
    crop = img.crop(box)
    adv = round(font.getlength(ch))
    return crop.width, crop.height, x0 - 50, y0 - H // 2, adv, pack(crop)

def pack(img):
    px = list(img.tobytes())
    w = img.width
    out = bytearray()
    for y in range(img.height):
        row = px[y * w:(y + 1) * w]
        if len(row) % 2:
            row = row + [0]
        for i in range(0, len(row), 2):
            a = (row[i] * 15 + 127) // 255
            b = (row[i + 1] * 15 + 127) // 255
            out.append((a << 4) | b)
    return bytes(out)

def carr(name, data):
    lines = []
    for i in range(0, len(data), 24):
        lines.append("  " + ",".join(f"0x{b:02X}" for b in data[i:i + 24]))
    return f"static const uint8_t {name}[] PROGMEM = {{\n" + ",\n".join(lines) + "\n};\n"

def main():
    fonts = {}
    def F(bold, size):
        k = (bold, size)
        if k not in fonts:
            fonts[k] = ImageFont.truetype(BOLD if bold else REG, size, layout_engine=ImageFont.Layout.RAQM)
        return fonts[k]

    h = ["// สร้างอัตโนมัติจาก tools/display/make_text_bitmaps.py — อย่าแก้ไฟล์นี้ด้วยมือ",
         "#pragma once", "#include <Arduino.h>", "",
         "// alpha 4 บิต แถวละ ceil(w/2) ไบต์ | dx,dy = ตำแหน่งมุมซ้ายบนเทียบกับจุดอ้างอิง",
         "struct UiBitmap { uint8_t w, h; int8_t dx, dy; uint8_t adv; const uint8_t *data; };", ""]
    total = 0
    names = []
    for lid, text, bold, size in LABELS:
        w, hh, dx, dy, data = render(text, F(bold, size))
        h.append(f"// {text}")
        h.append(carr(f"TXT_{lid}_DATA", data))
        names.append((lid, w, hh, dx, dy))
        total += len(data)
    for lid, w, hh, dx, dy in names:
        h.append(f"static const UiBitmap TXT_{lid} = {{ {w}, {hh}, {dx}, {dy}, 0, TXT_{lid}_DATA }};")
    h.append("")
    for did, bold, size, chars in DIGITS:
        entries = []
        for i, ch in enumerate(chars):
            w, hh, dx, dy, adv, data = render_glyph(ch, F(bold, size))
            h.append(carr(f"DIG_{did}_{i}_DATA", data))
            entries.append(f"  {{ {w}, {hh}, {dx}, {dy}, {adv}, DIG_{did}_{i}_DATA }}")
            total += len(data)
        h.append(f"static const char DIG_{did}_CHARS[] = \"{chars}\";")
        h.append(f"static const UiBitmap DIG_{did}[] = {{\n" + ",\n".join(entries) + "\n};\n")
    open(OUT, "w", encoding="utf-8").write("\n".join(h))
    print(f"wrote {OUT}  ({total} bytes of glyph data)")

if __name__ == "__main__":
    main()
