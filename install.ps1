#Requires -Version 5.1
<#
.SYNOPSIS
    Installs Notepad Pro for the current user.

.DESCRIPTION
    Copies the published NotepadPro.exe into %LOCALAPPDATA%\Programs\NotepadPro,
    creates a Start Menu shortcut, and registers the application so it appears in
    the Windows "Open with" menu. No administrator rights are required.

.PARAMETER Source
    Path to NotepadPro.exe. Defaults to NotepadPro.exe next to this script, or
    .\dist\NotepadPro.exe when running from a source checkout.

.PARAMETER DesktopShortcut
    Also create a shortcut on the desktop.

.PARAMETER NoOpenWith
    Skip the per-user "Open with" registration.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\install.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\install.ps1 -DesktopShortcut
#>
[CmdletBinding()]
param(
    [string]$Source,
    [switch]$DesktopShortcut,
    [switch]$NoOpenWith
)

$ErrorActionPreference = 'Stop'

$AppName = 'Notepad Pro'
$ExeName = 'NotepadPro.exe'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\NotepadPro'
$TargetExe = Join-Path $InstallDir $ExeName
$StartMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$StartMenuLink = Join-Path $StartMenuDir "$AppName.lnk"
$DesktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) "$AppName.lnk"

if ([string]::IsNullOrWhiteSpace($Source)) {
    $beside = Join-Path $PSScriptRoot $ExeName
    $Source = if (Test-Path -LiteralPath $beside -PathType Leaf) {
        $beside
    } else {
        Join-Path $PSScriptRoot "dist\$ExeName"
    }
}

$sourcePath = [System.IO.Path]::GetFullPath($Source)
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "NotepadPro.exe was not found at '$sourcePath'. Publish first (dotnet publish src\NotepadPro\NotepadPro.csproj -c Release -o dist) or pass -Source."
}

if ((Get-Item -LiteralPath $sourcePath).Length -lt 1MB) {
    throw "'$sourcePath' does not look like the published single-file executable."
}

Write-Host "Installing $AppName..." -ForegroundColor Cyan
Write-Host "  From: $sourcePath"
Write-Host "  To:   $TargetExe"

$running = Get-Process -Name 'NotepadPro' -ErrorAction SilentlyContinue
if ($running) {
    throw "Notepad Pro is running. Close all Notepad Pro windows and run the installer again."
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -LiteralPath $sourcePath -Destination $TargetExe -Force

function New-Shortcut {
    param([string]$Path, [string]$Target, [string]$WorkingDirectory, [string]$Description)
    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($Path)
        $shortcut.TargetPath = $Target
        $shortcut.WorkingDirectory = $WorkingDirectory
        $shortcut.IconLocation = "$Target,0"
        $shortcut.Description = $Description
        $shortcut.Save()
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
    }
}

New-Item -ItemType Directory -Path $StartMenuDir -Force | Out-Null
New-Shortcut -Path $StartMenuLink -Target $TargetExe -WorkingDirectory $InstallDir `
    -Description 'A fast, native text and code editor.'
Write-Host "  Start Menu shortcut created."

if ($DesktopShortcut) {
    New-Shortcut -Path $DesktopLink -Target $TargetExe -WorkingDirectory $InstallDir `
        -Description 'A fast, native text and code editor.'
    Write-Host "  Desktop shortcut created."
}

$appPaths = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\NotepadPro.exe'
New-Item -Path $appPaths -Force | Out-Null
Set-ItemProperty -Path $appPaths -Name '(default)' -Value $TargetExe
Set-ItemProperty -Path $appPaths -Name 'Path' -Value $InstallDir

if (-not $NoOpenWith) {
    $appKey = 'HKCU:\Software\Classes\Applications\NotepadPro.exe'
    New-Item -Path "$appKey\shell\open\command" -Force | Out-Null
    Set-ItemProperty -Path $appKey -Name 'FriendlyAppName' -Value $AppName
    Set-ItemProperty -Path $appKey -Name '(default)' -Value $AppName
    Set-ItemProperty -Path "$appKey\shell\open\command" -Name '(default)' -Value "`"$TargetExe`" `"%1`""
    New-Item -Path "$appKey\DefaultIcon" -Force | Out-Null
    Set-ItemProperty -Path "$appKey\DefaultIcon" -Name '(default)' -Value "$TargetExe,0"

    $supported = @(
        '.txt', '.md', '.markdown', '.log', '.ini', '.cfg', '.conf',
        '.cs', '.c', '.h', '.cpp', '.hpp', '.cc', '.java',
        '.js', '.mjs', '.cjs', '.jsx', '.ts', '.tsx', '.py',
        '.html', '.htm', '.css', '.scss', '.json', '.xml', '.sql', '.php',
        '.go', '.rs', '.sh', '.bash', '.ps1', '.psm1', '.yaml', '.yml',
        '.csv', '.bat', '.cmd', '.toml', '.env', '.gitignore'
    )
    New-Item -Path "$appKey\SupportedTypes" -Force | Out-Null
    foreach ($extension in $supported) {
        Set-ItemProperty -Path "$appKey\SupportedTypes" -Name $extension -Value ''
    }
    Write-Host "  'Open with' registration created."
}

Write-Host ''
Write-Host "Notepad Pro is installed." -ForegroundColor Green
Write-Host "  Launch it from the Start Menu, or run:"
Write-Host "    $TargetExe"
Write-Host ''
Write-Host "To remove it, run: powershell -ExecutionPolicy Bypass -File .\uninstall.ps1"
