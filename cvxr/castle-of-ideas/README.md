# Castle of Ideas

講義・交流に使える汎用 ChilloutVR ワールド **Castle of Ideas** です。実会場の平面図から読み取った主要寸法を基準に、次の三領域を一つのワールドとして構成します。

- **Main Lecture Hall**: 暗めの魔法学校／ダークアカデミア調の講義室
- **White Literary Salon**: 白を基調とした明るいロリータ文学調の自習室
- **Connecting Corridor**: 二部屋とエレベーターホールをつなぐ廊下

特定作品のロゴ、校章、固有意匠は使わず、石、濃色木材、真鍮、白塗装、淡色ファブリック、古書といった一般的なモチーフで雰囲気を作ります。

## 現在の成果物

RC／アーリーアクセス向けに、編集可能なソース、家具、写真素材、配信の自動再生を整えています。

- `design/layout.json`: メートル単位の寸法、家具列、色スロット
- `blender/generate_world.py`: Blender シーンを再生成するソース
- `blender/castle-of-ideas.blend`: 上記スクリプトから生成する編集用ファイル
- `unity/`: ChilloutVR CCK を導入する Unity 2022.3.58f1 プロジェクト骨格
- `preview/blockout.png`: 天井を非表示にした俯瞰確認画像
- `preview/elevator-interior.png`: EV 内装とスポーン位置の確認画像
- `preview/main-interior.png` / `preview/fireplace.png`: 天井を閉じたBlender室内確認画像
- `preview/unity-main-interior.png` / `preview/unity-fireplace.png`: Unity自身で描画した確認画像

Blender ファイルと FBX は生成物ですが、同じ結果を作るスクリプトと寸法 JSON を残します。テクスチャや細かな寸法は、JSON、Blender、Unity のいずれからでも調整できます。

Unityの確認画像は `Castle of Ideas > Render Interior Previews` で再生成します。暖炉の標準Particle Systemを1秒間シミュレートした状態も撮影します。

室内確認画像と組見本はGit管理外です。`Audit Chair Motion In PlayMode` は椅子の動作、固定の着席位置、引いた椅子と机・柱・壁・暖炉の衝突、後ろ壁の壁紙面を検証し、結果を `build/placement-audit.json` に保存します。初期状態では座面を机の下へ寄せ、着席時に0.35秒で引き、離席時に戻します。可動メッシュは静的バッチから外しています。

## 会場図と鏡写しの配置

- 仮想会場では、エレベーターから廊下へ出て**右側**に縦長のメイン講義室
- メインは165インチ相当の16:9モニター、横2セット×奥行き3セットの幅1.8 mの2人掛け机（12席）。椅子の外端は机の端から15 cm内側。中央通路1.3 m、壁側通路1.8 m／1.5 m、暖炉の張り出し部分との間も約0.8 mを確保し、後方は廊下からポータルへの空間を空ける
- **左側**に横長のサブ自習室
- サブは壁面スクリーン、横並びのグループ机
- サブ南側に廊下、中央付近にエレベーター前室
- 主スポーンはエレベーター内。開いた扉越しに廊下を向いた状態で到着する
- メイン入口の正面にあたる奥壁へ、70型電子黒板の縦横2倍のポータルを壁掛けする

この左右関係はリアル会場を反転したものです。互いのポータル越しに鏡写しの会場が続く演出を狙います。メインは8.2 m×12.1 m、サブは短辺6.4 m×長辺13.8 m、廊下は幅1.6 m×長さ13.8 mです。EVはサブの長辺中央に合わせます。廊下の長さは図面の配置を優先する指定に合わせました。元画像は再配布せず、導出した寸法だけを `design/layout.json` に記録しています。

全室・廊下・EVを床から3 mの天井で閉じ、出入口の上端まで壁を充填しています。廊下に面する表面はアリス室側も青石です。主会場の机は濃色の前板・彫刻帯・段付き縁・丸脚、椅子は黒い座面・高い格子背・ねじり柱を提供写真からモデル化しました。演台は置きません。暖炉は全幅1.6 mで、左端を廊下の部屋側の縁からスクリーン方向へ1.4 mの線に合わせ、標準Particle Systemで炎を動かします。講演表示面は元の110インチから縦横1.5倍の約3.653 m×2.055 mとし、両室で同寸法です。

アリス室の板床は幅15.15 cm×長さ90.9 cmを長辺方向に半枚ずらしで並べています。写真の黒い木目を残して白塗装を二値調へ寄せ、木目を反時計回りに4度補正しています。クロスは18 cmの周期で細かく反復します。家具は3グループ・12席とし、前方グループをスクリーンから離しました。照明は魔法学校より淡い暖色です。壁面は全12章から重複のない本文を取り、原典挿絵42点を同じ章のページにだけ配置します。19ページの原稿はISO B判比率（横:縦=1:√2）、各ページは2段組です。ページ内では焦点へ収束しない滑らかな波形で伸縮させ、ページ境界は直線に保ち、アスタリスク等の装飾区切りは除いています。4面はそれぞれ異なる部分を使用し、後ろ壁の白い面材への埋没も直しています。出典と再生成方法は `references/alice/README.md` を参照してください。

講義室の柱は以前の等間隔装飾を撤去し、提供図面の矩形座標を8.2 m×12.1 mの室寸法へ換算して再配置しています。柱の出幅は約66.7 cm、中央の柱は奥行き約101.9 cmです。図面JPEGからの換算でありCAD実測値ではありません。暖炉と重なる柱は生成しません。奥壁中央の柱は既存のポータル表示面と衝突するため省いています。換算に使う画素座標は `design/layout.json` の `mainPiers` に保存しています。

