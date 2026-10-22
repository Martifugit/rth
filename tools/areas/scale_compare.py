# Draws docs/areas/scale-compare.png: the plan beside RS3 at one scale. Needs coords.json from the RS wiki's 'coordinates' bucket (plane 0) in the working directory.
import json,math,collections
from PIL import Image,ImageDraw,ImageFont
T=1.4  # tiles per output pixel, both panels
F=lambda s:ImageFont.truetype('DejaVuSans-Bold.ttf',s)
plan=json.load(open('/home/user/rth/docs/areas/skeleton.json'));S=plan['scale']['tilesPerSketchPixel']
sk=Image.open('/home/user/rth/images/map.png').convert('RGB')
k=S/T; L=sk.resize((int(sk.width*k),int(sk.height*k)))
L=Image.blend(L,Image.new('RGB',L.size,(18,18,22)),0.35)
A={a['id']:a for a in plan['areas']}
P=lambda i:(A[i]['sketch'][0]*k,A[i]['sketch'][1]*k)
# RS panel from wiki coordinates
d=json.load(open('coords.json'));x0,x1,y0,y1=2100,3950,2560,3960
R=Image.new('RGB',(int((x1-x0)/T),int((y1-y0)/T)),(28,44,70));rd=ImageDraw.Draw(R)
cells=collections.Counter()
for e in d:
  if e.get('x') and e.get('y'):
    x,y=e['x'][0],e['y'][0]
    if x0<=x<x1 and y0<=y<y1: cells[(x//16,y//16)]+=1
for (cx,cy),n in cells.items():
  px,py=(cx*16-x0)/T,(y1-cy*16-16)/T
  rd.rectangle([px,py,px+16/T,py+16/T],fill=(86,104,66) if n<6 else (110,128,80))
Q=lambda x,y:((x-x0)/T,(y1-y)/T)
rs={'Lumbridge':(3200,3215),'Varrock':(3212,3443),'Falador':(3000,3353),'Al Kharid':(3300,3168),'Ardougne':(2642,3303),'Menaphos':(3175,2720),'Prifddinas':(2208,3361),'Burthorpe':(2898,3540),'Canifis':(3496,3488),'Rellekka':(2652,3680)}
kingdoms={'MISTHALIN':(3330,3300),'ASGARNIA':(2960,3460),'KANDARIN':(2640,3420),'KHARIDIAN DESERT':(3290,2960),'MORYTANIA':(3560,3330),'TIRANNWN':(2240,3220),'WILDERNESS':(3150,3800),'FREMENNIK':(2650,3760)}
def line(dr,a,b,c,txt,f=F(17)):
  dr.line([a,b],fill=c,width=4);m=(a[0]*0.4+b[0]*0.6,a[1]*0.4+b[1]*0.6)
  dr.text(m,txt,fill=c,font=f,anchor='mm',stroke_width=4,stroke_fill=(0,0,0))
def dot(dr,p,name,c=(255,255,255)):
  dr.ellipse([p[0]-7,p[1]-7,p[0]+7,p[1]+7],fill=c,outline=(0,0,0),width=2)
  dr.text((p[0]+11,p[1]-10),name,fill=c,font=F(18),stroke_width=3,stroke_fill=(0,0,0))
dist=lambda a,b:math.dist(a,b)
ld=ImageDraw.Draw(L)
realm={}
for a in plan['areas']:
  if a['sketch'] and a['layer']=='surface': realm.setdefault(a['realm'],[]).append(P(a['id']))
for r,ps in realm.items():
  if r in('Nivil',):continue
  cx=sum(p[0] for p in ps)/len(ps);cy=sum(p[1] for p in ps)/len(ps)
  ld.text((cx,cy+(90 if r=='Venaria' else 40)),r.upper(),fill=(255,230,150),font=F(26),anchor='mm',stroke_width=4,stroke_fill=(0,0,0))
# option 2 ring: Tithe 430 tiles from Cruor
c=P('cruor');r=430/T;ld.ellipse([c[0]-r,c[1]-r,c[0]+r,c[1]+r],outline=(255,120,120),width=2)
ld.text((c[0]+r*0.72,c[1]+r*0.72),'430 tiles from Cruor\n(option 2: Tithe here)',fill=(255,150,150),font=F(16),stroke_width=3,stroke_fill=(0,0,0))
for a,b in [('tolling','cruor'),('cruor','tithe'),('cruor','cistern'),('tithe','cistern'),('cruor','meridian')]:
  line(ld,P(a),P(b),(255,210,90),f"{dist(A[a]['sketch'],A[b]['sketch'])*S:.0f}")
for i in ['tolling','cruor','tithe','cistern','meridian']: dot(ld,P(i),A[i]['name'])
for n,(x,y) in kingdoms.items(): rd.text(Q(x,y),n,fill=(255,230,150),font=F(24),anchor='mm',stroke_width=4,stroke_fill=(0,0,0))
for a,b in [('Lumbridge','Varrock'),('Varrock','Falador'),('Varrock','Al Kharid'),('Varrock','Ardougne'),('Lumbridge','Menaphos')]:
  line(rd,Q(*rs[a]),Q(*rs[b]),(255,210,90),f"{dist(rs[a],rs[b]):.0f}")
for n,p in rs.items(): dot(rd,Q(*p),n)
H=max(L.height,R.height)+130;W=L.width+R.width+30
img=Image.new('RGB',(W,H),(14,14,17));img.paste(L,(0,100));img.paste(R,(L.width+30,100))
dr=ImageDraw.Draw(img)
dr.text((20,20),"R'th (plan, 1.75 tiles per sketch pixel)",fill='white',font=F(34))
dr.text((L.width+50,20),"RuneScape 3 mainland (dots = wiki map points)",fill='white',font=F(34))
dr.text((20,62),"Both drawn at the same scale. Yellow numbers: tiles in a straight line.",fill=(200,200,200),font=F(20))
bx=20;by=H-22;dr.line([bx,by,bx+500/T,by],fill='white',width=5);dr.text((bx+500/T+12,by-12),'500 tiles (about 2.5 min running)',fill='white',font=F(20))
img.save('/home/user/rth/docs/areas/scale-compare.png');print(img.size)
