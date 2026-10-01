# Castle of Ideas

講義・交流に使える汎用 ChilloutVR ワールド **Castle of Ideas** です。実会場の座席図を比率の参考にしながら、次の三領域を一つのワールドとして構成します。

- **Main Lecture Hall**: 暗めの魔法学校／ダークアカデミア調の講義室
- **White Literary Salon**: 白を基調とした明るいロリータ文学調の自習室
- **Connecting Corridor**: 二部屋とエレベーターホールをつなぐ廊下

特定作品のロゴ、校章、固有意匠は使わず、石、濃色木材、真鍮、白塗装、淡色ファブリック、古書といった一般的なモチーフで雰囲気を作ります。

## 現在の成果物

この初版は、寸法と素材を変更しやすいソース主体のブロックアウトです。

- `design/layout.json`: メートル単位の寸法、家具列、色スロット
- `blender/generate_world.py`: Blender シーンを再生成するソース
- `blender/castle-of-ideas.blend`: 上記スクリプトから生成する編集用ファイル
- `unity/`: ChilloutVR CCK を導入する Unity 2022.3.58f1 プロジェクト骨格
- `preview/blockout.png`: 天井を非表示にした俯瞰確認画像
- `preview/elevator-interior.png`: EV 内装とスポーン位置の確認画像

Blender ファイルと FBX は生成物ですが、同じ結果を作るスクリプトと寸法 JSON を残します。テクスチャや細かな寸法は、JSON、Blender、Unity のいずれからでも調整できます。

## 会場図から採用した配置

- 左側に縦長のメイン講義室
- メインは前方スクリーン、2 列 × 6 段の机
- 右側に横長のサブ自習室
- サブは壁面スクリーン、横並びのグループ机
- サブ南側に廊下、中央付近にエレベーター前室
- 主スポーンはエレベーター内。開いた扉越しに廊下を向いた状態で到着する

元図に実寸縮尺はなかったため、VR で歩きやすい寸法を仮定しています。寸法の正本は `design/layout.json` です。

## エレベーター（EV）

EV はスポーン地点を兼ねる導入空間です。濃色木パネル、真鍮、深い青の床、背面ミラー、青白い天井灯で、メイン部屋のダークアカデミアとサブ部屋の明るさをつないでいます。初期状態では扉を開け、次の編集用マーカーを Blender と FBX に残します。

- `SPAWN_PRIMARY_EV`: CVRWorld の主スポーン候補
- `ELEVATOR_DOOR_TRIGGER`: 扉開閉の検知位置
- `ELEVATOR_DOOR_LEFT_CLOSED` / `ELEVATOR_DOOR_RIGHT_CLOSED`: 閉扉アニメーションの目標位置
- `ELEVATOR_DING_AUDIO`: 到着音の Audio Source 取付位置

扉の同期アニメーションと到着音は CCK 導入後に実装します。

## 生成

Blender 5.1 で確認しています。

```bash
make cvxr-world
```

または直接実行します。

```bash
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup \
  --python cvxr/castle-of-ideas/blender/generate_world.py -- \
  --output cvxr/castle-of-ideas/blender/castle-of-ideas.blend \
  --export-fbx cvxr/castle-of-ideas/unity/Assets/CastleOfIdeas/Models/castle-of-ideas.fbx \
  --render cvxr/castle-of-ideas/preview/blockout.png \
  --render-elevator cvxr/castle-of-ideas/preview/elevator-interior.png
```

## Unity / CCK

