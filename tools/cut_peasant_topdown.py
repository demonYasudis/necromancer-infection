"""Extract animation parts from the approved elevated three-quarter peasant."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageChops, ImageFilter

root = Path(__file__).resolve().parents[1] / 'Assets/Art/PeasantTopDown'
image = Image.open(root / 'peasant-complete.png').convert('RGBA')
assert image.size == (1024, 1536)

def polygon(points):
    mask = Image.new('L', image.size)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask

def band(y0, y1):
    return polygon([(0, y0), (1024, y0), (1024, y1), (0, y1)])

def capped(y0, y1, center, radius, vertical_radius=None):
    mask = band(y0, y1)
    x, y = center
    vertical_radius = radius if vertical_radius is None else vertical_radius
    ImageDraw.Draw(mask).ellipse((x-radius, y-vertical_radius, x+radius, y+vertical_radius), fill=255)
    return mask

head = polygon([(390,0),(680,0),(680,270),(606,310),(590,380),(552,425),(496,401),(425,340),(390,290)])
left_arm = polygon([(300,400),(368,420),(410,520),(370,644),(366,950),(220,950),(220,450)])
right_arm = polygon([(680,310),(750,390),(850,810),(730,830),(673,650),(651,530),(664,425)])
arms = ImageChops.lighter(left_arm, right_arm)
body = ImageChops.subtract(band(270,800), ImageChops.lighter(head, arms))
left_leg = polygon([(380,800),(560,800),(535,950),(532,1100),(525,1536),(200,1536),(200,950)])
right_leg = polygon([(560,800),(850,800),(850,1536),(525,1536),(532,1100),(535,950)])
masks = [head, body, ImageChops.subtract(band(760,860), arms),
    ImageChops.multiply(right_arm, capped(0,526,(724,526),43)),
    ImageChops.multiply(right_arm, capped(526,850,(724,526),43)),
    ImageChops.multiply(left_arm, capped(0,625,(294,625),45)),
    ImageChops.multiply(left_arm, capped(625,950,(294,625),45)),
    # The painted right knee sits below the upper trouser fold. Keep its overlap
    # shallow: a large circular cap duplicated the entire fold during bending.
    ImageChops.multiply(right_leg, capped(825,985,(625,985),72,20)),
    ImageChops.multiply(right_leg, capped(985,1536,(625,985),72,20)),
    ImageChops.multiply(left_leg, capped(825,1013,(450,1013),80,20)),
    ImageChops.multiply(left_leg, capped(1013,1536,(450,1013),80,20))]
names = ['head','torso','pelvis','near_upper','near_fore','far_upper','far_fore','near_thigh','near_shin','far_thigh','far_shin']
starts = [(520,95),(520,400),(530,780),(683,351),(724,526),(334,446),(294,625),(601,828),(625,985),(470,845),(450,1013)]
ends = [(520,370),(530,780),(530,850),(724,526),(775,681),(294,625),(299,817),(625,985),(644,1128),(450,1013),(402,1315)]

def world(p):
    return {'x': (p[0]-512)/650, 'y': (1450-p[1])/650}

pieces = []
for name, mask, start, end in zip(names, masks, starts, ends):
    if name not in ('head','torso','pelvis'):
        mask = mask.filter(ImageFilter.MaxFilter(17))
    if name in ('near_thigh','near_shin','far_thigh','far_shin'):
        mask = ImageChops.subtract(mask, arms)
    alpha = ImageChops.multiply(image.getchannel('A'), mask)
    box = alpha.point(lambda v:255 if v > 100 else 0).getbbox()
    assert box, name
    part = image.copy()
    part.putalpha(alpha)
    part.crop(box).save(root / (name+'.png'))
    def norm(p):
        return {'x': (p[0]-box[0])/(box[2]-box[0]), 'y': 1-(p[1]-box[1])/(box[3]-box[1])}
    pieces.append({'Name':name, 'RestStart':world(start), 'RestEnd':world(end), 'ImageStart':norm(start), 'ImageEnd':norm(end)})
Image.open(root.parent / 'PeasantRig/pouch.png').save(root / 'pouch.png')
pieces.append({'Name':'pouch','RestStart':world((414,780)),'RestEnd':world((414,870)),
    'ImageStart':{'x':.5,'y':.97},'ImageEnd':{'x':.5,'y':.03}})
(root / 'rig.json').write_text(json.dumps({'Pieces':pieces},indent=2),encoding='utf-8')
print('Extracted approved top-down peasant into 11 body parts and an optional pouch.')
