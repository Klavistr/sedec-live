# Blender source

`generate_world.py` と `../design/layout.json` が再生成可能な正本です。生成後の `.blend` は通常の Blender ファイルとして自由に編集できます。

## 調整の入口

- 部屋寸法・座席列: `../design/layout.json`
- 講演スクリーン共有・異界の門の配置: `../design/layout.json` の `mediaTopology` と `screens`
- 形状や装飾密度: `generate_world.py`
- 表面・質感: 生成後の `.blend` 内にある `MAT_*` マテリアル
- 写真の切り出し座標: `../design/photo-textures.json`

## 写真素材の再生成

`extract_photo_textures.py` は提供写真の画素を切り出し、四角形補正、反転タイル、明るさ調整、輝度勾配からの簡易法線マップを作ります。画像生成・補完は行いません。元HEICと出力のSHA-256、EXIF回転後の寸法、加工レシピを `texture-provenance.json` に記録します。

写真加工時だけ Pillow が必要です。既存のPillow環境のPythonを `PHOTO_PYTHON` に指定でき、通常の検証用 `.venv` へ追加インストールする必要はありません。HEICの読込にはmacOSの `sips` を使います。

```bash
"$PHOTO_PYTHON" cvxr/castle-of-ideas/blender/extract_photo_textures.py \
  --source-dir /path/to/original-photographs \
  --review-output cvxr/castle-of-ideas/preview/photo-review
make cvxr-world
```

`preview/photo-review/` には元写真に切り出し枠と対角線、素材名を重ねたJPEGと一覧画像が出ます。人物などが写る元写真を含むためGit管理外です。写真素材の出典はUnityの `Textures/PhotoDerived/README.md` と同ディレクトリーの来歴JSONに記録しています。

全室の天井下面は床から3 mです。俯瞰レンダー中だけ天井を隠し、`.blend` 保存時とUnity向けFBXには天井を含めます。炎はBlenderでは静的な形状プレビュー、Unityでは標準Particle Systemと数式シェーダーで動きます。

スクリプトを再実行すると `.blend` と FBX は上書きされます。Blender で直接行った変更を残す場合は、別名ファイルまたは別ブランチへ保存してください。

生成 FBX は Unity の `Assets/CastleOfIdeas/Models/` に出力されます。Unity 側で作った Material やシーンはFBXの外に置き、再生成で消えないようにします。

講演用の `PROGRAM_FEED_PLAYER` はメイン／サブ画面で共有します。`PORTAL_BRIDGE_*` はメイン講義室の奥壁に置く、将来低遅延系統用マーカーです。70型BIG PADを基準に、仮想空間では縦横2倍、スタンドなしで配置します。柱の再計算後もポータル位置を維持し、表示面と衝突する奥壁中央の柱は省いています。Blender生成時点ではネットワーク機能を持ちません。基準寸法は `layout.json` の `screens.portal.physicalReference`、倍率は `virtualScale` で交換できます。

アリス室の本文はHoefler Textで組みます。本文層と挿絵層を別に生成してから一周40.4 mの連続壁紙に合成し、四面へ分割します。元レイヤーと全景はGit管理外の `preview/alice-wallpaper/`、再現可能な配置記録は `design/alice-wallpaper.json` に残ります。出典と再生成方法は `../references/alice/README.md` を参照してください。フォントそのものは同梱しません。