1. [公式セットアップページ](https://docs.chilloutvr.net/cck/setup/)から Unity **2022.3.58f1** を導入する
2. `unity/` を同バージョンで開く
3. ChilloutVR 公式配布の **CCK 4.0.1** をクリーンインポートする
4. Unity メニューの `Castle of Ideas > Build Complete CCK World` を実行する
5. 必要なら `Castle of Ideas > Validate World Scene` で構成を再検証する
6. CCK の Builder で検証し、Local Test を行う

この端末の Unity 2022.3.58f1（Apple Silicon）と Windows Build Support (Mono) で、プロジェクト読込、CCK コンパイル、シーン生成まで確認済みです。`Build Complete CCK World` は、EV スポーン、共有 Video Player、異界の門の取付位置、全座席の着席アクションを再生成してから検証します。

CCK Builder の `Test in PlayMode` では、EV スポーンの目線位置に置いた `WORLD_REFERENCE_CAMERA` から静止プレビューできます。これは `CVRWorld.referenceCamera` として本番のプレイヤーカメラ設定にも利用されます。PlayMode テストはフルのプレイヤーシミュレーターではないため、移動や着席まで試す場合は Windows の ChilloutVR クライアントを使う `Local Test` を利用します。Game View に `No cameras rendering` と出る場合は、PlayMode を止めて `Castle of Ideas > Repair PlayMode Preview Camera` を実行してください。この修復処理はスポーンを廊下向きに直し、Blender からの軸変換に合わせて Unity 側の補助照明も正しい位置へ戻します。

Mac から Windows 用プレビューを作る場合は、Build Settings の対象を `Windows`（Intel 64-bit）にしたうえで、Unity メニューの `Castle of Ideas > Build Windows Preview` を実行します。`unity/build/castle-of-ideas-windows-preview.zip` が生成され、Windows 側ではUnityを導入せず、展開後に同梱の `launch-castle-of-ideas.cmd` からChilloutVRのオフラインプレビューを起動できます。生成物はGit管理しません。

CCK は公式 Unity Package をローカル導入し、配布物そのものはリポジトリに含めません。確認に使用したパッケージは次の通りです。

- Version: `4.0.1`
- Source: `https://files.chilloutvr.net/cck/CCK_4.0.1_Release.unitypackage`
- SHA-256: `2381461837ab5b18a82db456dcc3c34a8d85b8713d464e6da76530a770953517`

## 配信スクリーン

メインとサブにそれぞれ 16:9 スクリーンを用意します。講演本線は CCK の [`CVR Video Player`](https://docs.chilloutvr.net/cck/components/cvr-video-player/) 1台だけで再生し、同じ `ProgramFeed.renderTexture` を両方のスクリーン材質で共有します。これにより、各参加者のPCで同じHLSを二重に取得・デコードしません。講演音声は両室共通の2D音声です。

ワールドはURL未設定の安全な待機状態を既定とし、イベント固有の配信先はローカル設定として分離します。`unity/Assets/CastleOfIdeas/Config/streaming.example.json` を同じ場所の `streaming.local.json` へコピーし、視聴URLを設定してから `Castle of Ideas > Build Complete CCK World` または `Apply Local Streaming Configuration` を実行します。ローカル設定はGit管理されず、URLはUnityのログにも出しません。

`interactiveUi` は既定で `false` です。現在のCCK標準UIだけでは「インスタンスオーナーだけ表示」を保証できないため、固定URLを自動再生する初期構成ではUI自体を出しません。将来オーナー専用操作を実装するまでは、URL変更時にローカル設定からワールドを再ビルドします。ただし、映像は各参加者のPCが直接取得するため、ワールドへ設定した視聴URLを参加者から完全に秘匿することはできません。

### SEDEC連携

SEDEC向けプリセットでは、配信元や管理用URLをワールドへ入れず、`https://sedec-doujin.jp/live/<viewer-key>/index.m3u8` 形式の視聴URLだけをローカル設定へ入れます。OBS用publish keyとは別で、viewer keyのリンクをserver側で外せば視聴URLだけを失効できます。このプリセット以外のワールド本体・アセット・実行時オブジェクトにはSEDEC固有名を含めません。

## 異界の門

白いサブ自習室の北東に、リアル側サブ自習室とつなぐ小型16:9表示面を設けます。これは講演スクリーンとは別系統です。現段階ではフレームと表示面に加えて、次の編集用マーカーだけを配置します。

- `PORTAL_BRIDGE_PLAYER`: 将来の低遅延 Video Player 取付位置
- `PORTAL_BRIDGE_VIEW_ANCHOR`: VR参加者が門を覗く基準位置
- `PORTAL_BRIDGE_VOICE_ANCHOR`: CVRボイスの音響設計基準
- `PORTAL_BRIDGE_CAMERA_TARGET`: リアル側へ返すCVR画面の構図基準

低遅延RTMPなどの方式は実機検証前なので、`PORTAL_BRIDGE_PLAYER` にはまだCCKコンポーネントを付けません。講演HLSへ異界の門を混ぜず、方式が決まった段階で独立した `bridge` 系統として接続します。

## ギミック計画

- [x] CCK コンポーネントを後から付けられるスクリーン階層
- [x] 全椅子の `SeatPoint` / `ExitPoint` マーカー
- [x] 部屋ごとの照明グループとスイッチ取付位置
- [x] EV 内の主スポーン、廊下の安全復帰位置、各部屋の制作時テスト位置
- [x] EV 内装、開扉状態、閉扉目標・トリガー・到着音マーカー
- [x] 講演用 `CVR Video Player` の一重化、両室RenderTexture共有、2D音声
- [x] Git管理外のローカル設定からHLS視聴URLを注入
- [ ] インスタンスオーナー専用のVideo Player操作UI
- [x] 異界の門の表示面・視点・音声・プレイヤー取付マーカー
- [ ] 異界の門の低遅延方式を実機検証して接続
- [x] `CVR Interactable` による着席
- [ ] ドア、照明、配信音量の操作
- [x] CCK Local Test用Windows Previewの生成
- [ ] 非公開アップロードと複数人同期確認
- [ ] ライトベイク、Occlusion Culling、LTCGI

配信の実 URL、エレベーター扉、照明操作など、運用や同期方式に依存する部分は引き続き Unity 側で仕上げます。
