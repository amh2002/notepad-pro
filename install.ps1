#Requires -Version 5.1
<#
.SYNOPSIS
    Installs Notepad Pro for the current user.

.DESCRIPTION
    Copies the published NotepadPro.exe, documentation (README.md, LICENSE, CHANGELOG.md),
    and uninstall.ps1 into %LOCALAPPDATA%\Programs\NotepadPro, creates a Start Menu shortcut,
    adds the installation directory to the user's PATH environment variable, registers the
    uninstaller with Windows (Settings > Installed apps), and registers the application so it
    appears in the Windows "Open with" menu. No administrator rights are required.

.PARAMETER Source
    Path to NotepadPro.exe. Defaults to NotepadPro.exe next to this script, or
    .\dist\NotepadPro.exe when running from a source checkout.

.PARAMETER DesktopShortcut
    Also create a shortcut on the desktop.

.PARAMETER NoOpenWith
    Skip the per-user "Open with" registration.

.PARAMETER NoPath
    Skip adding the install directory to the user's PATH environment variable.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\install.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\install.ps1 -DesktopShortcut
#>
[CmdletBinding()]
param(
    [string]$Source,
    [switch]$DesktopShortcut,
    [switch]$NoOpenWith,
    [switch]$NoPath
)

$ErrorActionPreference = 'Stop'

$AppName = 'Notepad Pro'
$ExeName = 'NotepadPro.exe'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\NotepadPro'
$TargetExe = Join-Path $InstallDir $ExeName
$TargetUninstall = Join-Path $InstallDir 'uninstall.ps1'
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

$bundledFiles = @('uninstall.ps1', 'README.md', 'LICENSE', 'CHANGELOG.md')
foreach ($fileName in $bundledFiles) {
    $srcFile = Join-Path $PSScriptRoot $fileName
    if (Test-Path -LiteralPath $srcFile -PathType Leaf) {
        $destFile = Join-Path $InstallDir $fileName
        Copy-Item -LiteralPath $srcFile -Destination $destFile -Force
        Write-Host "  Copied $fileName."
    }
}

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

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NotepadPro'
New-Item -Path $uninstallKey -Force | Out-Null

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($TargetExe)
$appVersion = if ($versionInfo.ProductVersion) { $versionInfo.ProductVersion.Trim() } else { '1.0.2' }
$publisher = if ($versionInfo.CompanyName) { $versionInfo.CompanyName.Trim() } else { 'Notepad Pro contributors' }
$estimatedSize = [int]((Get-Item -LiteralPath $TargetExe).Length / 1KB)

Set-ItemProperty -Path $uninstallKey -Name 'DisplayName' -Value $AppName
Set-ItemProperty -Path $uninstallKey -Name 'DisplayVersion' -Value $appVersion
Set-ItemProperty -Path $uninstallKey -Name 'Publisher' -Value $publisher
Set-ItemProperty -Path $uninstallKey -Name 'DisplayIcon' -Value "$TargetExe,0"
Set-ItemProperty -Path $uninstallKey -Name 'InstallLocation' -Value $InstallDir
Set-ItemProperty -Path $uninstallKey -Name 'UninstallString' -Value "powershell.exe -ExecutionPolicy Bypass -File `"$TargetUninstall`""
Set-ItemProperty -Path $uninstallKey -Name 'QuietUninstallString' -Value "powershell.exe -ExecutionPolicy Bypass -File `"$TargetUninstall`""
Set-ItemProperty -Path $uninstallKey -Name 'EstimatedSize' -Value $estimatedSize -Type DWord
Set-ItemProperty -Path $uninstallKey -Name 'NoModify' -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name 'NoRepair' -Value 1 -Type DWord
Write-Host "  Windows uninstaller registered."

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

if (-not $NoPath) {
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $normalizedInstallDir = $InstallDir.TrimEnd('\', '/')
    $entries = if ($userPath) {
        $userPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    } else {
        @()
    }

    $alreadyInPath = $entries | Where-Object { $_.Trim().TrimEnd('\', '/') -ieq $normalizedInstallDir }
    if (-not $alreadyInPath) {
        $newPath = (@($entries) + $InstallDir) -join ';'
        [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
        Write-Host "  Added to user PATH."
    } else {
        Write-Host "  Already in user PATH."
    }

    # Update PATH in current PowerShell session
    $sessionEntries = if ($env:PATH) {
        $env:PATH -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    } else {
        @()
    }
    if (-not ($sessionEntries | Where-Object { $_.Trim().TrimEnd('\', '/') -ieq $normalizedInstallDir })) {
        $env:PATH = (@($sessionEntries) + $InstallDir) -join ';'
    }
}

Write-Host ''
Write-Host "Notepad Pro is installed." -ForegroundColor Green
Write-Host "  Launch it from the Start Menu, or run:"
Write-Host "    $TargetExe"
if (-not $NoPath) {
    Write-Host "    $ExeName"
}
Write-Host ''
Write-Host "To remove it, uninstall from Windows Settings or run:"
Write-Host "  powershell -ExecutionPolicy Bypass -File `"$TargetUninstall`""
