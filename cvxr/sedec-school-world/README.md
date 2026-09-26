# SEDEC School World

SEDEC の講義・交流に使う ChilloutVR ワールドです。実会場の座席図を比率の参考にしながら、次の三領域を一つのワールドとして構成します。

- **Main Lecture Hall**: 暗めの魔法学校／ダークアカデミア調の講義室
- **White Literary Salon**: 白を基調とした明るいロリータ文学調の自習室
- **Connecting Corridor**: 二部屋とエレベーターホールをつなぐ廊下

特定作品のロゴ、校章、固有意匠は使わず、石、濃色木材、真鍮、白塗装、淡色ファブリック、古書といった一般的なモチーフで雰囲気を作ります。

## 現在の成果物

この初版は、寸法と素材を変更しやすいソース主体のブロックアウトです。

- `design/layout.json`: メートル単位の寸法、家具列、色スロット
- `blender/generate_world.py`: Blender シーンを再生成するソース
- `blender/sedec-school-world.blend`: 上記スクリプトから生成する編集用ファイル
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
  --python cvxr/sedec-school-world/blender/generate_world.py -- \
  --output cvxr/sedec-school-world/blender/sedec-school-world.blend \
  --export-fbx cvxr/sedec-school-world/unity/Assets/SEDECWorld/Models/sedec-school-world.fbx \
  --render cvxr/sedec-school-world/preview/blockout.png \
  --render-elevator cvxr/sedec-school-world/preview/elevator-interior.png
```

## Unity / CCK

1. [公式セットアップページ](https://docs.chilloutvr.net/cck/setup/)から Unity **2022.3.58f1** を導入する
2. `unity/` を同バージョンで開く
3. ChilloutVR 公式配布の **CCK 4.0.1** をクリーンインポートする
4. Unity メニューの `SEDEC > Build Complete CCK World` を実行する
5. 必要なら `SEDEC > Validate World Scene` で構成を再検証する
6. CCK の Builder で検証し、Local Test を行う

この端末の Unity 2022.3.58f1（Apple Silicon）と Windows Build Support (Mono) で、プロジェクト読込、CCK コンパイル、シーン生成まで確認済みです。`Build Complete CCK World` は、EV スポーン、2 台の Video Player、全座席の着席アクションを再生成してから検証します。

CCK は公式 Unity Package をローカル導入し、配布物そのものはリポジトリに含めません。確認に使用したパッケージは次の通りです。

- Version: `4.0.1`
- Source: `https://files.chilloutvr.net/cck/CCK_4.0.1_Release.unitypackage`
- SHA-256: `2381461837ab5b18a82db456dcc3c34a8d85b8713d464e6da76530a770953517`

## 配信スクリーン

メインとサブにそれぞれ 16:9 スクリーンを用意します。最終的には CCK の [`CVR Video Player`](https://docs.chilloutvr.net/cck/components/cvr-video-player/) と RenderTexture を割り当て、`sedec-server` が公開する安定した配信 URL を設定します。

配信 URL はまだ確定していないため、Git 管理するファイルには実URLを入れません。`unity/Assets/SEDECWorld/Config/streaming.example.json` をコピーしてローカル設定を作る想定です。

## ギミック計画

- [x] CCK コンポーネントを後から付けられるスクリーン階層
- [x] 全椅子の `SeatPoint` / `ExitPoint` マーカー
- [x] 部屋ごとの照明グループとスイッチ取付位置
- [x] EV 内の主スポーン、廊下の安全復帰位置、各部屋の制作時テスト位置
- [x] EV 内装、開扉状態、閉扉目標・トリガー・到着音マーカー
- [ ] `CVR Video Player` の実URL、同期、音声出力
- [x] `CVR Interactable` による着席
- [ ] ドア、照明、配信音量の操作
- [ ] CCK Local Test と複数人同期確認
- [ ] ライトベイク、Occlusion Culling、LTCGI

配信の実 URL、エレベーター扉、照明操作など、運用や同期方式に依存する部分は引き続き Unity 側で仕上げます。
