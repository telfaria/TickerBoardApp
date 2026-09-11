# TickerBoard（日本語）

WPF（.NET 10）で動作する株価ティッカーボードです。画面上部に常時表示し、銘柄情報を横スクロール表示します。

## 主な機能

- Yahoo Finance ベースの株価取得（APIキー不要）
- 設定ウィンドウから銘柄コードの追加・編集・削除
- 保存時の銘柄名自動解決（JP 優先）
- スクロール速度、フォント、バー高さ、透明度、常時最前面の設定
- マルチディスプレイ / 高 DPI での表示位置調整

## 実行方法

1. Visual Studio で `TickerBoard.slnx` を開く
2. `TickerBoard` をスタートアッププロジェクトに設定
3. 実行（F5）

## 設定ファイル

初回起動時に以下が作成されます。

- `%LOCALAPPDATA%\\TickerBoard\\settings.json`

主な設定項目例:

- `symbols`
- `refreshIntervalSeconds`
- `height`
- `opacityPercent`
- `alwaysOnTop`
- `fontFamily`
- `fontSize`
- `scrollSpeed`
- `initialOffsetPercent`

## 操作メモ

- 歯車アイコン: 設定メニュー
- ダブルクリック: 設定ウィンドウ表示

## 補足

- 表示や初期位置の調整は `settings.json` でも変更できます。
- 実行環境や表示解像度により見え方が変わる場合があります。
