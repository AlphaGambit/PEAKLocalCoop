[CmdletBinding()]
param(
    [string]$PeakDir,
    [string]$LoaderArchive,
    [switch]$Setup,
    [switch]$LaunchDx11
)

$ErrorActionPreference = 'Stop'
$release = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'release-info.json') -Raw | ConvertFrom-Json
$steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -Name SteamPath -ErrorAction SilentlyContinue).SteamPath
if (!$steam) { $steam = Join-Path ${env:ProgramFiles(x86)} 'Steam' }
$steam = $steam.Replace('/', '\')
$steamExe = Join-Path $steam 'steam.exe'

function Find-Peak {
    if ($PeakDir) { return (Resolve-Path -LiteralPath $PeakDir).ProviderPath }
    $libraries = @($steam)
    $list = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $list -PathType Leaf) {
        $libraries += [regex]::Matches((Get-Content -LiteralPath $list -Raw), '(?m)^\s*"path"\s+"([^"]+)"') |
            ForEach-Object { $_.Groups[1].Value.Replace('\\', '\') }
    }
    foreach ($library in ($libraries | Select-Object -Unique)) {
        $common = Join-Path $library 'steamapps\common'
        $manifest = Join-Path $library 'steamapps\appmanifest_3527290.acf'
        if (!(Test-Path -LiteralPath $manifest -PathType Leaf)) { continue }
        $match = [regex]::Match((Get-Content -LiteralPath $manifest -Raw), '"installdir"\s+"([^"]+)"')
        if (!$match.Success) { continue }
        $game = Join-Path $common $match.Groups[1].Value
        if (Test-Path -LiteralPath (Join-Path $game 'PEAK.exe') -PathType Leaf) { return $game }
    }
    throw 'Instalasi Steam PEAK tidak ditemukan. Berikan folder PEAK sebagai argumen.'
}

function Test-Hash([string]$Path, [string]$Expected) {
    return (Test-Path -LiteralPath $Path -PathType Leaf) -and
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Expected
}

function Save-File([string]$Source, [string]$Destination, [string]$Expected, [switch]$Replace) {
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $Destination))
    $temp = $Destination + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::Copy($Source, $temp)
        if (!(Test-Hash $temp $Expected)) { throw "Checksum gagal untuk $Source." }
        if ($Replace -and [IO.File]::Exists($Destination)) { [IO.File]::Replace($temp, $Destination, [NullString]::Value) }
        else { [IO.File]::Move($temp, $Destination) }
    }
    finally {
        if ([IO.File]::Exists($temp)) { [IO.File]::Delete($temp) }
    }
}

function Import-Loader([string]$Archive) {
    if (!(Test-Hash $Archive $release.loaderArchiveSha256)) {
        throw "Arsip loader berbeda dari BepInExPack PEAK $($release.loaderVersion)."
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        foreach ($file in $release.loaderFiles.PSObject.Properties) {
            $cached = Join-Path $loaderCache $file.Name
            if (Test-Hash $cached $file.Value) { continue }
            $entry = $zip.GetEntry("BepInExPack_PEAK/$($file.Name)")
            if (!$entry) { throw "Arsip loader tidak memiliki $($file.Name)." }
            $temp = Join-Path $env:TEMP ([guid]::NewGuid().ToString('N'))
            try {
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $temp)
                Save-File $temp $cached $file.Value -Replace
            }
            finally { [IO.File]::Delete($temp) }
        }
    }
    finally { $zip.Dispose() }
}

if (Get-Process -Name PEAK -ErrorAction SilentlyContinue) { throw 'Tutup PEAK sebelum memasang atau memulihkan mod.' }
$game = Find-Peak
if (!(Test-Path -LiteralPath (Join-Path $game 'PEAK.exe') -PathType Leaf)) {
    throw "PEAK.exe tidak ditemukan di $game."
}

