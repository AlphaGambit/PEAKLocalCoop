using System;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace PeakLocalCoop;

[BepInPlugin(Id, "PEAK Local Co-op", "0.1.13")]
[BepInProcess("PEAK.exe")]
[DefaultExecutionOrder(550)]
public sealed class Plugin : BaseUnityPlugin
{
    private const string Id = "local.peak.coop";
    private Harmony harmony;
    private OfflineScouts scouts;
    private PartyRuntime runtime;
    private LocalCoopUi ui;

    private void Awake()
    {
        var args = Environment.GetCommandLineArgs();
        var directLaunch = args.Contains("-peak-coop-dx11");
        if (directLaunch && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
        {
            Logger.LogError("Launcher Local Co-op memerlukan Direct3D 11.");
            Application.Quit();
            enabled = false;
            return;
        }
        if (!Config.Bind("Local Co-op", "Enabled", true, "Aktifkan Local Co-op pada build yang sudah diaudit.").Value)
        {
            if (directLaunch) Application.Quit();
            enabled = false;
            return;
        }
        try
        {
            BuildGuard.Verify();
            scouts = new OfflineScouts();
            SeatHooks.Scouts = scouts;
            harmony = new Harmony(Id);
            SeatHooks.Install(harmony);
            SeatInteraction.Install(harmony);
            ItemSeat.Install(harmony);
            CheckpointGate.Install(harmony);
            PartyRuntime.Install(harmony);
            GuestInventory.Install(harmony);
            GuestHud.Install(harmony);
            GuestReticle.Install(harmony);
            SeatPing.Install(harmony);
            PingPresentation.Install(harmony);
            MirrorPresentation.Install(harmony);
            SeatGhosts.Install(harmony);
            HazardHits.Install(harmony);
            SharedWorldHooks.Install(harmony);
            AreaEffects.Install(harmony);
            runtime = new PartyRuntime(Logger, scouts);
            if (directLaunch) DirectCoopLaunch.Install(harmony);
            ui = new LocalCoopUi(runtime, Logger, directLaunch);
            Logger.LogInfo("Build 26542162 cocok. Adapter offline siap.");
            var probe = new RuntimeProbe(Logger, runtime, scouts, ui);
            if (directLaunch)
            {
                if (args.Contains("-peak-coop-fast-check")) StartCoroutine(probe.DirectLaunchCheck());
            }
            else if (args.Contains("-peak-coop-probe"))
                StartCoroutine(probe.Proof());
            else if (args.Contains("-peak-coop-menu-check"))
                StartCoroutine(probe.Slice(menuOnly: true));
            else if (args.Contains("-peak-coop-slice") || args.Contains("-peak-coop-item-check") || args.Contains("-peak-coop-feed-check") || args.Contains("-peak-coop-logic-check") || args.Contains("-peak-coop-world-check") || args.Contains("-peak-coop-mirror-flag-check"))
                StartCoroutine(probe.Slice());
            else if (args.Any(arg => arg == "-peak-coop-bench" || arg == "-peak-coop-perf" || arg == "-peak-coop-perf-api"))
                StartCoroutine(probe.Benchmark());
        }
        catch (Exception exception)
        {
            Logger.LogError($"Local Co-op dinonaktifkan. {exception}");
            PingPresentation.Uninstall();
            MirrorPresentation.Uninstall();
            SeatGhosts.Uninstall();
            try { harmony?.UnpatchSelf(); }
            catch (Exception rollbackError) { Logger.LogError($"Pelepasan patch gagal. {rollbackError}"); }
            if (directLaunch) Application.Quit();
            enabled = false;
        }
    }

    private void Update()
    {
        runtime?.Tick();
        ui?.Update();
    }

    private void OnGUI() => ui?.Draw();
    private void LateUpdate() => runtime?.LateTick();

    private void OnDestroy()
    {
        PingPresentation.Uninstall();
        MirrorPresentation.Uninstall();
        SeatGhosts.Uninstall();
        ui?.Dispose();
        runtime?.Dispose();
        SeatHooks.Clear();
        scouts?.Clear();
        SeatHooks.Scouts = null;
        harmony?.UnpatchSelf();
    }
}
