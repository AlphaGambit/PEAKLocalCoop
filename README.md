# PEAK Local Co-op

**English** · [Bahasa Indonesia](README.id.md)

An experimental split-screen mod for **2–4 players on one Windows PC**. Each player has a character, camera, HUD, and inventory in one shared PEAK world. Player 1 uses a keyboard and mouse. Players 2–4 each use a controller.

**Supported build for v0.1.13** · PEAK **3.1.0**, Steam build **26542162**.

- [Download the installer ZIP](https://github.com/AlphaGambit/PEAKLocalCoop/releases/download/v0.1.13/PEAKLocalCoop-0.1.13.zip)
- [Installation guide and controller controls](docs/TUTORIAL.md)
- [Panduan instalasi dan kontrol dalam bahasa Indonesia](docs/TUTORIAL.id.md)

## Quick start

1. Extract the release ZIP to a folder outside your Steam library and close PEAK.
2. Run `Install or Repair Local Co-op.cmd`. The installer downloads the pinned BepInEx pack on first use.
3. Keep Steam open, connect your controllers, and run `Play Local Co-op DX11.cmd`.
4. Press **Start/Menu** on each guest controller. Player 1 selects **Masuk Airport** (Enter Airport), then uses the in-game kiosk and boarding pass to start the climb.

You can also launch PEAK from Steam and select **Local Co-op** in the main menu. For an existing mod manager profile, install BepInExPack PEAK in that profile and place `release/PeakLocalCoop.dll` under the profile's `BepInEx/plugins/PeakLocalCoop/`. Launch through the mod manager. [Pinned BepInExPack PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/v/5.4.75301/)

## Compatibility and recovery

- This release supports local play. An active session shares campfire progress and biome transitions. Resuming a co-op run after closing the game is currently unsupported.
- The DX11 launcher uses the game's existing graphics settings. Positional audio uses Player 1's listener, and Player 1 operates shared boarding and end-of-run menus.
- The installer stores a verified recovery copy under `%LOCALAPPDATA%\PEAKLocalCoop`. The DX11 launcher restores missing files before checking the PEAK build. If the build check fails after a Steam update, use a mod release compatible with the new build.
- To disable the feature, set `Enabled = false` in `BepInEx/config/local.peak.coop.cfg`. See the [installation guide](docs/TUTORIAL.md#troubleshooting-and-uninstall) for full removal.

## Build from source

Use a Steam installation of the supported PEAK build, .NET SDK 8 (tested with 8.0.422), and the BepInEx 5.4.23.3 core files with Harmony from the pinned pack. The project resolves PEAK and Unity assemblies from the local installation.

Run the following in PowerShell and adjust the installation path as needed.

```powershell
$peakDir = 'C:\Program Files (x86)\Steam\steamapps\common\PEAK'
$bepCore = Join-Path $peakDir 'BepInEx\core'
dotnet build .\PeakLocalCoop.csproj -c Release "-p:PeakDir=$peakDir" "-p:BepInExCore=$bepCore"
```

The build produces `bin/Release/netstandard2.1/PeakLocalCoop.dll`. For troubleshooting, check `BepInEx/LogOutput.log`. The [Indonesian README](README.id.md#diagnostik) lists the automated diagnostic launch arguments. Further notes are available in Indonesian. [Build audit](docs/AUDIT.md) · [Integration](docs/INTEGRATION.md) · [Verification](docs/VERIFICATION.md) · [Benchmarks](docs/PERFORMANCE.md)
