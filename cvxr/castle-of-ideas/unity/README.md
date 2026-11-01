# Unity project

`Castle of Ideas > Build Windows Preview` は同じバージョンの比較用ZIPを2種類生成します。

- `castle-of-ideas-windows-preview-spatial.zip`: 立体音響・講演ゲイン+6 dB。`launch-castle-of-ideas-spatial.cmd` から起動。
- `castle-of-ideas-windows-preview-stereo.zip`: 講演音声を標準Directモードの2Dステレオ・0 dBで再生。距離減衰なし。`launch-castle-of-ideas-stereo.cmd` から起動。

各ZIPを展開して使います。比較時はChilloutVRを終了してから別版を起動し、クライアント・Windows・ヘッドセットの音量を同じに保ってください。ワールドのローカルテストIDも版ごとに分けます。講演とポータルの両方が比較対象です。立体音響版のポータルは表示面中央の見えない1音源からモノラルで鳴らし、+6 dBと講義室と同じ緩い距離減衰を適用します。スピーカーのメッシュは追加しません。2D版ではポータルもDirectステレオ・0 dBです。各版の受信機は講演1台＋任意ポータル1台のままで、Luaやワールド内の切替UIは使いません。ソースシーンは立体音響版を維持し、ステレオ化は一時ビルド用コピーにだけ適用します。

ChilloutVR CCK 4 用の Unity プロジェクト骨格です。公式要件に合わせて Unity 2022.3.58f1 を指定しています。

CCK とその依存物はライセンスおよび更新手順の都合で同梱しません。Unity を開く前に親ディレクトリの README を確認してください。

生成された FBX が `Assets/CastleOfIdeas/Models/` にある状態で、Unity メニューから次を実行します。

1. `Castle of Ideas > Build World Scene`
2. CCK 4 をインポート
3. `Castle of Ideas > Attach Available CCK Components`

講演HLSの受信URLは、親ディレクトリの `streaming.local.json` からビルド用コピーへだけ注入します。リポジトリ上のシーンには保存されません。親ディレクトリの `streaming.local.example.json` をコピーして視聴用URLを設定してください。Preview、CCK Local Test、本番アップロードではこの設定が必須です。PlayModeは設定なしでも起動できます。

Windows Previewの問題を調べる場合は `launch-castle-of-ideas-debug.cmd` から起動します。別のPowerShell窓がChilloutVRの `Player.log` を追跡し、Video Player、HLSと `[CastleOfIdeas]` の行を表示します。HTTPS URLはログ表示時に伏せ字にします。

メイン／サブの講演スクリーンは `ProgramFeed.renderTexture` を共有します。1台の講演受信機からRoom ScaleのL/R音声を各室のスピーカー計4台へ送り、部屋の中ほどで−6 dB以内となるカスタム距離減衰を使います。`ProgramAudio.mixer` の初期ゲインは+6 dB（振幅で約2倍）です。ベータ版ではワールド内の配信コンパネを撤去しています。`PORTAL_BRIDGE_PLAYER` は独立した任意のHLS受信機で、`portalReceiverUrl` をビルド用コピーへ注入した場合だけ自動再生します。

初回セットアップ後は、生成された `.meta`、シーン、Material、RenderTexture をコミット対象にします。`Library/`、`Temp/`、`Logs/`、ユーザー固有設定はコミットしません。

`Build Complete CCK World` は写真素材のStandard材質（色＋簡易法線）14種と、アリス室の連続壁紙4面を割り当て、3 m天井の当たり判定と暖炉の標準Particle Systemを構成します。炎は `AnalyticFlame.shader` の数式で揺らぎ、ランタイムのカスタムC#スクリプトやビットマップ素材は使いません。`Validate World Scene` で天井高、材質、暖炉と柱の衝突、幅1.8 mの机6台と椅子の15 cm内寄せ、炎の構成も検証します。`Render Interior Previews` は講義室・暖炉・アリス室・廊下の画像を `../preview/` に出力します。

室内プレビューは編集モードで撮影します。シーンの表示確認用で、配信用URLの注入や再生は行いません。
