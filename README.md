# SEDEC Livestreaming

成人向け二次元同人作品制作勉強会「性DEC」の配信・録画に使う資産を、再利用可能な形で管理するリポジトリです。

OBS Studio のシーンと設定、画像・音声・映像、ローカル代替画面、ChilloutVR 用アセット、会場 3D モデル、制作を補助するスクリプトを対象にします。VPS で動く公式 Web、配信基盤、OBS が URL で参照するブラウザソースと表示用 API は、隣接する `Klavistr/sedec-server` が担当します。イベント全体のトンマナと運用手順は、`Klavistr/sedec-d11n` が正本です。

## 現在地

現在は、制作物を追加し始められるリポジトリ基盤と、最初の ChilloutVR ワールドの編集可能なブロックアウトまで整備済みです。OBS シーンコレクションと本番用メディアはまだ入っていません。

ChilloutVR ワールド **Castle of Ideas** は [`cvxr/castle-of-ideas/`](cvxr/castle-of-ideas/) で制作しています。暗い講義室、白い自習室、接続廊下、家具、配信スクリーンの素体を Blender スクリプトから再生成できます。

最初のマイルストーンは、次の最小配信パックを作ることです。

1. OBS の解像度・音声・録画設定を決める
2. 待機、登壇、休憩、終了のシーンを用意する
3. ChilloutVR ワールドを CCK Local Test まで通す
4. `sedec-server` のブラウザソースを OBS とワールド内スクリーンに登録する
5. リハーサル録画を行い、チェックリストを `sedec-d11n` に反映する

## リポジトリ構成

```text
.
├── assets/
│   ├── audio/             # BGM、効果音など
│   ├── images/            # ロゴ、背景、静止画など
│   ├── overlays/          # ローカル代替画面と表示確認用テストカード
│   └── video/             # スティンガー、ループ映像など
├── cvxr/                  # ChilloutVR 用アセットと導入メモ
├── docs/                  # このリポジトリ固有の設計・制作メモ
├── models/                # 会場・ワールド用 3D ソース
├── obs/
│   ├── profiles/          # 共有可能な OBS プロファイル
│   └── scene-collections/ # OBS シーンコレクション JSON
├── scripts/               # 検証・書き出しなどの補助スクリプト
└── tests/                 # 補助スクリプトのテスト
```

各ディレクトリの README に、格納対象と注意事項を記載しています。生成物や個人環境のキャッシュはコミットしません。

## 必要要件

リポジトリの初期化と検証に必要なのは次の 2 つだけです。

- Python 3.11 以上
- GNU Make（任意。使わない場合のコマンドも下記に記載）

制作対象に応じて OBS Studio、Blender、ChilloutVR + CCK/Unity を別途使います。バージョンは最初の実アセットを追加するときに固定します。Node.js は現時点では不要です。

## セットアップ

```bash
git clone https://github.com/Klavistr/sedec-live.git
cd sedec-live
make setup
make check
make test
```

`make setup` は `.venv` に Python 仮想環境を作るだけで、外部パッケージやネットワーク接続を必要としません。

Make を使わない場合は次の通りです。

```bash
python3 -m venv .venv
.venv/bin/python scripts/check_repo.py
.venv/bin/python -m unittest discover -s tests -v
```

## ローカル代替画面のプレビュー

サンプルのテストカードを含むローカル表示物は、ローカル HTTP サーバーで確認できます。

```bash
make serve
```

ブラウザーで <http://localhost:8080/test-card/> を開きます。OBS ではブラウザソースの URL に同じアドレスを指定します。既定のキャンバスは 1920 × 1080 です。

本番配信時に URL で参照するブラウザソースは `sedec-server` から配信します。このディレクトリには、VPS またはネットワークの障害時にも表示できるローカル代替画面と、表示確認用のテスト素材だけを置きます。

## 基本ルール

- ファイル名は原則として英小文字の `kebab-case` にする
- OBS から参照するパスは、可能な限りリポジトリ内の相対パスにする
- Web 配信するブラウザソースは `sedec-server` の安定した公開 URL を参照し、GitHub の raw URL やリポジトリ間のファイルパスを参照しない
- 編集可能なソースと書き出し物を区別し、書き出し手順を同じ場所に記録する
- API キー、配信キー、個人情報をコミットしない
- 大きなバイナリを追加する前に Git LFS の導入方針を決める（現在は未導入）
- 配信運用の手順やチェックリストは `sedec-d11n`、配信端末で使う OBS 設定とメディアはこのリポジトリ、VPS で動くサービスは `sedec-server` に置く

詳しい資産の扱いは [`docs/asset-guidelines.md`](docs/asset-guidelines.md)、次の作業候補は [`docs/work-plan.md`](docs/work-plan.md) を参照してください。Codex はルートの [`AGENTS.md`](AGENTS.md) に従います。

## 検証

```bash
make check  # 必須ディレクトリ、JSON、HTML エントリーポイント、大容量ファイルを確認
make test   # 検証スクリプトのユニットテスト
```

`check` は問題があれば終了コード 1、注意事項だけなら終了コード 0 を返します。

## ライセンスと配布

ライセンス、第三者素材の扱い、完成物の配布先は未決定です。決定するまでは、このリポジトリの内容を外部配布可能とはみなさないでください。素材ごとの出典と利用条件は、同じディレクトリの README またはメタデータに記録します。
