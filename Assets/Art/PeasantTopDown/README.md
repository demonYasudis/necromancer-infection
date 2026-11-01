# Approved top-down peasant

`peasant-complete.png` is the user-approved elevated three-quarter view.
The reference is also saved as `Previews/peasant-three-quarter-topdown.png`.
Generation prompt: `Previews/peasant-topdown-prompt.md`.

Run `tools/cut_peasant_topdown.py` with Pillow to reproduce the parts and
`rig.json`. Rounded overlapping cuts around elbows and knees retain the
painted anatomy without independently generating replacement limbs.

The right leg uses a joint at the painted knee and a shallow elliptical overlap.
The thigh draws over the shin there, so the same trouser fold does not appear
twice when the leg bends. The left knee is aligned with the same forward
bending plane as the right; both use shallow overlaps at the painted folds.

The gait uses narrow parallel foot lanes. A quintic forward swing matches
stance velocity and acceleration at contact, and the lift eases in and out.
One amplitude is fitted for the whole cycle, avoiding per-frame joint-limit
plateaus. Soft reach prevents a snap at full knee extension. Starting, stopping,
and changing walking/running cadence are smoothed in gameplay.

`PeasantLegChecks.RunSmoothBatch` checks contact and joint-motion continuity,
ankle lanes, common knee direction, joint limits and connected body parts in
both mirrored facings, and exports 60 frames per cycle.
`tools/export_peasant_smooth.py` creates fixed-palette GIF previews.

`PeasantTopDownSetup.Install` imports the parts, updates the sprite prefab,
and connects it to the existing Civilian prefab. It preserves scene placement,
gameplay scripts, health, click capsule and transformation prefab references.
`PeasantGameChecks.RunBatch` verifies the actual gameplay connection.

There is one authored facing with horizontal mirroring. Other headings require
additional artwork; the system does not rotate the painted body into unseen views.
