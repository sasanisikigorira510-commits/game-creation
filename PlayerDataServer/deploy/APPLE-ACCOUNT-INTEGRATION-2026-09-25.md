# Apple連携へのトークン統合・確認付き解除（開発検証）

## 今回の変更

- `account_linking.py`: 交換前の予約記録、暗号化grant保存後の連携、保存済み結果の再取得、
  結果不明時の安全停止。確認付きunlink、解除と復旧challenge無効化の同時確定。
- `apple_grants.py`: 失効世代を追加。認証処理中の解除と遅延応答の競合を検出。
- `apple_tokens.py`: Appleが明示的に拒否したcodeと、処理結果不明を区別。
- `application.py`: authorizationCode受け取り、unlink preview/commitの2ルート。
- `AppleNativeIdentity.cs` / `NasusAppleIdentity.mm`: nativeのauthorizationCodeを一時的に渡す。
- `OnlinePlayerData.Apple.cs`: code必須、結果だけの再確認、解除の確認画面と結果再確認。
- `tests/test_account_grants.py`: 統合26件。
- `tests/test_production.py`: 本番で新ルート/テーブルが有効にならないことの確認を拡張。
- `tests/apple_unity_fixture.py` / `AppleAccountTransportTests.cs`: 実暗号化vaultと合成Apple交換、
  認証応答欠落→結果再取得→復旧→連携解除の一連の通信テスト。
- README/連携仕様/トークン管理仕様を更新。新規画像・プレハブは追加していない。

## 確認結果

- Python全140件成功、失敗0、スキップ0。前段114件から統合26件追加。
- Unity 6000.3.11f1の関連95件成功、失敗0、スキップ0。
  結果: `/tmp/nasus-apple-unlink-20260925.xml`。
  認証コードが欠けている時は送信せず、通信後にcode/JWT/refresh tokenが
  端末のJSONへ残らないことを確認。合成の購入120個・召喚1体の未反映差分を復旧し、
  連携解除後も無償600・有償120・仲間1体・PlayerIdが変わらないことを確認。
- Pythonでは、連打・並行verify/confirm、保存失敗、challenge期限切れ、資格情報変更、
  unlink中の遅延応答、解除queueの原子性、無効な確認token、失効待ちの復旧拒否を検証。
- `pip check`成功。iPhoneOS26.5 SDK、arm64、iOS15.0対象のObjective-C++構文/型チェック成功。
  署名付きフルビルド・実認証試験を意味しない。

## 変更していないもの / 残作業

- `PlayerDataService.json`のBaseUrlは空、開発機能フラグはfalse相当、本番factoryも未接続。
- VPS/DB/Caddy/Apple Developer/署名鍵/暗号鍵/実iPhoneのセーブには変更なし。
- 解除はゲームアカウント削除ではない。ゲームアカウント削除と確認画面は未実装。
- Apple実認証/失効、OS/サーバー署名通知、定期失効ワーカー・滞留監視・鍵配置は未実施。
- 交換結果不明(requested/uncertain)は自動再送せず停止する。運営解消手順と保持期限は未実装。
- iPhoneの表示/操作、バックグラウンド・容量不足・電源断、実Sandbox購入後の復旧は未確認。
- 公開前の全ゲートは[トークン管理](APPLE-TOKEN-LIFECYCLE.md)参照。
