using System;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace PeakLocalCoop;

internal sealed class CheckpointGate
{
    private Campfire candidate;
    private bool admitted;
    private bool closed;

    public void OnCampfireLit(Campfire campfire)
    {
        if (candidate == campfire) return;
        candidate = campfire;
        admitted = false;
        closed = false;
    }

    public Campfire Available()
    {
        if (!candidate || closed || !GameHandler.IsOnIslandAndInitialized
            || LoadingScreenHandler.loading || RunSettings.isMiniRun) return null;
        var segment = MapHandler.CurrentSegmentNumber;
        if (segment == Segment.Peak || segment == Segment.Void ||
            (segment == Segment.TheKiln && LavaRising.LavaActive(Segment.TheKiln)))
            return null;

        var inCamp = candidate.isActiveAndEnabled && candidate.state == Campfire.FireState.Lit
            && candidate.EveryoneInRange();
        if (admitted && !inCamp) closed = true;
        if (!inCamp || !PhotonNetwork.IsMessageQueueRunning || MapHandler.PreviousCampfire != candidate)
            return null;

        var host = Character.localCharacter;
        if (!host || host.gameOver || host.warping || host.data.dead || host.data.fullyPassedOut)
            return null;

        admitted = true;
        return candidate;
    }

    public static Vector3 FindSpawn(Campfire campfire, int seat)
    {
        const float radius = 0.43f;
        var camp = campfire.transform.position;
        var occupied = PlayerHandler.GetAllPlayerCharacters().Where(character => character && !character.data.dead)
            .Select(character => character.Center).ToArray();
        var terrain = HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainMap);
        var physical = HelperFunctions.GetMask(HelperFunctions.LayerType.AllPhysical);
        for (var i = 0; i < 12; i++)
        {
            var direction = Quaternion.Euler(0f, (i + seat * 3) * 30f, 0f) * Vector3.forward;
            var origin = camp + direction * (i < 6 ? 2.2f : 3.2f) + Vector3.up * 2f;
            if (!Physics.Raycast(origin, Vector3.down, out var floor, 4f, terrain, QueryTriggerInteraction.Ignore)
                || floor.normal.y < 0.88f || Mathf.Abs(floor.point.y - camp.y) > 1.5f
                || occupied.Any(position => (position - floor.point).sqrMagnitude < 2.25f))
                continue;
            var bottom = floor.point + Vector3.up * (radius + 0.12f);
            if (!Physics.CheckCapsule(bottom, bottom + Vector3.up * 1.05f, radius, physical, QueryTriggerInteraction.Ignore))
                return floor.point + Vector3.up * 0.2f;
        }
        throw new InvalidOperationException("Tidak ada titik spawn yang cukup rata dan kosong di sekitar campfire.");
    }

    public static void Install(Harmony harmony)
    {
        harmony.Patch(BuildGuard.Method(typeof(Campfire), "Light_Rpc", typeof(bool), typeof(float)),
            postfix: new HarmonyMethod(BuildGuard.MethodByName(typeof(CheckpointGate), nameof(AfterLight))));
    }

    private static void AfterLight(Campfire __instance, bool updateSegment)
    {
        if (updateSegment && PartyRuntime.Current?.IsLocalRoom == true)
            PartyRuntime.Current.Checkpoint.OnCampfireLit(__instance);
    }
}
