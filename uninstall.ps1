#Requires -Version 5.1
<#
.SYNOPSIS
    Removes the per-user Notepad Pro installation.

.DESCRIPTION
    Deletes the installed executable, Start Menu and desktop shortcuts, and the
    per-user registration entries. Settings, session, and recovery data are kept
    unless -PurgeData is specified.

.PARAMETER PurgeData
    Also delete %APPDATA%\NotepadPro (settings, session, recent files) and
    %LOCALAPPDATA%\NotepadPro (recovery snapshots, error log).

.PARAMETER Force
    Do not ask for confirmation when -PurgeData is used.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\uninstall.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 -PurgeData
#>
[CmdletBinding()]
param(
    [switch]$PurgeData,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$AppName = 'Notepad Pro'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\NotepadPro'
$StartMenuLink = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$AppName.lnk"
$DesktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) "$AppName.lnk"
$ConfigDir = Join-Path $env:APPDATA 'NotepadPro'
$LocalDataDir = Join-Path $env:LOCALAPPDATA 'NotepadPro'

if ($PurgeData -and -not $Force) {
    $answer = Read-Host "This deletes settings, session, and recovery data. Type YES to continue"
    if ($answer -ne 'YES') {
        Write-Host 'Cancelled.'
        exit 1
    }
}

$running = Get-Process -Name 'NotepadPro' -ErrorAction SilentlyContinue
if ($running) {
    throw 'Notepad Pro is running. Close all Notepad Pro windows and run the uninstaller again.'
}

Write-Host "Removing $AppName..." -ForegroundColor Cyan

foreach ($link in @($StartMenuLink, $DesktopLink)) {
    if (Test-Path -LiteralPath $link) {
        Remove-Item -LiteralPath $link -Force
        Write-Host "  Removed $link"
    }
}

foreach ($key in @(
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\NotepadPro.exe',
    'HKCU:\Software\Classes\Applications\NotepadPro.exe'
)) {
    if (Test-Path -Path $key) {
        Remove-Item -Path $key -Recurse -Force
        Write-Host "  Removed registry key $key"
    }
}

if (Test-Path -LiteralPath $InstallDir) {
    Remove-Item -LiteralPath $InstallDir -Recurse -Force
    Write-Host "  Removed $InstallDir"
}

if ($PurgeData) {
    foreach ($directory in @($ConfigDir, $LocalDataDir)) {
        if (Test-Path -LiteralPath $directory) {
            Remove-Item -LiteralPath $directory -Recurse -Force
            Write-Host "  Removed $directory"
        }
    }
}
else {
    Write-Host "  Settings and recovery data kept in:"
    Write-Host "    $ConfigDir"
    Write-Host "    $LocalDataDir"
}

Write-Host ''
Write-Host 'Notepad Pro has been removed.' -ForegroundColor Green
