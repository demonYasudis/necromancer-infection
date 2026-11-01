"""Assemble the frames actually rendered by Unity into a looping preview."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
paths = sorted((root / 'Logs/peasant-frames').glob('walk-*.png'))
assert len(paths) == 24, 'Run PeasantRigPlayChecks.RunBatch first'
frames = [Image.open(path).convert('RGB').crop((400, 0, 900, 700)) for path in paths]
target = root / 'Previews/peasant-unity-walk-v3.gif'
target.parent.mkdir(exist_ok=True)
frames[0].save(target, save_all=True, append_images=frames[1:], duration=49, loop=0, optimize=False)
idle = Image.open(root / 'Logs/peasant-unity-idle.png').crop((400, 0, 900, 700))
idle.save(root / 'Previews/peasant-unity-idle-v3.png')
print(target)
