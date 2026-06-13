#Requires -Version 7.0

<#
.SYNOPSIS
    HikariEditor の開発用スクリプト。
.DESCRIPTION
    ビルド、実行、インストール、パッケージング等の開発タスクを実行する。
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('run', 'publish', 'zip', 'install', 'uninstall', 'pack')]
    [string]$Command,

    [Alias('h')]
    [switch]$Help,

    [Alias('V')]
    [switch]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

# ====================
# Global Variables
# ====================

$script:ScriptName = Split-Path -Leaf $PSCommandPath
$script:Verbose    = $VerbosePreference -ne 'SilentlyContinue'

$script:Csproj      = '.\HikariEditor\HikariEditor.csproj'
$script:AppName     = 'HikariEditor'
$script:Publisher   = 'ひかり'
$script:ExecFile    = 'HikariEditor.exe'
$script:AppVersion  = (Get-Date).ToString('yy.M.d')
$script:Date        = (Get-Date).ToString('yyyyMMdd')
$script:PublishDir  = 'HikariEditor\publish'
$script:MuiIcon     = 'HikariEditor\Assets\App.ico'
$script:AppPath     = "$env:LOCALAPPDATA\$script:AppName"
$script:StartMenu   = "$([Environment]::GetFolderPath('Programs'))\$script:AppName"

# ====================
# Color codes
# ====================

if (-not [Console]::IsOutputRedirected -and -not $env:NO_COLOR) {
    $script:Red     = "`e[0;31m"
    $script:Yellow  = "`e[0;33m"
    $script:Green   = "`e[1;32m"
    $script:Cyan    = "`e[0;36m"
    $script:CyanDim = "`e[36m"
    $script:Gray    = "`e[0;90m"
    $script:Nc      = "`e[0m"
} else {
    $script:Red = $script:Yellow = $script:Green = ''
    $script:Cyan = $script:CyanDim = $script:Gray = $script:Nc = ''
}

# ====================
# Logging
# ====================

