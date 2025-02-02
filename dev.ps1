$csproj = ".\HikariEditor\HikariEditor.csproj"
$appName = "HikariEditor"
$publisher = "ひかり"
$execFile = "HikariEditor.exe"
$version = (Get-Date).ToString("yy.M.d")
$date = (Get-Date).ToString("yyyyMMdd")
$publishDir = "HikariEditor\publish"
$muiIcon = "HikariEditor\Assets\App.ico"

$startMenuPath = [Environment]::GetFolderPath("Programs")

if ($appName -eq "") { return; }
$appPath = "$env:localappdata\$appName"
$startMenuPath = "$startMenuPath\${appName}"

$arg = $Args[0]

function CreateShortcut($link, $target) {
    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut($link)
    $Shortcut.TargetPath = $target
    $Shortcut.Save()
}

function Run() {
    dotnet run --project $csproj
}

function Publish() {
    $null = dotnet publish $csproj -c Release -p:Version=$version
}

function Zip() {
    Publish
    Compress-Archive -Path $publishDir -DestinationPath $appName-$version.zip
}

function Install() {
    Write-Output "アプリをビルドしています..."
    Publish

    # アプリをコピー
    if (Test-Path $appPath) {
        Write-Output "既存のアプリを削除しています: ($appPath)"
        $null = Remove-Item -Recurse -Path $appPath
    }
    Write-Output "アプリをインストールしています: ($appPath)"
    Copy-Item -Path $publishDir -Recurse $appPath

    # ショートカットの作成
    if (-not (Test-Path $startMenuPath)) {
        Write-Output "ショートカットを作成しています (1/2): ($startMenuPath)"
        $null = New-Item -Path $startMenuPath -ItemType Directory
    }
    Write-Output "ショートカットを作成しています (2/2): (${startMenuPath}\${appName}.lnk -> $appPath\$execFile)"
    CreateShortcut "${startMenuPath}\${appName}.lnk" "$appPath\$execFile"
}

function Uninstall() {
    # ショートカットの削除
    if (Test-Path $startMenuPath) {
        Write-Output "ショートカットを削除しています: ($startMenuPath)"
        Remove-Item -Recurse -Path $startMenuPath
    }

    # アプリを削除
    if (Test-Path $appPath) {
        Write-Output "アプリを削除しています: ($appPath)"
        Remove-Item -Path $appPath -Recurse
    }
}

function Pack() {
    Publish
    $size = [Math]::Round((Get-ChildItem $publishDir -Force -Recurse -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum / 1KB, 0, [MidpointRounding]::AwayFromZero)
    .'C:\Program Files (x86)\NSIS\makensis.exe' /DVERSION="$version" /DDATE="$date" /DSIZE="$size" /DMUI_ICON="$muiIcon" /DMUI_UNICON="$muiIcon" /DPUBLISH_DIR="$publishDir" /DPRODUCT_NAME="$appName" /DEXEC_FILE="$execFile" /DPUBLISHER="$publisher" installer.nsh
}


if ($arg -eq "run") {
    Run
}
elseif ($arg -eq "publish") {
    Publish
}
elseif ($arg -eq "zip") {
    Zip
}
elseif ($arg -eq "install") {
    Install
}
elseif ($arg -eq "uninstall") {
    Uninstall
}
elseif ($arg -eq "pack") {
    Pack
}