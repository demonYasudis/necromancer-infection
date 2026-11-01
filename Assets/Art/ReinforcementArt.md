# Скелет, арбалетчик и поднятый стражник

Три цельных 2D-спрайта в текущем стиле игры, ракурс 3/4 сверху:

- `SkeletonTopDown/skeleton-v1.png` — скелет, видимая высота около 1,4 м.
- `CrossbowmanTopDown/crossbowman-v1.png` — арбалетчик, около 1,55 м.
- `RaisedGuardTopDown/raised-guard-v1.png` — тяжёлая нежить, около 1,65 м.

PNG с прозрачностью, 1024 × 1536. В Unity импортируются без сжатия,
с опорой у стоп и компонентом `CharacterSpriteVisual`: поворот к камере,
зеркальное отражение при движении и вспышка при получении урона.
Это цельные изображения без суставной анимации и отдельных видов со спины.

Установка: **Necromancer Infection → Install Reinforcement Character Art**.
Изменяются только внешность и видимость старых MeshRenderer в трёх prefab.
Старые части сохранены как точки для анимационных и игровых компонентов.
Коллайдеры, характеристики, точка вылета болта и ссылки на подкрепления сохранены.

Проверка из командной строки Unity:
`-batchmode -projectPath <проект> -executeMethod NecromancerInfection.Editor.ReinforcementArtSetup.RunBatch -logFile Logs/reinforcement-art-checks.log`

Проверяет подключение в обеих сценах, выполняет существующие игровые проверки
арбалетчика, платного поднятия стражника и кладбища. Снимок из Unity:
`Previews/reinforcement-characters-in-game.png`.

## Генерация

Built-in image_gen, `transparent_background: true`.
Референс стиля для всех трёх: `GuardTopDown/guard-concept-v1.png`.
Ниже сохранены использованные промпты.

### Скелет

Use case: stylized-concept. Asset type: production game character sprite for Necromancer Infection, single intact figure. Match the provided reference's detailed gritty medieval dark fantasy painted sprite style, subdued palette, fine dark outlines, realistic adult proportions, readable silhouette. Full body, 3/4 front view from elevated camera approximately 35 degrees above ground, facing slightly screen right. Entire character and equipment visible with comfortable clear margins, feet aligned close to bottom. Actual transparent background, NO background, NO floor, NO shadow ellipse, NO text, NO labels, NO frame. One character only, neutral ready stance, not a sheet. Subject: reanimated human skeleton, ivory weathered bones, coherent anatomical ribcage and pelvis, thin limbs, skull with small toxic green glowing eyes, ragged charcoal cloth at waist, frayed leather belt. Empty hands reaching slightly forward. No armor, no helmet, no weapons. Clearly an agile fragile undead minion.

### Общая часть промпта арбалетчика и поднятого стражника

Production game sprite, match reference's gritty medieval dark fantasy hand-painted detailed style, subdued colors, fine dark outlines. One complete adult character full body 3/4 front elevated view 35 degrees above ground, facing slightly screen right, neutral ready pose. Transparent background, no floor or shadows, no text or frame. Entire head, feet and equipment visible with margins.

### Арбалетчик — дополнение

Living medieval village crossbowman, worn dark ochre gambeson, brown hood, stern bearded human face, dark trousers, scuffed leather boots and bracers, bolt quiver on hip. Both hands hold recognizable wooden and steel crossbow with broad horizontal bow across chest. No shield, no sword, no magic.

### Поднятый стражник — дополнение

Heavy undead version of reference guard: same riveted iron helmet, chainmail, faded torn blue surcoat, wood kite shield with steel rim at screen right and lowered chipped sword at screen left. Pale corpse face, tiny green luminous eyes, battered dented iron, frayed cloth, asymmetric shoulder armor. Broad imposing hunched silhouette, restrained green magic. No gore.
