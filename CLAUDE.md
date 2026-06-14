# CLAUDE.md — HikariEditor 開発ガイド

## プロジェクト概要

WinUI 3 (Windows App SDK) と Monaco Editor を使った Windows 向けテキスト／コードエディタ。  
言語: C# / .NET 9.0、UI: XAML、エディタコア: Monaco Editor (WebView2 経由)。

## ディレクトリ構成

```
HikariEditor/
├── HikariEditor/              # メインプロジェクト
│   ├── Editor/                # マルチタブエディタ（Monaco Editor + WebView2）
│   ├── Explorer/              # ファイルエクスプローラー（TreeView）+ FileItem.cs
│   ├── Terminal/              # ターミナル／ログ表示（WebView2）+ LogPage.xaml
│   ├── Preview/               # PDF プレビュー（LaTeX 出力）
│   ├── Dialogs/               # ダイアログ・ページ（About, Error, Open, Search）
│   ├── Helpers/               # ユーティリティ（Text.cs, Directories.cs）
│   ├── Assets/                # アイコン・Editor.html（Monaco ラッパー）
│   ├── MainWindow.xaml(.cs)   # メインウィンドウ・全体統合
│   ├── Settings.cs            # 設定の読み書き（JSON）
│   ├── LaTeX.cs               # ptex2pdf 呼び出し・プレビュー連携
│   └── HikariEditor.csproj
├── DESIGN/                    # 設計ドキュメント
│   └── powershell.md          # PowerShell スクリプト設計ガイドライン
├── dev.ps1                    # 開発用 PowerShell スクリプト
├── installer.nsh              # NSIS インストーラースクリプト（dev.ps1 pack が使用）
└── HikariEditor.sln
```

## 開発コマンド（dev.ps1）

PowerShell 7 以上が必要。

```powershell
# Debug ビルド（コンパイルエラーの確認用）
.\dev.ps1 build

# 開発モードで起動（dotnet run）
.\dev.ps1 run

# リリースビルド（publish/ に出力）
.\dev.ps1 publish

# リリースビルド + Zip アーカイブ作成
.\dev.ps1 zip

# ビルド + ローカルインストール（%LOCALAPPDATA%\HikariEditor）+ スタートメニュー登録
.\dev.ps1 install

# アンインストール（インストール先とショートカットを削除）
.\dev.ps1 uninstall

# NSIS インストーラー作成（NSIS が別途必要）
.\dev.ps1 pack

# ヘルプ表示
.\dev.ps1 -h

# 詳細ログ付き実行
.\dev.ps1 run --verbose
```

## ビルド要件

| 項目 | バージョン |
|------|-----------|
| .NET SDK | 9.0 以上 |
| Windows App SDK | 1.7（csproj で NuGet 管理） |
| PowerShell | 7.0 以上（dev.ps1 実行時） |
| NSIS | `pack` コマンド使用時のみ必要 |
| texlive | LaTeX 機能使用時（ptex2pdf.exe が PATH に必要） |

## アーキテクチャのポイント

### エディタと Monaco の通信

`Editor.xaml.cs` は TCP リスナー（127.0.0.1:8086）を起動し、Monaco Editor（WebView2）からの保存コマンドを受け取る。パラメータは Base64 エンコード済み。最大バッファ 64MB。

### 設定の永続化

`Settings.cs` が `%TEMP%\HikariEditor-settings.json` に JSON で保存。保存項目: `ExplorerDir`、`AutoSave`、`OpenDirPath`。

### LaTeX フロー

1. エクスプローラーで `.tex` ファイルを選択
2. `LaTeX.cs` が `ptex2pdf.exe` を呼び出し
3. 成功時は `Preview/PDF.xaml` で PDF を表示、失敗時はログにエラーを表示

### 改行コード

`Explorer/FileItem.cs` でファイル読み書き時に LF/CRLF を変換。現在のモードはステータスバーで切り替え可能。

## コーディング規約

- コメントは「なぜ」を書く（「何を」はコードが語る）
- 不要な抽象化は避け、現在の要件に必要なコードだけを書く
- エラーハンドリングはシステム境界（ファイル I/O、プロセス起動）にのみ行う
- PowerShell スクリプトの規約は `DESIGN/powershell.md` を参照

## 主要ファイルの役割早見表

| ファイル | 役割 |
|---------|------|
| `MainWindow.xaml.cs` | ウィンドウ統合、Monaco 自動セットアップ、メニュー、ショートカット |
| `Editor/Editor.xaml.cs` | TabView 管理、WebView2 連携、TCP サーバー、自動保存 |
| `Explorer/Explorer.xaml.cs` | TreeView 表示、ファイル選択、新規作成・削除 |
| `Terminal/Terminal.xaml.cs` | ターミナルタブ管理 |
| `Preview/PDF.xaml.cs` | PDF プレビュー表示 |
| `LaTeX.cs` | ptex2pdf 実行・結果処理 |
| `Settings.cs` | 設定 JSON の読み書き |
| `Explorer/FileItem.cs` | ファイル作成・保存・.tgz 展開 |
| `Dialogs/Error.xaml.cs` | エラーダイアログ（静的ヘルパー） |
| `Dialogs/Open.xaml.cs` | ディレクトリ選択ページ |
| `Helpers/Text.cs` | Base64 エンコードユーティリティ |
| `Helpers/Directories.cs` | ディレクトリ情報モデル |
| `Terminal/LogPage.xaml.cs` | ログ表示・追記ヘルパー |
| `Assets/Editor.html` | Monaco Editor の HTML ラッパー |
