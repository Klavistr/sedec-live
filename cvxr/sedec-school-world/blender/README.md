# Blender source

`generate_world.py` と `../design/layout.json` が再生成可能な正本です。生成後の `.blend` は通常の Blender ファイルとして自由に編集できます。

## 調整の入口

- 部屋寸法・座席列: `../design/layout.json`
- 形状や装飾密度: `generate_world.py`
- 表面・質感: 生成後の `.blend` 内にある `MAT_*` マテリアル

スクリプトを再実行すると `.blend` と FBX は上書きされます。Blender で直接行った変更を残す場合は、別名ファイルまたは別ブランチへ保存してください。

生成 FBX は Unity の `Assets/SEDECWorld/Models/` に出力されます。Unity 側で作った Material やシーンはFBXの外に置き、再生成で消えないようにします。
