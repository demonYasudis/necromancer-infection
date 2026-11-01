"""Slice generated 4x3 atlas; ignore isolated specks when finding each part's bounds."""
from pathlib import Path
import json
from PIL import Image

root = Path(__file__).resolve().parents[1] / 'Assets/Art/PeasantRigV2'
atlas = Image.open(root / 'atlas.png').convert('RGBA')
names = ['head', 'torso', 'pelvis', 'near_upper', 'near_fore', 'far_upper',
         'far_fore', 'near_thigh', 'near_shin', 'far_thigh', 'far_shin', 'pouch']
rects = {}
for i, name in enumerate(names):
    col, row = i % 4, i // 4
    x0, y0 = round(col * atlas.width / 4), round(row * atlas.height / 3)
    cell = atlas.crop((x0, y0, round((col + 1) * atlas.width / 4), round((row + 1) * atlas.height / 3)))
    w, h = cell.size
    remaining = bytearray(cell.getchannel('A').point(lambda v: 255 if v > 100 else 0).tobytes())
    largest, box = 0, None
    for start in range(w * h):
        if not remaining[start]:
            continue
        stack = [start]
        remaining[start] = 0
        count, left, top, right, bottom = 0, w, h, 0, 0
        while stack:
            pos = stack.pop()
            x, y = pos % w, pos // w
            count += 1
            left, top, right, bottom = min(left, x), min(top, y), max(right, x), max(bottom, y)
            neighbors = []
            if x > 0: neighbors.append(pos - 1)
            if x + 1 < w: neighbors.append(pos + 1)
            if y > 0: neighbors.append(pos - w)
            if y + 1 < h: neighbors.append(pos + w)
            for other in neighbors:
                if remaining[other]:
                    remaining[other] = 0
                    stack.append(other)
        if count > largest:
            largest = count
            box = (max(0, left - 1), max(0, top - 1), min(w, right + 2), min(h, bottom + 2))
    if not box:
        raise ValueError('Empty atlas cell: ' + name)
    cell.crop(box).save(root / (name + '.png'))
    rects[name] = [x0 + box[0], y0 + box[1], box[2] - box[0], box[3] - box[1]]
(root / 'parts.json').write_text(json.dumps(rects, indent=2), encoding='utf-8')
print(rects)
