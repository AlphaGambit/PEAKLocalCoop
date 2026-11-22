using HarmonyLib;

namespace PeakLocalCoop;

internal static class DirectCoopLaunch
{
    public static void Install(Harmony harmony)
    {
        var block = new HarmonyMethod(typeof(DirectCoopLaunch), nameof(BlockStockEntry));
        foreach (var method in new[] { "PlayClicked", "JoinClicked", "StartMultiplayerLobby",
            "ShowPlayOfflineModal", "ContinueFromQuicksaveClicked" })
            harmony.Patch(BuildGuard.Method(typeof(MainMenuMainPage), method), prefix: block);

        harmony.Patch(BuildGuard.Method(typeof(MainMenu), "StartSoloSession"),
            prefix: new HarmonyMethod(typeof(DirectCoopLaunch), nameof(AllowLocalRoom)));
    }

    private static bool BlockStockEntry() => false;
    private static bool AllowLocalRoom() => PartyRuntime.Current?.Requested == true;
}
