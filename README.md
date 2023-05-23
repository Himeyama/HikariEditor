# ひかりエディタ

![image](https://github.com/Himeyama/HikariEditor/assets/39254183/b4804759-b2ea-43e5-ad1b-e37805be721a)

## 証明書のインストール方法
1. `.cer` ファイルを開き、「証明書のインストール」をクリック
2. 「ローカルコンピューター」を選択し次へ 
3. 「証明書をすべて次のストアに配置する」を選択し、「参照」
4. 「信頼されたルート証明機関」を選択し OK

## 依存パッケージ
- amd64: https://aka.ms/windowsappsdk/1.2/1.2.221109.1/windowsappruntimeinstall-x64.exe
- x86: https://aka.ms/windowsappsdk/1.2/1.2.221109.1/windowsappruntimeinstall-x86.exe

## WebView2 のインストール
- https://developer.microsoft.com/ja-jp/microsoft-edge/webview2/consumer/

## インストール方法
```ps1
Add-AppPackage .\Microsoft.WindowsAppRuntime.1.2.msix
Add-AppPackage .\hikarieditor_1.0.0.0_x64.msix
```
