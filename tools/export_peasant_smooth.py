"""Export 60-frame Unity cycles with a fixed palette to avoid GIF colour shimmer."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
crop = (440, 60, 820, 650)
for action, duration in [('walk', 20), ('run', 10)]:
    paths = sorted((root / 'Logs/peasant-gait-smooth').glob(action + '-*.png'))
    assert len(paths) == 60, 'Run PeasantLegChecks.RunSmoothBatch first'
    frames = [Image.open(path).convert('RGB').crop(crop) for path in paths]
    palette = frames[0].quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    frames = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    frames[0].save(root / ('Previews/peasant-' + action + '-smooth.gif'),
                   save_all=True, append_images=frames[1:], duration=duration,
                   loop=0, optimize=False, disposal=1)
print('Exported 60-frame walk and run cycles with a shared palette.')
