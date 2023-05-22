# ひかりエディタ

## 証明書のインストール方法
1. `.cer` ファイルを開き、「証明書のインストール」をクリック
2. 「ローカルコンピューター」を選択し次へ 
3. 「証明書をすべて次のストアに配置する」を選択し、「参照」
4. 「信頼されたルート証明機関」を選択し OK

## 依存パッケージ
- amd64: https://aka.ms/windowsappsdk/1.2/1.2.221109.1/windowsappruntimeinstall-x64.exe
- x86: https://aka.ms/windowsappsdk/1.2/1.2.221109.1/windowsappruntimeinstall-x86.exe

## インストール方法
```ps1
Add-AppPackage .\Microsoft.WindowsAppRuntime.1.2.msix
Add-AppPackage .\hikarieditor_1.0.0.0_x64.msix
```
