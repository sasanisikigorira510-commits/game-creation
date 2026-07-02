# BGM Resources

`AudioManager` loads BGM from this folder by Resources key.

Use these filenames first. WAV is fine for draft loops; Ogg Vorbis is preferred for final mobile builds.

Draft BGM clips in this folder should be imported as Streaming/Vorbis with background loading enabled so long loops do not sit fully decompressed in memory. `BgmAudioImportSettingsPostprocessor` applies the same settings to new BGM clips.

- `home_theme_loop.ogg`
- `home_theme_loop.wav`
- `battle_normal_loop.ogg`
- `battle_normal_loop.wav`
- `summon_chamber_loop.ogg`
- `summon_chamber_loop.wav`
- `fusion_ritual_loop.ogg`
- `fusion_ritual_loop.wav`
- `battle_boss_loop.ogg`
- `battle_boss_loop.wav`

Optional stem files use the same key plus suffix:

- `_base`
- `_rhythm`
- `_melody`
- `_tension`

Example:

- `home_theme_base.ogg`
- `home_theme_rhythm.ogg`
- `home_theme_melody.ogg`
- `home_theme_tension.ogg`
