# Unity project

ChilloutVR CCK 4 用の Unity プロジェクト骨格です。公式要件に合わせて Unity 2022.3.58f1 を指定しています。

CCK とその依存物はライセンスおよび更新手順の都合で同梱しません。Unity を開く前に親ディレクトリの README を確認してください。

生成された FBX が `Assets/CastleOfIdeas/Models/` にある状態で、Unity メニューから次を実行します。

1. `Castle of Ideas > Build World Scene`
2. CCK 4 をインポート
3. `Castle of Ideas > Attach Available CCK Components`

講演HLSの受信URLは、親ディレクトリの `streaming.local.json` からビルド用コピーへだけ注入します。リポジトリ上のシーンには保存されません。親ディレクトリの `streaming.local.example.json` をコピーして視聴用URLを設定してください。Preview、CCK Local Test、本番アップロードではこの設定が必須です。PlayModeは設定なしでも起動できます。

Windows Previewの問題を調べる場合は `launch-castle-of-ideas-debug.cmd` から起動します。別のPowerShell窓がChilloutVRの `Player.log` を追跡し、Video Player、HLSと `[CastleOfIdeas]` の行を表示します。HTTPS URLはログ表示時に伏せ字にします。

メイン／サブの講演スクリーンは `ProgramFeed.renderTexture` を共有します。音声は2D Audio Sourceから `ProgramAudio.mixer` へ送り、Luaに依存しないローカルボタンで −12、−6、0、+6、+12 dBを選べます。`PORTAL_BRIDGE_PLAYER` はサブ会場後方壁にある低遅延方式の検証用マーカーで、初期HLS構成ではVideo Playerを付けません。

初回セットアップ後は、生成された `.meta`、シーン、Material、RenderTexture をコミット対象にします。`Library/`、`Temp/`、`Logs/`、ユーザー固有設定はコミットしません。
