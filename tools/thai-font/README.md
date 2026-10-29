# Breez Seed TH font generator

TextMeshPro (3.0.x) has no GSUB/GPOS support, so Thai tone marks and upper vowels collide.
`build_thai_font.py` shapes every consonant + vowel + tone cluster with HarfBuzz, stores the
positioned results as composite glyphs in the Private Use Area (U+E000…) and writes one lookup
table for `ThaiShaper` (Unity) that is shared by every weight.

Source: LINE Seed Sans TH App (© LINE, SIL Open Font License 1.1), e.g. from the npm package
`@fontpkg/line-seed-sans-th-app`. The output is renamed to "Breez Seed TH".

```
pip install uharfbuzz fonttools
python build_thai_font.py --family "Breez Seed TH" --table ../../Assets/Script/Thai/ThaiShapingTable.cs \
  LINESeedSansTH_A_Rg.ttf:Regular:../../Assets/Fonts/Breez/BreezSeedTH-Regular.ttf \
  LINESeedSansTH_A_Bd.ttf:Bold:../../Assets/Fonts/Breez/BreezSeedTH-Bold.ttf
```
