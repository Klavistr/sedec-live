# Unity project

ChilloutVR CCK 4 用の Unity プロジェクト骨格です。公式要件に合わせて Unity 2022.3.58f1 を指定しています。

CCK とその依存物はライセンスおよび更新手順の都合で同梱しません。Unity を開く前に親ディレクトリの README を確認してください。

生成された FBX が `Assets/CastleOfIdeas/Models/` にある状態で、Unity メニューから次を実行します。

1. `Castle of Ideas > Build World Scene`
2. CCK 4 をインポート
3. `Castle of Ideas > Attach Available CCK Components`

講演HLSの受信URLは、親ディレクトリの `streaming.local.json` からビルド用コピーへだけ注入します。リポジトリ上のシーンには保存されません。親ディレクトリの `streaming.local.example.json` をコピーして視聴用URLを設定してください。Preview、CCK Local Test、本番アップロードではこの設定が必須です。PlayModeは設定なしでも起動できます。

Windows Previewの問題を調べる場合は `launch-castle-of-ideas-debug.cmd` から起動します。別のPowerShell窓がChilloutVRの `Player.log` を追跡し、Video Player、HLSと `[CastleOfIdeas]` の行を表示します。HTTPS URLはログ表示時に伏せ字にします。

メイン／サブの講演スクリーンは `ProgramFeed.renderTexture` を共有します。音声は2D Audio Sourceから `ProgramAudio.mixer` へ送り、初期音量は0 dBです。RCではワールド内の配信コンパネを撤去しています。`PORTAL_BRIDGE_PLAYER` はメイン講義室へ入った正面の奥壁にある、70型BIG PADの縦横2倍の低遅延方式検証用マーカーです。初期HLS構成ではVideo Playerを付けません。

初回セットアップ後は、生成された `.meta`、シーン、Material、RenderTexture をコミット対象にします。`Library/`、`Temp/`、`Logs/`、ユーザー固有設定はコミットしません。

`Build Complete CCK World` は写真素材のStandard材質（色＋簡易法線）14種と、アリス室の連続壁紙4面を割り当て、3 m天井の当たり判定と暖炉の標準Particle Systemを構成します。炎は `AnalyticFlame.shader` の数式で揺らぎ、ランタイムのカスタムC#スクリプトやビットマップ素材は使いません。`Validate World Scene` で天井高、材質、暖炉と柱の衝突、幅1.8 mの机6台と椅子の15 cm内寄せ、炎の構成も検証します。`Render Interior Previews` は講義室・暖炉・アリス室・廊下の画像を `../preview/` に出力します。

室内プレビューは編集モードで撮影します。シーンの表示確認用で、配信用URLの注入や再生は行いません。