$cache = Join-Path $env:LOCALAPPDATA 'PEAKLocalCoop'
$loaderCache = Join-Path $cache "loader\$($release.loaderVersion)"
$pluginCache = Join-Path $cache "plugins\$($release.pluginSha256).dll"
$packaged = Join-Path $PSScriptRoot 'release\PeakLocalCoop.dll'
if (!(Test-Hash $pluginCache $release.pluginSha256)) {
    Save-File $packaged $pluginCache $release.pluginSha256 -Replace
}
if ($LoaderArchive) { Import-Loader ((Resolve-Path -LiteralPath $LoaderArchive).ProviderPath) }
elseif ($Setup -and @($release.loaderFiles.PSObject.Properties | Where-Object {
        !(Test-Hash (Join-Path $loaderCache $_.Name) $_.Value)
    }).Count -gt 0) {
    $temp = Join-Path $env:TEMP ([guid]::NewGuid().ToString('N') + '.zip')
    try {
        Write-Host "Mengunduh BepInExPack PEAK $($release.loaderVersion) dari Thunderstore."
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $release.loaderUrl -UseBasicParsing -OutFile $temp
        Import-Loader $temp
    }
    finally { [IO.File]::Delete($temp) }
}

$configPaths = @('doorstop_config.ini', 'BepInEx/config/BepInEx.cfg')
foreach ($file in $release.loaderFiles.PSObject.Properties) {
    $installed = Join-Path $game $file.Name
    $cached = Join-Path $loaderCache $file.Name
    if (Test-Hash $installed $file.Value) {
        if (!(Test-Hash $cached $file.Value)) { Save-File $installed $cached $file.Value -Replace }
        continue
    }
    if (Test-Path -LiteralPath $installed) {
        if ($configPaths -contains $file.Name) { continue }
        throw "Loader yang sudah terpasang berbeda pada $($file.Name). Gunakan profil mod manager atau pasang ulang loader yang dipin."
    }
    if (!(Test-Hash $cached $file.Value) -and $file.Name -ne 'BepInEx/config/BepInEx.cfg') {
        throw "Salinan loader untuk $($file.Name) belum tersedia. Jalankan Install or Repair Local Co-op.cmd."
    }
}
foreach ($file in $release.loaderFiles.PSObject.Properties) {
    $installed = Join-Path $game $file.Name
    $cached = Join-Path $loaderCache $file.Name
    if (!(Test-Path -LiteralPath $installed) -and (Test-Hash $cached $file.Value)) {
        Save-File $cached $installed $file.Value
        Write-Host "Dipulihkan $($file.Name)"
    }
}

$plugin = Join-Path $game 'BepInEx\plugins\PeakLocalCoop\PeakLocalCoop.dll'
if (!(Test-Hash $plugin $release.pluginSha256)) {
    if ((Test-Path -LiteralPath $plugin) -and !$Setup) {
        throw 'DLL mod yang terpasang berbeda. Pakai launcher dari ZIP versi itu, atau jalankan penginstal untuk memilih versi ZIP ini.'
    }
    if (Test-Path -LiteralPath $plugin -PathType Leaf) {
        $oldHash = (Get-FileHash -LiteralPath $plugin -Algorithm SHA256).Hash
        $backup = Join-Path $cache "plugins\$oldHash.dll"
        if (!(Test-Hash $backup $oldHash)) { Save-File $plugin $backup $oldHash -Replace }
    }
    Save-File $pluginCache $plugin $release.pluginSha256 -Replace:$Setup
    Write-Host 'Dipulihkan PeakLocalCoop.dll'
}

foreach ($file in $release.gameFiles.PSObject.Properties) {
    $path = Join-Path (Join-Path $game 'PEAK_Data') $file.Name
    if (!(Test-Hash $path $file.Value)) {
        $message = "File PEAK berubah: $($file.Name). Mod tersimpan. Tunggu rilis mod yang mendukung build baru atau mainkan PEAK dari Steam."
        if ($LaunchDx11) { throw $message }
        Write-Warning $message
        return
    }
}

if ($LaunchDx11) {
    if (!(Test-Path -LiteralPath $steamExe -PathType Leaf)) { throw 'steam.exe tidak ditemukan.' }
    if ($PeakDir) {
        $explicitGame = $game
        $PeakDir = $null
        if ((Find-Peak) -ne $explicitGame) {
            throw 'Folder yang diberikan berbeda dari instalasi yang akan dijalankan Steam.'
        }
    }
}

Write-Host "PEAK Local Co-op $($release.version) siap. Salinan pemulihan: $cache"
if ($LaunchDx11) { Start-Process -FilePath $steamExe -ArgumentList '-applaunch 3527290 -force-d3d11 -peak-coop-dx11' }
