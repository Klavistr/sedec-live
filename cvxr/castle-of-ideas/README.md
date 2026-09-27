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

この端末の Unity 2022.3.58f1（Apple Silicon）と Windows Build Support (Mono) で、プロジェクト読込、CCK コンパイル、シーン生成まで確認済みです。`Build Complete CCK World` は、EV スポーン、2 台の Video Player、全座席の着席アクションを再生成してから検証します。

CCK Builder の `Test in PlayMode` では、EV スポーンの目線位置に置いた `WORLD_REFERENCE_CAMERA` から静止プレビューできます。これは `CVRWorld.referenceCamera` として本番のプレイヤーカメラ設定にも利用されます。PlayMode テストはフルのプレイヤーシミュレーターではないため、移動や着席まで試す場合は Windows の ChilloutVR クライアントを使う `Local Test` を利用します。Game View に `No cameras rendering` と出る場合は、PlayMode を止めて `Castle of Ideas > Repair PlayMode Preview Camera` を実行してください。この修復処理はスポーンを廊下向きに直し、Blender からの軸変換に合わせて Unity 側の補助照明も正しい位置へ戻します。

Mac から Windows 用プレビューを作る場合は、Build Settings の対象を `Windows`（Intel 64-bit）にしたうえで、Unity メニューの `Castle of Ideas > Build Windows Preview` を実行します。`unity/build/castle-of-ideas-windows-preview.zip` が生成され、Windows 側ではUnityを導入せず、展開後に同梱の `launch-castle-of-ideas.cmd` からChilloutVRのオフラインプレビューを起動できます。生成物はGit管理しません。

CCK は公式 Unity Package をローカル導入し、配布物そのものはリポジトリに含めません。確認に使用したパッケージは次の通りです。

- Version: `4.0.1`
- Source: `https://files.chilloutvr.net/cck/CCK_4.0.1_Release.unitypackage`
- SHA-256: `2381461837ab5b18a82db456dcc3c34a8d85b8713d464e6da76530a770953517`

## 配信スクリーン

メインとサブにそれぞれ 16:9 スクリーンを用意します。CCK の [`CVR Video Player`](https://docs.chilloutvr.net/cck/components/cvr-video-player/) と RenderTexture を割り当て、任意の配信URLを設定できる汎用構成にします。

ワールドは汎用利用できる待機状態を既定とし、イベント固有の配信先はプリセットとして分離します。URL入力・停止・切替UIはインスタンスオーナーだけに表示します。ただし、映像は各参加者のPCが直接取得し、同期時にはURL自体も各クライアントへ渡るため、UIを隠すだけではURLを秘匿できません。

### SEDEC連携

SEDEC向けプリセットでは、配信元や管理用URLをワールドへ入れず、短期間だけ有効な中継URLを `sedec-server` 側で発行・失効できる構成にします。このプリセット以外のワールド本体・アセット・実行時オブジェクトにはSEDEC固有名を含めません。

## ギミック計画

- [x] CCK コンポーネントを後から付けられるスクリーン階層
- [x] 全椅子の `SeatPoint` / `ExitPoint` マーカー
- [x] 部屋ごとの照明グループとスイッチ取付位置
- [x] EV 内の主スポーン、廊下の安全復帰位置、各部屋の制作時テスト位置
- [x] EV 内装、開扉状態、閉扉目標・トリガー・到着音マーカー
- [ ] `CVR Video Player` の中継URL、同期、音声出力
- [ ] インスタンスオーナー専用のVideo Player操作UI
- [x] `CVR Interactable` による着席
- [ ] ドア、照明、配信音量の操作
- [x] CCK Local Test用Windows Previewの生成
- [ ] 非公開アップロードと複数人同期確認
- [ ] ライトベイク、Occlusion Culling、LTCGI

配信の実 URL、エレベーター扉、照明操作など、運用や同期方式に依存する部分は引き続き Unity 側で仕上げます。
