"""Extract overlapping parts from one coherent generated human drawing."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageChops, ImageFilter
root=Path(__file__).resolve().parents[1]/'Assets/Art/PeasantRigV3'
image=Image.open(root/'peasant-complete.png').convert('RGBA')
assert image.size==(1024,1536)
def polygon(points):
    mask=Image.new('L',image.size); ImageDraw.Draw(mask).polygon(points,fill=255); return mask
def band(y0,y1): return polygon([(0,y0),(1024,y0),(1024,y1),(0,y1)])
def capped_band(y0,y1,center,radius):
    mask=band(y0,y1)
    x,y=center
    ImageDraw.Draw(mask).ellipse((x-radius,y-radius,x+radius,y+radius),fill=255)
    return mask
head=polygon([(390,0),(650,0),(650,215),(600,245),(575,280),(545,310),(475,298),(424,260),(390,230)])
left_arm=polygon([(312,291),(373,319),(395,431),(352,555),(352,900),(200,900),(200,340)])
right_arm=polygon([(632,300),(700,352),(820,800),(800,900),(699,900),(631,555),(621,450)])
arms=ImageChops.lighter(left_arm,right_arm)
body=ImageChops.subtract(band(220,750),ImageChops.lighter(head,arms))
left_leg=polygon([(350,750),(530,750),(530,815),(500,1000),(480,1200),(500,1536),(200,1536),(200,900)])
right_leg=polygon([(530,750),(800,750),(850,1536),(500,1536),(480,1200),(500,1000),(530,815)])
masks=[head,body,ImageChops.subtract(band(706,815),arms),
ImageChops.multiply(right_arm,capped_band(0,540,(689,540),43)),
ImageChops.multiply(right_arm,capped_band(540,900,(689,540),43)),
ImageChops.multiply(left_arm,capped_band(0,535,(291,535),43)),
ImageChops.multiply(left_arm,capped_band(535,900,(291,535),43)),
ImageChops.multiply(right_leg,capped_band(780,1014,(594,1014),66)),
ImageChops.multiply(right_leg,capped_band(1014,1536,(594,1014),66)),
ImageChops.multiply(left_leg,capped_band(780,1014,(416,1014),66)),
ImageChops.multiply(left_leg,capped_band(1014,1536,(416,1014),66))]
names=['head','torso','pelvis','near_upper','near_fore','far_upper','far_fore','near_thigh','near_shin','far_thigh','far_shin']
starts=[(510,42),(511,298),(514,724),(646,350),(689,540),(336,342),(291,535),(580,788),(594,1014),(440,788),(416,1014)]
ends=[(503,283),(512,733),(514,810),(689,540),(745,722),(291,535),(277,731),(594,1014),(593,1270),(416,1014),(375,1280)]
def world(p): return {'x':(p[0]-512)/650,'y':(1470-p[1])/650}
pieces=[]
for name,mask,start,end in zip(names,masks,starts,ends):
    if name not in ('head','torso','pelvis'): mask=mask.filter(ImageFilter.MaxFilter(17))
    if name in ('near_thigh','near_shin','far_thigh','far_shin'):
        mask=ImageChops.subtract(mask,arms)
    alpha=ImageChops.multiply(image.getchannel('A'),mask)
    box=alpha.point(lambda v:255 if v>100 else 0).getbbox()
    part=image.copy(); part.putalpha(alpha); part.crop(box).save(root/(name+'.png'))
    def norm(p): return {'x':(p[0]-box[0])/(box[2]-box[0]),'y':1-(p[1]-box[1])/(box[3]-box[1])}
    pieces.append({'Name':name,'RestStart':world(start),'RestEnd':world(end),'ImageStart':norm(start),'ImageEnd':norm(end)})
Image.open(root.parent/'PeasantRig/pouch.png').save(root/'pouch.png')
pieces.append({'Name':'pouch','RestStart':world((399,737)),'RestEnd':world((399,827)),'ImageStart':{'x':.5,'y':.97},'ImageEnd':{'x':.5,'y':.03}})
(root/'rig.json').write_text(json.dumps({'Pieces':pieces},indent=2),encoding='utf-8')
print('Extracted 11 body parts and pouch from one coherent human drawing.')