function Write-LogError {
    param([Parameter(Mandatory)][string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    [Console]::Error.WriteLine("$timestamp $script:Red[ERROR]$script:Nc $Message")
}

function Write-LogWarn {
    param([Parameter(Mandatory)][string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    [Console]::Error.WriteLine("$timestamp $script:Yellow[WARN]$script:Nc  $Message")
}

function Write-LogInfo {
    param([Parameter(Mandatory)][string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    [Console]::Out.WriteLine("$timestamp $script:Cyan[INFO]$script:Nc  $Message")
}

function Write-LogDebug {
    param([Parameter(Mandatory)][string]$Message)
    if (-not $script:Verbose) { return }
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    [Console]::Out.WriteLine("$timestamp $script:Gray[DEBUG]$script:Nc $Message")
}

# ====================
# Help / Version
# ====================

function Show-Help {
    Write-Output 'HikariEditor Dev Script'
    Write-Output ''
    Write-Output "$script:Green`Usage:$script:Nc $script:ScriptName [OPTIONS] <COMMAND>"
    Write-Output ''
    Write-Output "${script:Green}Options:$script:Nc"
    Write-Output "  $script:Cyan-h$script:Nc, $script:Cyan--help$script:Nc                    Show this help message"
    Write-Output "  $script:Cyan-V$script:Nc, $script:Cyan--version$script:Nc                 Show version"
    Write-Output "  $script:Cyan--verbose$script:Nc                    Enable verbose output"
    Write-Output ''
    Write-Output "${script:Green}Commands:$script:Nc"
    Write-Output "  ${script:Cyan}run${script:Nc}                        Run the app in development mode"
    Write-Output "  ${script:Cyan}publish${script:Nc}                    Build a Release publish"
    Write-Output "  ${script:Cyan}zip${script:Nc}                        Publish and create a zip archive"
    Write-Output "  ${script:Cyan}install${script:Nc}                    Build and install to local app directory"
    Write-Output "  ${script:Cyan}uninstall${script:Nc}                  Remove installed app and shortcuts"
    Write-Output "  ${script:Cyan}pack${script:Nc}                       Publish and create an NSIS installer"
}

function Show-Version {
    Write-Output "HikariEditor Dev Script"
}

# ====================
# Commands
# ====================

function Invoke-Run {
    Write-LogInfo 'Starting HikariEditor...'
    dotnet run --project $script:Csproj
}

function Invoke-Publish {
    Write-LogInfo "Publishing version $script:AppVersion..."
    $null = dotnet publish $script:Csproj -c Release -p:Version=$script:AppVersion
    Write-LogInfo 'Publish complete.'
}

function Invoke-Zip {
    Invoke-Publish
    $zipName = "$script:AppName-$script:AppVersion.zip"
    Write-LogInfo "Creating archive: $zipName"
    Compress-Archive -Path $script:PublishDir -DestinationPath $zipName
    Write-LogInfo "Archive created: $zipName"
}

function New-Shortcut {
    param(
        [Parameter(Mandatory)][string]$Link,
        [Parameter(Mandatory)][string]$Target
    )
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Link)
    $shortcut.TargetPath = $Target
    $shortcut.Save()
}

function Invoke-Install {
    Invoke-Publish

    if (Test-Path $script:AppPath) {
        Write-LogInfo "既存のインストールを削除しています: $script:AppPath"
        $null = Remove-Item -Recurse -Path $script:AppPath
    }
    Write-LogInfo "アプリをインストールしています: $script:AppPath"
    Copy-Item -Path $script:PublishDir -Recurse $script:AppPath

    if (-not (Test-Path $script:StartMenu)) {
        $null = New-Item -Path $script:StartMenu -ItemType Directory
    }
    $lnk = "$script:StartMenu\$script:AppName.lnk"
    $exe = "$script:AppPath\$script:ExecFile"
    Write-LogInfo "ショートカットを作成しています: $lnk"
    New-Shortcut -Link $lnk -Target $exe

    Write-LogInfo 'インストール完了。'
}

function Invoke-Uninstall {
    if (Test-Path $script:StartMenu) {
        Write-LogInfo "ショートカットを削除しています: $script:StartMenu"
        Remove-Item -Recurse -Path $script:StartMenu
    }

    if (Test-Path $script:AppPath) {
        Write-LogInfo "アプリを削除しています: $script:AppPath"
        Remove-Item -Path $script:AppPath -Recurse
    }

    Write-LogInfo 'アンインストール完了。'
}

function Invoke-Pack {
    Invoke-Publish
    $size = [Math]::Round(
        (Get-ChildItem $script:PublishDir -Force -Recurse -ErrorAction SilentlyContinue |
            Measure-Object Length -Sum).Sum / 1KB,
        0,
        [MidpointRounding]::AwayFromZero
    )
    Write-LogInfo "NSIS インストーラーを作成しています (size=${size}KB)..."
    & 'C:\Program Files (x86)\NSIS\makensis.exe' `
        /DVERSION="$script:AppVersion" `
        /DDATE="$script:Date" `
        /DSIZE="$size" `
        /DMUI_ICON="$script:MuiIcon" `
        /DMUI_UNICON="$script:MuiIcon" `
        /DPUBLISH_DIR="$script:PublishDir" `
        /DPRODUCT_NAME="$script:AppName" `
        /DEXEC_FILE="$script:ExecFile" `
        /DPUBLISHER="$script:Publisher" `
        installer.nsh
    Write-LogInfo 'インストーラー作成完了。'
}

# ====================
# Main
# ====================

trap {
    Write-LogError "Script failed: $_"
    exit 1
}

try {
    if ($Help) {
        Show-Help
        exit 0
    }

    if ($Version) {
        Show-Version
        exit 0
    }

    if ([string]::IsNullOrEmpty($Command)) {
        Write-LogError 'コマンドが指定されていません。'
        Show-Help
        exit 2
    }

    Write-LogDebug "Command=$Command, Verbose=$script:Verbose"

    switch ($Command) {
        'run'       { Invoke-Run }
        'publish'   { Invoke-Publish }
        'zip'       { Invoke-Zip }
        'install'   { Invoke-Install }
        'uninstall' { Invoke-Uninstall }
        'pack'      { Invoke-Pack }
    }
} finally {
    # クリーンアップ処理（必要であれば追加）
}
