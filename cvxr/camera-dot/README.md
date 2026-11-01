# Camera Dot

Castle of Ideas のポータルから室内を撮るための小さなChilloutVRアバターです。Unityの基本球体だけを使い、外部の画像・モデル・アニメーションは取り込みません。

## 編集用データ

既存の `../castle-of-ideas/unity/` プロジェクト内に置いています。

- `Assets/CameraDot/Camera-Dot.prefab`: CCK Avatar、目線・声の位置、点の形状
- `Assets/CameraDot/Camera-Dot.unity`: アバターだけを置いた編集・CCKアップロード用シーン
- `Assets/CameraDot/Dot.mat`: 画像を使わない単色材質
- `Assets/CameraDot/CameraDotHumanoid.asset`: 見た目の体を持たない、人型ボーンの定義
- `Assets/CastleOfIdeas/Editor/CameraDotAvatarBuilder.cs`: 再生成とWindows Local Test ZIP作成用のソース

Unityメニューの `Castle of Ideas > Build Camera Dot Avatar` で再生成します。生成元の目線高は1.5 m、点の直径は10 mm、点は頭ボーンに付けて目線の4 cm後方へ置きます。見える体はありませんが、内部には通常のHumanoidリグを持たせ、CVRの人型アバターとして扱います。再生成はPrefabを上書きするので、直接編集を残す場合は複製するか生成ソースを編集してください。既存PrefabのオンラインobjectIdは再生成時に維持します。

## 使い方

1. WindowsでCastle of Ideasのプレビューを起動する。
2. `camera-dot-windows-preview.zip` を展開し、`launch-camera-dot.cmd` を実行する。ワールドではなくアバターを読み込みます。
3. ポータル中央の約0.4 m前に立ち、壁を背にして講義室中央へ向く。壁に垂直な西向きからスクリーン方向へ約35度振った向きです。デスクトップモードで目線を水平に合わせる。自動移動はしません。
4. OBSで一人称のゲーム画面を16:9で取り込み、会場のサブモニターへ出力する。HUD・ネームプレートはクライアント側で非表示にする。

目線は床上1.5 mです。ZIP内の `portal-camera-framing.png` はこの位置から講義室中央へ向き、縦FOV60度・1920×1080で撮った構図の参考画像です。`portal-camera-straight.png` には壁に垂直な西向きの構図も残します。クライアント側のFOVは別設定であり、アバターから固定しません。立ち位置のUnity座標と向きはZIP内の `build-info.json` に記録します。

アバター自体にはCamera、AudioSource、コライダー、Lua、HLS送信や画面出力機能を付けません。実際のレンズ・クライアントの表示・OBS・会場側の出力はそれぞれの設定です。撮影クライアントのマイクは、会場音が回り込む構成ならミュートしてください。

**Local Testで姿が変わるのは自分だけです。ほかの人には元のアバターが見えます。** 本番で周囲にも点として見せるには、編集用シーンからCCKでアバターをアップロードして着用する必要があります。作成処理はオンラインアップロードを行いません。

見た目の点には独自Colliderを付けず、通常の移動時の壁・床の判定はCVRのプレイヤーコントローラーへ任せます。すり抜け飛行やテレポートを有効にする処理はありません。人型リグの妥当性はUnity/CCKで検証しますが、実機の壁との接触・目線・移動・構図はWindowsのChilloutVRで最終確認してください。
