# Breez Thai font generator

TextMeshPro (3.0.x) has no GSUB/GPOS support, so Thai tone marks and upper vowels collide.
`build_thai_font.py` shapes every consonant + vowel + tone cluster with HarfBuzz, stores the
positioned results as composite glyphs in the Private Use Area (U+F700…) and writes a lookup
table for `ThaiShaper` (Unity).

```
pip install uharfbuzz fonttools
python build_thai_font.py Prompt-Regular.ttf ../../Assets/Fonts/Breez/BreezThai-Regular.ttf ../../Assets/Script/Thai/ThaiShapingTable.cs
```

The font is derived from Prompt (SIL Open Font License 1.1) and renamed to "Breez Thai".
