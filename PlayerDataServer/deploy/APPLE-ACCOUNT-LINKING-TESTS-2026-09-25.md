# Apple連携: サーバー側の追加検証

2026-09-25、ローカル環境のみ。ユーザーは任意のApple連携方式を承認。

- Python unittest: **84件成功、失敗0、スキップ0**（4.161秒）。
- `pip check`: `No broken requirements found.`
- 新規24件: account linking 17件、署名検証7件。既存60件も成功。
- 合成RSA鍵、一時SQLite、ローカルHTTPのみ。Appleの実アカウント・実購入は未使用。
- nonce/署名/issuer/audience/期限/鍵方式/必須claimの拒否、連携衝突、
  未連携データの復旧拒否、復旧プレビュー後の変更、同時確定、応答再送、
  購入後未アップロードの差分、旧認証情報の無効化、購入検証中の引き継ぎ競合を確認。
- Apple subject・identity token・インストール認証情報の平文をDB/HTTPログに保存しないことを確認。

実行:

```sh
NASUS_TEST_AGE='/Users/andou/Library/Application Support/NasusBackups/tools/age-v1.3.2-darwin/age/age' .venv-production/bin/python -m unittest discover -s tests
.venv-production/bin/python -m pip check
```

変更: `application.py`, `store.py`, `build_release.py`, `README.md`。
追加: `apple_identity.py`, `account_linking.py`, `requirements-identity.txt`,
`tests/test_account_linking.py`, `tests/test_apple_identity.py`, 本記録と仕様メモ。

本番へのファイル転送/インストール、公開ルート変更、実DBへの追加テーブル作成、
Apple Developer設定変更、Unityコード変更、実機インストールは行っていない。
**サーバーの土台のみ。Apple連携全体は未完成で公開不可。**
続きは `APPLE-ACCOUNT-LINKING.md` の必須項目を実装する。
