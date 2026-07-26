# SE Resources

`AudioManager` loads these clips by `AudioCue` before falling back to generated runtime tones.

Short SE clips are imported as Decompress On Load / ADPCM by `BgmAudioImportSettingsPostprocessor` so UI and battle cues start immediately on mobile.

Priority cue mapping:

- `UiClick`: `ui_click.wav`
- `UiConfirm`: `ui_confirm.wav`
- `UiCancel`: `ui_cancel.wav`
- `Attack`: `attack_swing.wav`
- `Hit`: `hit_impact.wav`
- `CriticalHit`: `critical_hit.wav`
- `GachaStart`: `summon_start.wav`
- `GachaReveal`: `summon_reveal.wav`
- `GachaRareReveal`: `summon_rare.wav`
- `GachaLegendaryReveal`: `summon_legendary.wav`
- `Victory`: `victory_fanfare.wav`
- `FusionStart`: `fusion_start.wav`
- `Fusion`: `fusion_mix.wav`
- `FusionSuccess`: `fusion_success.wav`

Other supported cue files:

- `skill_cast.wav`
- `battle_start.wav`
- `defeat.wav`
- `reward.wav`
- `level_up.wav`
- `error.wav`
- `equipment_drop.wav`
- `mission_complete.wav`
- `daily_reward.wav`
- `upgrade_success.wav`
- `upgrade_fail.wav`
- `upgrade_break.wav`
- `enemy_defeat.wav`
- `ally_defeat.wav`

Regenerate the current procedural WAV set with:

```sh
python3 tools/generate_final_se_assets.py
```
