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

3分で自動終了します。Ctrl+C、または開始時に表示するPIDへのSIGINT/SIGTERMで、今回起動したffmpegだけを停止します。最大25プロセス・600秒・再多重化出力3 GBの上限があります。

集計と実行中のPIDはGit管理外の `cvxr/castle-of-ideas/build/ffmpeg-hls-load-test/` に保存します。URL、生ログ、映像は保存しません。結果は運用環境に依存するため、再配布物には含めません。

## HTTP取得だけで測る場合

標準ライブラリだけでプレイリストとセグメントを取得するツールもあります。

```sh
.venv/bin/python cvxr/castle-of-ideas/tools/hls-load-test.py \
  --single-clients 5 --hold-seconds 60
```

こちらもCtrl+C/SIGTERMで停止し、Git管理外の `build/hls-load-test/` に集計を保存します。メディアを再生する試験とは測定方法が異なります。
