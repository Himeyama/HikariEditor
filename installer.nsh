; ============================================================
; HikariEditor NSIS インストーラースクリプト
;
; dev.ps1 の `pack` コマンドから makensis.exe 経由で呼び出される。
; 以下の値は /D オプションで外部から注入される（installer.nsh 単体では未定義）:
;   VERSION      アプリのバージョン（例: 26.6.14）
;   DATE         ビルド日（例: 20260614）
;   SIZE         インストールサイズ（KB 単位、Add/Remove Programs 表示用）
;   MUI_ICON     インストーラーアイコン（MUI2 が自動参照）
;   MUI_UNICON   アンインストーラーアイコン（MUI2 が自動参照）
;   PUBLISH_DIR  dotnet publish の出力フォルダ
;   PRODUCT_NAME 製品名（HikariEditor）
;   EXEC_FILE    実行ファイル名（例: HikariEditor.exe）
;   PUBLISHER    発行者名
;
; 注意: 日本語を含むため、このファイルは UTF-8 (BOM 付き) で保存すること。
;       BOM が無いと makensis が ACP として読み「Bad text encoding」で失敗する。
; ============================================================

Unicode true

!include "MUI2.nsh"

; ------------------------------------------------------------
; 注入値が無い場合のフォールバック（makensis を直接叩いた時用）
; ------------------------------------------------------------
!ifndef PRODUCT_NAME
  !define PRODUCT_NAME "HikariEditor"
!endif
!ifndef VERSION
  !define VERSION "0.0.0"
!endif
!ifndef DATE
  !define DATE "00000000"
!endif
!ifndef SIZE
  !define SIZE "0"
!endif
!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "HikariEditor\publish"
!endif
!ifndef EXEC_FILE
  !define EXEC_FILE "${PRODUCT_NAME}.exe"
!endif
!ifndef PUBLISHER
  !define PUBLISHER "ひかり"
!endif
; ライセンス表示に使うファイル。Unicode 版 NSIS は日本語を正しく表示するため
; BOM 付き UTF-8 を要求するので、dev.ps1 が BOM 付きの一時コピーを渡す。
!ifndef LICENSE_FILE
  !define LICENSE_FILE "LICENSE"
!endif

; ------------------------------------------------------------
; 基本設定
; ------------------------------------------------------------
; 管理者権限を要求しない（ユーザー領域にインストールするため）
RequestExecutionLevel user

; アンインストール情報を書き込むレジストリキー
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"

Name "${PRODUCT_NAME} ${VERSION}"
BrandingText "${PRODUCT_NAME} ${VERSION} (${DATE})"
OutFile "${PRODUCT_NAME}-${VERSION}-setup.exe"

; dev.ps1 の install と揃え、ユーザーの LocalAppData 配下へ配置する
InstallDir "$LOCALAPPDATA\${PRODUCT_NAME}"
; 既存インストールがあればそのパスを引き継ぐ
InstallDirRegKey HKCU "${UNINST_KEY}" "InstallLocation"

ShowInstDetails show
ShowUnInstDetails show

; ------------------------------------------------------------
; Modern UI ページ
; ------------------------------------------------------------
!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${LICENSE_FILE}"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

; インストール完了後に起動するオプション
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXEC_FILE}"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Japanese"
!insertmacro MUI_LANGUAGE "English"

; ------------------------------------------------------------
; インストールセクション
; ------------------------------------------------------------
Section "Install"
  SetOutPath "$INSTDIR"

  ; publish の中身を丸ごと展開
  File /r "${PUBLISH_DIR}\*.*"

  ; スタートメニューにショートカットを作成
  CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
  CreateShortcut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\${EXEC_FILE}"

  ; アンインストーラーを生成
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Add/Remove Programs（プログラムと機能）への登録
  WriteRegStr   HKCU "${UNINST_KEY}" "DisplayName"     "${PRODUCT_NAME}"
  WriteRegStr   HKCU "${UNINST_KEY}" "DisplayVersion"  "${VERSION}"
  WriteRegStr   HKCU "${UNINST_KEY}" "Publisher"       "${PUBLISHER}"
  WriteRegStr   HKCU "${UNINST_KEY}" "DisplayIcon"     "$INSTDIR\${EXEC_FILE}"
  WriteRegStr   HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKCU "${UNINST_KEY}" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr   HKCU "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify"        1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair"        1
  ; EstimatedSize は KB 単位（SIZE は dev.ps1 が KB で算出済み）
  WriteRegDWORD HKCU "${UNINST_KEY}" "EstimatedSize"   ${SIZE}
SectionEnd

; ------------------------------------------------------------
; アンインストールセクション
; ------------------------------------------------------------
Section "Uninstall"
  ; ショートカット削除
  Delete "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk"
  RMDir  "$SMPROGRAMS\${PRODUCT_NAME}"

  ; インストールフォルダを丸ごと削除
  RMDir /r "$INSTDIR"

  ; レジストリ登録を削除
  DeleteRegKey HKCU "${UNINST_KEY}"
SectionEnd
