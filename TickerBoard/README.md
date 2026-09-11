# TickerBoard

Visual Studio 用の WPF 株価ティッカーボードの初期実装です。現在はモック価格を1分ごとに更新します。

## 実行

Visual Studio で `TickerBoard.slnx` を開き、`TickerBoard` をスタートアッププロジェクトにして実行します。

## 設定

初回起動時、`%LOCALAPPDATA%\\TickerBoard\\settings.json` が作成されます。`symbols` に銘柄を追加・削除してから再起動してください。

## 次の実装候補

- 実データAPIプロバイダー
- 設定画面での銘柄編集
- システムトレイ常駐とホットキー
- クリック透過モード
