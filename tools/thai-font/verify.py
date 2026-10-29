import re, sys
from PIL import Image, ImageDraw, ImageFont
cs=open('thaifont/ThaiShapingTable.cs',encoding='utf-8').read()
def arr(name):
    body=cs.split(f'{name} =')[1].split('};')[0]
    return [bytes(m,'ascii').decode('unicode_escape') for m in re.findall(r'"((?:\\u[0-9A-F]{4})+)"',body)]
K,V=arr('Keys'),arr('Values'); T=dict(zip(K,V))
def fix(s):  # เหมือน ThaiShaper.Fix ใน C#
    out=[];i=0
    while i<len(s):
        c=s[i]
        if 'ก'<=c<='ฮ':
            for n in (4,3,2):
                k=s[i:i+n]
                if len(k)==n and k in T: out.append(T[k]); i+=n; break
            else: out.append(c); i+=1
        else: out.append(c); i+=1
    return ''.join(out)
words=['ผลลัพธ์  คะแนน  ระยะเวลาที่ใช้  ลูกโป่งที่เป่าได้','จำนวนลูกโป่ง  หายใจเข้า  หายใจออก  เชื่อมต่อแล้ว','ปั่น ฟื้น ฝั่ง น้ำ ค่ำ ญาติ กตัญญู ฎีกา ปฏิทิน ปู่ ฟ้า ฬ้']
W=Image.new('RGB',(1500,560),'white'); d=ImageDraw.Draw(W)
orig='fonts/expo-google-fonts-prompt-0.4.1/package/400Regular/Prompt_400Regular.ttf'
fb=ImageFont.truetype('thaifont/BreezThai-Regular.ttf',44,layout_engine=ImageFont.Layout.BASIC)
fr=ImageFont.truetype(orig,44,layout_engine=ImageFont.Layout.RAQM)
lab=ImageFont.truetype(orig,18)
y=10
for w in words:
    d.text((20,y),'Breez Thai + ThaiShaper, NO shaping (= Unity):',fill='gray',font=lab); d.text((20,y+22),fix(w),fill='black',font=fb)
    d.text((20,y+80),'reference (HarfBuzz):',fill='gray',font=lab); d.text((20,y+102),w,fill='#225',font=fr)
    y+=175
W.crop((0,0,1500,y)).save('thai_fixed.png')
