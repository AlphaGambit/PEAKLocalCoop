# PEAK Local Co-op installation and play guide

**English** · [Bahasa Indonesia](TUTORIAL.id.md) · [Project README](../README.md)

## Install on Windows

1. Check that PEAK is version **3.1.0**, Steam build **26542162**. In your Steam library, open **PEAK → Properties → Updates** to check the Build ID.
2. Download **PEAKLocalCoop-0.1.13.zip** from **Assets** on the [v0.1.13 release page](https://github.com/AlphaGambit/PEAKLocalCoop/releases/tag/v0.1.13).
3. Select **Extract All** and put the files somewhere outside your Steam library, such as Documents.
4. Close PEAK and run **Install or Repair Local Co-op.cmd** from the extracted folder. The first install needs an internet connection to retrieve BepInEx.
5. Wait for **PEAK Local Co-op 0.1.13 siap**, connect your controllers, and leave Steam running.

## Start a climb

1. Run **Play Local Co-op DX11.cmd** to go straight to the lobby. If you open PEAK from Steam, select **Local Co-op** below **Play Offline** in the main menu.
2. Player 1 uses the keyboard and mouse. Each guest presses **Start/Menu** on their own controller, then checks that their seat shows a check mark.
3. Player 1 selects **Masuk Airport** (Enter Airport). Use the kiosk and boarding pass in the airport to start the climb.
4. Each player has a separate character, view, stamina bar, and inventory. Two players are stacked vertically. With three players, P1 is on top and P2 and P3 share the bottom row. Four players use a 2×2 grid.

## Controller controls

| Action | Control |
| --- | --- |
| Switch hotbar slots | D-pad left/right |
| Empty your hands or reselect the last slot | D-pad down |
| Use the backpack wheel | D-pad to select, bottom face button to confirm, right face button to close |
| Extend or reel in a held Rope Spool | Hold RB/R1 to extend or LB/L1 to reel in |
| Place a Rope Spool or checkpoint flag | Aim at a suitable surface and hold RT/R2 |
| Play an emote | Hold D-pad up, choose with the right stick, then release D-pad up |
| Ping | Press the right stick (R3). P1 can use middle mouse click. |
| Spectate after passing out or dying | D-pad left/right to switch scouts. RB/R1 and LB/L1 adjust camera distance. |

## Join during a run or reconnect

- Light a campfire that advances the climb and wait for the checkpoint prompt. While the party stays in the checkpoint area, press **Start/Menu** on an unused controller to fill an open seat.
- Reconnect an existing controller at any time. To replace a single disconnected controller, press **Start/Menu** on the replacement. If several players disconnected, P1 selects the seat first using **F6** for P2, **F7** for P3, or **F8** for P4. The replacement controller then presses **Start/Menu**.

## Troubleshooting and uninstall

- If Steam removed mod files after updating or verifying the game, run **Play Local Co-op DX11.cmd** to restore them, or run **Install or Repair Local Co-op.cmd** to repair without starting PEAK.
- If the launcher shows **File PEAK berubah** (PEAK files changed), check the [Releases page](https://github.com/AlphaGambit/PEAKLocalCoop/releases) for a version that supports your game build.
- When reporting a problem, include your PEAK build, steps to reproduce, and relevant messages from `BepInEx/LogOutput.log`.
- To uninstall, close PEAK and remove `BepInEx/plugins/PeakLocalCoop/` from the game folder. Running the mod launcher again will restore the DLL. You can delete the extracted ZIP folder and `%LOCALAPPDATA%\PEAKLocalCoop` to remove the recovery copy.