主会場の石壁・床・天井、青く塗った廊下の石、木目、黒い座面、机と暖炉の彫刻は提供写真を切り出した素材です。切り出しレシピと来歴を残し、元写真に採取範囲を重ねた確認画像も生成できます。手順は `blender/README.md` を参照してください。

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

この端末の Unity 2022.3.58f1（Apple Silicon）と Windows Build Support (Mono) で、プロジェクト読込、CCK コンパイル、シーン生成まで確認済みです。`Build Complete CCK World` は、EV スポーン、共有 Video Player、ポータルの取付位置、全座席の着席アクションを再生成してから検証します。

CCK Builder の `Test in PlayMode` では、EV スポーンの目線位置に置いた `WORLD_REFERENCE_CAMERA` から静止プレビューできます。これは `CVRWorld.referenceCamera` として本番のプレイヤーカメラ設定にも利用されます。PlayMode テストはフルのプレイヤーシミュレーターではないため、移動や着席まで試す場合は Windows の ChilloutVR クライアントを使う `Local Test` を利用します。Game View に `No cameras rendering` と出る場合は、PlayMode を止めて `Castle of Ideas > Repair PlayMode Preview Camera` を実行してください。この修復処理はスポーンを廊下向きに直し、Blender からの軸変換に合わせて Unity 側の補助照明も正しい位置へ戻します。

Mac から Windows 用プレビューを作る場合は、Build Settings の対象を `Windows`（Intel 64-bit）にしたうえで、Unity メニューの `Castle of Ideas > Build Windows Preview` を実行します。`unity/build/castle-of-ideas-windows-preview.zip` が生成され、Windows 側ではUnityを導入せず、展開後に同梱の `launch-castle-of-ideas.cmd` からChilloutVRのオフラインプレビューを起動できます。生成物はGit管理しません。

CCK は公式 Unity Package をローカル導入し、配布物そのものはリポジトリに含めません。確認に使用したパッケージは次の通りです。

- Version: `4.0.1`
- Source: `https://files.chilloutvr.net/cck/CCK_4.0.1_Release.unitypackage`
- SHA-256: `2381461837ab5b18a82db456dcc3c34a8d85b8713d464e6da76530a770953517`

## 配信スクリーン

メインとサブにそれぞれ 16:9 スクリーンを用意します。表示面は0〜1のUVを全面に割り当てた専用Quadで、講演本線は CCK の [`CVR Video Player`](https://docs.chilloutvr.net/cck/components/cvr-video-player/) 1台だけで再生し、同じ `ProgramFeed.renderTexture` を両方のスクリーン材質で共有します。これにより、映像をクロップせず、各参加者のPCで同じHLSを二重に取得・デコードしません。

HTTPSの `.m3u8` 受信URLはローカル設定からビルド用コピーへだけ注入し、Video Playerが入室時に自動再生します。RCではワールド内の配信操作コンパネを撤去しました。リポジトリ上のシーンにはURLを保存しません。配信開始後に入室する運用を基本とし、取得開始に失敗した場合はワールドを開き直します。Windows Previewの `launch-castle-of-ideas-debug.cmd` は別のPowerShell窓でVideo Player・HLS関連の `Player.log` を追跡します。

`streaming.local.example.json` を `streaming.local.json` として同じディレクトリへコピーし、視聴用URLを設定してからPreviewまたは本番アップロードを実行します。`streaming.local.json` はGit管理外です。実URL入りZIPにはURLが抽出可能な形で含まれるため、GitHubではDraft Releaseなど限定された場所で扱います。

```json
{
  "receiverUrl": "https://example.invalid/live/viewer-key/index.m3u8"
}
```

講演音声は2D Audio Sourceから専用AudioMixerへ送り、初期音量は0 dBです。RCでは音量プリセットを含むワールド独自のコンパネを撤去しています。映像は各参加者のPCが直接取得するため、視聴URLを参加者から完全に秘匿することはできません。[2026年10月29日のHLS負荷試験](docs/hls-load-test-2026-10-29.md)では、10 Mbpsの25人同時受信は未達でした。

### SEDEC連携

SEDECで使う場合も、配信元や管理用URLをワールドへ入れず、`https://sedec-doujin.jp/live/<viewer-key>/index.m3u8` 形式の受信URLだけをワールド内UIへ入力します。OBS用publish keyとは別で、viewer keyのリンクをserver側で外せば受信URLだけを失効できます。この説明以外のワールド本体・アセット・実行時オブジェクトにはSEDEC固有名を含めません。

## 鏡写しのポータル

メイン講義室の奥壁に、リアル会場とつなぐ16:9表示面を設けます。リアル側の70型BIG PAD参照寸法は保持しつつ、仮想側は縦横2倍に拡大した壁掛け（表示面3.08 m×1.7325 m）です。スタンドを置かず、壁の高さ中央に配置します。柱の再計算後も表示面の位置は維持しています。`physicalReference` は実物の寸法、`virtualScale` は仮想側の拡大率です。

講演スクリーンとは別系統です。現段階では本体、表示面に加えて、次の編集用マーカーだけを配置します。

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
- [x] Git管理外の設定からHLS受信URLをビルド時注入・自動再生
- [x] RC向けにURL入力・再読込・音量プリセットのワールド内コンパネを撤去
- [x] 70型BIG PAD寸法のポータル表示面・視点・音声・プレイヤー取付マーカー
- [ ] ポータルの低遅延方式を実機検証して接続
- [x] `CVR Interactable` による着席
- [ ] ドア、照明の操作
- [x] CCK Local Test用Windows Previewの生成
- [ ] 非公開アップロードと複数人同期確認
- [ ] ライトベイク、Occlusion Culling、LTCGI

配信の実 URL、エレベーター扉、照明操作など、運用や同期方式に依存する部分は引き続き Unity 側で仕上げます。
