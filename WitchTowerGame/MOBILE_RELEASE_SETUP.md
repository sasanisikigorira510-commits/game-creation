# Mobile Release Setup

This project has been prepared for iPhone and Android release with baseline Unity settings.

Configured in project:
- Bundle identifier: `com.andou.witchtowergame`
- iOS build number: `1`
- Android bundle version code: `1`
- Landscape-only autorotation
- Android minimum SDK: `26`
- Android target architectures: ARMv7 + ARM64
- iOS automatic signing: enabled
- Placeholder mobile icons assigned from `Assets/Branding/AppIcon.png`
- Placeholder splash backgrounds assigned from existing project art
- Release-only safety gates keep preview roster, unfinished IAP, and unfinished ads disabled by default
- Versioned atomic save writes with previous-save backup and automatic recovery
- Unity EditMode release-safety tests (`WitchTower > Run Edit Mode Tests`)
- Build preflight validation (`WitchTower > Validate Release Readiness`), also enforced before mobile builds

Still required before store submission:
- Replace the placeholder app icon with final branding artwork
- Set your real Apple Developer Team in Unity
- Sign in to Xcode with the Apple account used for release
- Create or select the Android keystore used for release signing
- Confirm the final app name, company name, and bundle identifier
- Add store listing assets and privacy information in App Store Connect / Play Console

Recommended quick checks in Unity:
- Run `WitchTower > Run Edit Mode Tests` and confirm the completion log has `failed=0`
- Run `WitchTower > Validate Release Readiness` and resolve every blocker
- Run `python3 tools/unity_smoke_check.py` while Unity has the project open
- `Build Settings` -> switch once to `iOS`
- `Build Settings` -> switch once to `Android`
- `Project Settings > Player` -> confirm icons and splash show correctly
- Run on one iPhone device and one Android device before making release builds
