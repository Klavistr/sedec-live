# Unity project

ChilloutVR CCK 4 用の Unity プロジェクト骨格です。公式要件に合わせて Unity 2022.3.58f1 を指定しています。

CCK とその依存物はライセンスおよび更新手順の都合で同梱しません。Unity を開く前に親ディレクトリの README を確認してください。

生成された FBX が `Assets/CastleOfIdeas/Models/` にある状態で、Unity メニューから次を実行します。

1. `Castle of Ideas > Build World Scene`
2. CCK 4 をインポート
3. `Castle of Ideas > Attach Available CCK Components`

講演HLSを設定する場合は、`Assets/CastleOfIdeas/Config/streaming.example.json` を `streaming.local.json` として同じディレクトリへコピーし、viewer URLを入力します。その後 `Build Complete CCK World` を実行すると、URLをログへ表示せず、1台の同期プレイヤーへ設定します。`streaming.local.json` はGit管理されません。

メイン／サブの講演スクリーンは `ProgramFeed.renderTexture` を共有します。`PORTAL_BRIDGE_PLAYER` は低遅延方式の検証用マーカーで、初期HLS構成ではVideo Playerを付けません。

初回セットアップ後は、生成された `.meta`、シーン、Material、RenderTexture をコミット対象にします。`Library/`、`Temp/`、`Logs/`、ユーザー固有設定はコミットしません。
