# KidsK-ify

Android（キッズケータイを含む）の画面をWindows PCに表示し、マウス・文字入力・ADB管理操作で操作する.NET 8 / WinFormsアプリです。

## 必要環境

- Windows 10/11
- .NET 8 SDK（ビルド時）
- Android SDK Platform-Toolsの`adb.exe`
- Android側でADBデバッグが有効で、PCの接続が許可されていること

`adb.exe`はPATHに登録するか、`KidsK-ify.exe`と同じフォルダーに置いてください。

## ビルド

```cmd
dotnet build KidsK-ify\KidsK-ify.csproj -c Release
```

Visual Studio 2022では`KidsK-ify.sln`を開いてビルドできます。

## 画面操作

- 上部の標準WinFormsツールバーで通常／高速モードを切り替えます。
- 高速モードのキャプチャ枚数は**4〜10枚**から選択できます。枚数が多いほどキャプチャの並列バッファが増えます。
- プレビューのクリックはタップ、ドラッグはスワイプ、左ボタンの長押しは長押し、右クリックは1秒の長押しです。
- 下部から文字入力、HOME、BACKを送信できます。
- メニューの「デバイス → 現在の表示画面を保存」で、端末画面をPNGとしてWindowsへダウンロード（保存）できます。

## ADB管理ツール

メニューの「デバイス → ADB管理ツール」から次の操作を利用できます。

- **ADB / Shellコマンド**: `adb`引数または`adb shell`コマンドの実行
- **getprop**: Androidのシステムプロパティ一覧の取得
- **ADB File Manager**: `ls`、`cd`、`mkdir`、`rm`、`mv`、ファイルのpull（受信）／push（送信）
- **Package Manager**: `pm list packages`、システムパッケージ一覧、APKの`install`、`install -r`、`uninstall`、`enable`、`disable-user`

ファイル送受信では、リモートパスを入力してから「受信 (pull)」または「送信 (push)」を押します。APKのインストールはPackage ManagerタブのボタンからAPKファイルを選択します。

## 注意事項

- Androidの`screencap`が動作する端末を前提とします。
- 初回解像度取得後に端末の表示サイズを変更した場合は、いったん再接続してください。
- `adb shell input text`はAndroidのIME実装により対応文字が異なります。日本語が入力できない場合は端末側IMEの制約をご確認ください。
- この環境ではWindows向け.NETビルドは実行していません。Windowsの.NET 8 SDKでビルドしてください。
