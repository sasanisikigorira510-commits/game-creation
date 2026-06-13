# Equipment Image2 Asset Rebuild

This folder keeps the source inputs used by `tools/rebuild_equipment_image2_assets.swift`.

## Sources

- `image2_sources/equipment_arcane_background_source.png`
  - Existing ChatGPT Image2-generated dark dungeon/ritual background source.
- `legacy_icon_sources/`
  - Pre-rebuild equipment, relic, and lock icon silhouettes used as subject references.

## Output

Run from `game-creation`:

```bash
swift tools/rebuild_equipment_image2_assets.swift
```

The script rewrites the Unity `Resources` PNG files in:

- `WitchTowerGame/Assets/Resources/EquipmentBackgrounds`
- `WitchTowerGame/Assets/Resources/EquipmentIcons`
- `WitchTowerGame/Assets/Resources/EquipmentRelics`
- `WitchTowerGame/Assets/Resources/EquipmentUi`

Unity `.meta` files are intentionally left untouched so existing scene and runtime references keep their GUIDs.

Equipment cards use the same class-based slot frame assets as monster formation:
`MonsterCardFrames/monster_class_<1-6>_slot_frame`.
