# Class 4 Image2 Attack Effect Sources

Purpose: every Class 4 monster gets its own ChatGPT Image2-generated attack
effect source instead of reusing a generic family effect. The source art lives in
`tools/generated_attack_effects/class4_sources`, and the Unity runtime frames are
imported to `WitchTowerGame/Assets/Resources/BattleEffects/Monster`.

Generated with the built-in ChatGPT Image2 path, then copied into
`tools/generated_attack_effects/class4_sources`.

- `mecha_dragon_valdrake`: mechanical dragon crimson reactor plasma blast with cyan electric accents.
- `drag_gaia`: earth-dragon golem stone shockwave with blue crystal veins.
- `dragon_sword_saint_agito`: red-orange dragon claw crescent sword slash with embers.
- `abyss_dragon_mage_valflare`: violet-black abyss dragon sorcery projectile with cyan rim light.
- `fortress_machine_gigafort`: cyan rail-cannon beam and red artillery micro-bursts.
- `mecha_sword_saint_gransaber`: cyan-white photon X slash with gold circuit sparks.
- `dark_magic_machine_god_merchion`: violet-black magitech singularity with neon circuitry arcs.
- `rock_knight_gaius`: tan stone shield shockwave with blue crystal cracks.
- `astral_eclipse_golem`: purple eclipse ring with gold star fragments and gravity pulse.
- `magic_sword_saint_luciel`: violet cursed crescent slash with dark feather afterimages.
- `seraph_michael`: white-gold seraphic cross burst with wing-shaped rays.
- `spirit_queen_titania`: emerald wind spiral with petals, leaf light blades, and pollen sparkles.
- `spirit_queen_titania_staff_beam`: pink-white sustained fairy beam with petals, nature ring arcs, and pollen glitter.

All prompts requested a single centered 2D RPG VFX sprite on a pure black `#000000`
background with no character, UI, text, watermark, border, or frame, so the import
script can convert black to alpha for Unity.

## Prompt Template

Use this shape for future Class 4 monsters:

```text
Create a single centered 2D RPG attack VFX sprite for [monster name].
The effect must match the monster artwork: [specific silhouette/material/color
motifs from the character art].
Make it original and recognizable, not a generic slash, fireball, explosion, or
shared family effect.
Pure black #000000 background. No character, no UI, no text, no watermark, no
border, no frame. High contrast luminous fantasy game effect, readable at small
mobile battle size.
```

## Import

Run the importer after adding or replacing a source PNG:

```bash
/Users/andou/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3 tools/import_image_generated_class4_attack_effects.py
```

The importer creates four frames per monster:

- frame 0: restrained emergence
- frame 1: expansion
- frame 2: brightest impact frame
- frame 3: fading overshoot

For horizontal beam effects such as `spirit_queen_titania_staff_beam`, the importer
preserves a wide canvas and varies opacity/brightness instead of converting the
effect into a centered square burst.

`tools/generate_monster_attack_effect_assets.swift` intentionally preserves
Class 3+ image-generated frames so lower-tier procedural regeneration does not
overwrite these bespoke assets.

## Runtime Binding

Each Class 4 monster must map to its own `ImageGeneratedMonsterAttackEffectPath`
entry in `BattleSceneController.MonsterAttackEffects`. Do not point a new Class 4
monster at `PremiumDragonAttackEffectPath`, `PremiumImpactAttackEffectPath`,
`PremiumRobotAttackEffectPath`, `PremiumSwordAttackEffectPath`, or
`PremiumMagicAttackEffectPath` unless it is only a temporary placeholder during
asset production.

The current audit for this set is:

- `tools/reports/class4_attack_effect_audit_20260529.json`
- `tools/reports/class4_character_effect_pair_montage.png`
- `tools/reports/class4_image2_sources_montage.png`
- `tools/reports/class4_attack_effect_frames_montage.png`
