# ひかりエディタ

<img width="2892" height="1534" alt="image" src="https://github.com/user-attachments/assets/bfba27b3-3132-47ba-83a4-bc8d47ca6a3c" />

Windows 向けのモダンなテキスト／コードエディタです。WinUI 3 と Monaco Editor を組み合わせ、VSCode ライクな編集体験を提供します。AI エージェント・Git 連携・本物の擬似コンソール（ConPTY）を内蔵しています。

## 機能

- **マルチタブ編集** — TabView で複数ファイルを同時に開いて編集できます
- **Monaco Editor** — VSCode と同じエンジンによるシンタックスハイライトとコード補完
- **ファイルエクスプローラー** — TreeView でディレクトリ構造を表示し、ファイルの作成・削除・リネーム・切り替えが可能
- **AI エージェント** — サイドパネルで LLM と対話。`Read` / `Write` / `Edit` ツールを使って作業ディレクトリのファイルを直接編集できるエージェント型アシスタント
  - 対応 API: OpenAI Chat Completions / Responses、Anthropic Messages、Azure OpenAI
  - 複数のモデル設定を登録し、ステータスバーから切り替え。応答はストリーミング表示
- **Git 連携** — git リポジトリを開くとステータスバーに現在のブランチを表示。クリックでブランチを切り替え
- **多彩なプレビュー** — 拡張子に応じて開き方を自動判別
  - 画像（PNG / JPEG / GIF / BMP / ICO / WebP / AVIF）・動画（MP4 / WebM / OGG / MOV など）・SVG・PDF を WebView で表示
  - バイナリファイルは 16 進ビューで表示
- **ターミナル** — Windows 擬似コンソール（ConPTY）でシェルをホストし、複数のターミナルタブを管理。ssh・vim・python などの対話プロセスも正しく動作
- **LaTeX サポート** — `.tex` ファイルをコンパイルし、生成された PDF をインライン プレビュー表示
- **自動保存** — タイトルバーのトグルで有効／無効を切り替えられます
- **改行コード切り替え** — ステータスバーから LF／CRLF をワンクリックで切り替え
- **リサイズ可能なパネル** — エクスプローラー・ターミナル・AI パネルの幅／高さをドラッグで調整
- **Mica バックドロップ** — Windows 11 のモダンなアクリル素材 UI

## 動作環境

| 項目 | 要件 |
|------|------|
| OS | Windows 10 22H2 (build 22621) 以上 |
| アーキテクチャ | x64 |
| ランタイム | 不要（Self-Contained） |
| Git | ブランチ表示・切り替え機能を使う場合（PATH に `git` が必要） |
| texlive | LaTeX 機能を使う場合（PATH に `ptex2pdf.exe` が必要） |

## キーボードショートカット

| キー | 操作 |
|------|------|
| `Ctrl+O` | ディレクトリを開く |
| `Ctrl+S` | ファイルを保存 |
| `Ctrl+C` | コピー |
| `Ctrl+V` | ペースト |
| `Ctrl+T` | ターミナルを開く |
| `Ctrl+U` | ログを開く |
| `Ctrl+I` | AI エージェントパネルの表示／非表示 |

## AI エージェントの設定

ステータスバーのモデルボタンからモデル設定を開き、利用する LLM を登録します。

| 項目 | 説明 |
|------|------|
| API 種別 | Chat Completions / Responses / Messages / Azure OpenAI |
| エンドポイント | `/chat/completions` を除いたベース URL（例: `http://localhost/v1`） |
| モデル | モデル名（例: `gpt-4o-mini`、`claude-opus-4-8`） |
| API キー | 認証キー。Azure OpenAI など `api-key` ヘッダー認証にも対応 |

> API キーを含むモデル設定は `%LOCALAPPDATA%\HikariEditor\ai-models.json` に保存されます。
> その他の設定（開いているディレクトリ・自動保存・パネルサイズなど）は `%TEMP%\HikariEditor-settings.json` に保存されます。

## 技術スタック

| 層 | 技術 |
|----|------|
| フレームワーク | WinUI 3 (Windows App SDK 1.7) |
| 言語 | C# (.NET 9.0) |
| エディタエンジン | Monaco Editor 0.38.0 |
| ターミナル | ConPTY（Windows 擬似コンソール）+ WebView2 表示 |
| AI SDK | Anthropic 12.29.0 / OpenAI 2.11.0 |
| プレビュー描画 | WebView2 (Chromium) |
| 画像処理 | System.Drawing.Common 9.0.0 |
| 圧縮ライブラリ | SharpCompress 0.49.1 |

## ビルド

PowerShell 7 以上で `dev.ps1` を使います。

```powershell
.\dev.ps1 build      # Debug ビルド（コンパイルエラーの確認）
.\dev.ps1 run        # 開発モードで起動
.\dev.ps1 publish    # リリースビルド（publish/ に出力）
.\dev.ps1 zip        # リリースビルド + Zip アーカイブ作成
.\dev.ps1 install    # ビルド + ローカルインストール + スタートメニュー登録
.\dev.ps1 pack       # NSIS インストーラー作成（要 NSIS）
.\dev.ps1 -h         # ヘルプ表示
```

## ライセンス

[LICENSE](LICENSE) を参照してください。
