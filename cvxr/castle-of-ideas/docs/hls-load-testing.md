# HLS同時受信の負荷試験

運用する配信サービスの許可を得てから、その環境に必要な人数・配信ビットレートで確認してください。運用環境の測定結果や回線・契約情報はGit管理外の `.private/` に記録します。

## ffmpegによる独立受信

Python 3.11以上とPATH上のffmpegを使います。受信URLはGit管理外の `cvxr/castle-of-ideas/streaming.local.json` の `receiverUrl` / `portalReceiverUrl` から読みます。

```sh
.venv/bin/python cvxr/castle-of-ideas/tools/ffmpeg-hls-load-test.py \
  --program-clients 9 --portal-clients 8 \
  --seconds 180 --warmup-seconds 20
```

各視聴者が講演とポータルを受信する場合は、それぞれに1本ずつ必要です。上の例では講演だけを見る視聴者1人を加え、独立したffmpegを計17個起動します。

`-c copy`でMPEG-TSへ再多重化し、出力はメモリ上で読み捨てます。最初の20秒を除いたメディア量、映像時刻の進み方、受信エラーを集計します。HTTP/TLSを含む総通信量、デコード、ChilloutVRでの表示、多拠点・長時間の安定性は別途確認してください。映像時刻の進み方は配信元からの絶対遅延を表しません。

既定では3分で自動終了します。Ctrl+C、または開始時に表示するPIDへのSIGINT/SIGTERMで、今回起動したffmpegだけを停止します。最大25プロセスです。`--seconds` は5秒〜2時間、`--max-output-gb` は0.001〜50 GBで明示指定できます。既定の再多重化出力上限は3 GBです。長いリハーサルでは時間だけでなく出力量の上限も指定してください。映像は保存せず読み捨てますが、その分の受信通信は発生します。

集計と実行中のPIDはGit管理外の `cvxr/castle-of-ideas/build/ffmpeg-hls-load-test/` に保存します。URL、生ログ、映像は保存しません。結果は運用環境に依存するため、再配布物には含めません。

## 実参加者ぶんを除いて補うリハーサル

Git管理外の計画JSONを `--rehearsal-plan` で指定すると、本番目標と実接続数の差だけを受信します。

- `target` と `actual` の両方に `dualFeedClients` と `programOnlyClients` を指定します。
- `dualFeedClients` は講演とポータルを両方受信するCVRクライアントの数です。参加者だけでなく、接続中のオーナーや会場カメラも1台として含めます。
- `programOnlyClients` は講演だけを受信する端末の数です。
- 講演は両者の合計、ポータルは `dualFeedClients` です。目標から実接続数を配信ごとに引き、足りる配信には追加しません。
- 同じ配信を両室のスクリーンで表示しても、1クライアントあたり講演の取得は1本です。

```sh
# 受信数の計算だけ。ffmpeg・実URL・通信は不要です。
.venv/bin/python cvxr/castle-of-ideas/tools/ffmpeg-hls-load-test.py \
  --rehearsal-plan .private/hls-rehearsal/plan.json --dry-run

# 実接続数を確認してから開始。上限時間は1時間、出力量は20 GBです。
.venv/bin/python cvxr/castle-of-ideas/tools/ffmpeg-hls-load-test.py \
  --rehearsal-plan .private/hls-rehearsal/plan.json \
  --seconds 3600 --max-output-gb 20 \
  --report-dir .private/hls-rehearsal/current
```

実接続数は手動申告で、開始時に読みます。CVRへの入退室を自動検出しません。人数が変わったらCtrl+Cで止め、JSONを直してから起動し直してください。停止後の集計は同じ `current/summary.json` に上書きするので、残したい結果は次回起動前に退避します。同じ出力先に `run.pid` が残っている間は二重起動を拒否します。異常終了の残骸を消す場合も、先の実行が止まっていることを確認してください。

10秒ごとに稼働数、メディア転送量、診断行数、映像時刻が15秒以上進んでいない受信機数を表示します。受信機が途中終了すると全体を止めて集計します。これは配信サービスへの通信負荷を補うもので、CVR内のアバター・ボイス・描画・同期の人数を増やすものではありません。

## HTTP取得だけで測る場合

標準ライブラリだけでプレイリストとセグメントを取得するツールもあります。

```sh
.venv/bin/python cvxr/castle-of-ideas/tools/hls-load-test.py \
  --single-clients 5 --hold-seconds 60
```

こちらもCtrl+C/SIGTERMで停止し、Git管理外の `build/hls-load-test/` に集計を保存します。メディアを再生する試験とは測定方法が異なります。
