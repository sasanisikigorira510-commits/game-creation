# Equipment enhancement image2 sources

These source sheets were generated with the built-in image2 flow for the equipment enhancement overlay.

- `enhance_success_image2_sheet.png`: 4x2 success animation sheet, used for `EnhanceSuccess_0..7` and `EnhanceRuneCircle`.
- `enhance_fail_image2_sheet.png`: 4x2 failure animation sheet, used for `EnhanceFail_0..7`.
- `rejected_destroy_image2_sheet_has_monster.png`: rejected as a full animation because several frames contain a monster-like silhouette. Only the abstract explosion/smoke frames are sampled by `tools/generate_equipment_enhancement_effect_assets.swift` for the destroy effect.

The runtime assets are saved under `WitchTowerGame/Assets/Resources/UI/EquipmentEnhance`.
