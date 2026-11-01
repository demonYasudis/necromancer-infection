"""Export an exact-phase comparison and cycle from Unity leg regression renders."""
from pathlib import Path
import argparse
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--baseline', default='Logs/peasant-leg-before')
parser.add_argument('--suffix', default='')
args = parser.parse_args()
crop = (440, 60, 820, 650)
before = Image.open(root / args.baseline / 'run-06.png').convert('RGB').crop(crop)
after = Image.open(root / 'Logs/peasant-leg-after/run-06.png').convert('RGB').crop(crop)
review = Image.new('RGB', (before.width * 2, before.height + 50), '#101315')
review.paste(before, (0, 50))
review.paste(after, (before.width, 50))
draw = ImageDraw.Draw(review)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 26)
draw.text((24, 10), 'До', font=font, fill='#e8b4a3')
draw.text((before.width + 24, 10), 'После', font=font, fill='#b5d5bb')
target = root / ('Previews/peasant-leg-before-after' + args.suffix + '.png')
review.save(target)
paths = sorted((root / 'Logs/peasant-leg-after').glob('run-*.png'))
assert len(paths) == 24
frames = [Image.open(path).convert('RGB').crop(crop) for path in paths]
frames[0].save(root / ('Previews/peasant-leg-fixed' + args.suffix + '.gif'), save_all=True,
               append_images=frames[1:], duration=49, loop=0, optimize=False)
print('Exported exact-phase before/after comparison and corrected cycle.')
