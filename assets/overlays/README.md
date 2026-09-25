# overlays

VPS またはネットワークの障害時に OBS で使うローカル代替画面と、表示確認用のテストカードを格納します。1 画面 1 ディレクトリとし、直接開ける `index.html` を用意してください。

`make serve` の実行後、`http://localhost:8080/<directory>/` で確認できます。

本番で URL から読み込むブラウザソースと表示用 API は `sedec-server` で実装・配信します。このディレクトリから GitHub の raw URL を本番参照先として使わないでください。
