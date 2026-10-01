# Unity project

ChilloutVR CCK 4 用の Unity プロジェクト骨格です。公式要件に合わせて Unity 2022.3.58f1 を指定しています。

CCK とその依存物はライセンスおよび更新手順の都合で同梱しません。Unity を開く前に親ディレクトリの README を確認してください。

生成された FBX が `Assets/CastleOfIdeas/Models/` にある状態で、Unity メニューから次を実行します。

1. `Castle of Ideas > Build World Scene`
2. CCK 4 をインポート
3. `Castle of Ideas > Attach Available CCK Components`

講演HLSの受信URLはワールドへ埋め込みません。ワールド内の `PROGRAM FEED CONTROL` へHTTPSの `.m3u8` URLを入力し、`URLを適用` を押します。URL操作はインスタンスオーナーだけが同期プレイヤーへ反映でき、`再読込` でHLS開始待ちから復帰できます。入力欄はマスク表示です。

メイン／サブの講演スクリーンは `ProgramFeed.renderTexture` を共有します。音声は2D Audio Sourceから `ProgramAudio.mixer` へ送り、各ユーザーが −60〜+12 dBの範囲でローカル調整できます。`PORTAL_BRIDGE_PLAYER` はサブ会場後方壁にある低遅延方式の検証用マーカーで、初期HLS構成ではVideo Playerを付けません。

初回セットアップ後は、生成された `.meta`、シーン、Material、RenderTexture をコミット対象にします。`Library/`、`Temp/`、`Logs/`、ユーザー固有設定はコミットしません。
