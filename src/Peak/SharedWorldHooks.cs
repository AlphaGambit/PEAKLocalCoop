using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Peak.Afflictions;
using UnityEngine;

namespace PeakLocalCoop;

internal static class SharedWorldHooks
{
    private static OfflineScouts ActiveScouts => SeatHooks.Scouts?.IsActive == true ? SeatHooks.Scouts : null;
    private static readonly Func<WindChillZone, float> TimeWaiting = AccessTools.MethodDelegate<Func<WindChillZone, float>>(
        AccessTools.DeclaredPropertyGetter(typeof(WindChillZone), "timeSpentWaiting")
            ?? throw new MissingMemberException(typeof(WindChillZone).FullName, "timeSpentWaiting"));
    private static readonly Action<WindChillZone, Character> ApplyWindStatus = AccessTools.MethodDelegate<Action<WindChillZone, Character>>(
        BuildGuard.Method(typeof(WindChillZone), "ApplyStatus", typeof(Character)));
    private static readonly Action<WindChillZone, Character, float> ApplyWindForce = AccessTools.MethodDelegate<Action<WindChillZone, Character, float>>(
        BuildGuard.Method(typeof(WindChillZone), "AddWindForceToCharacter", typeof(Character), typeof(float)));

    public static void Install(Harmony harmony)
    {
        Patch(typeof(FogSphere), "SetSharderVars", nameof(ApplyFog));
        Patch(typeof(Campfire), "ApplyCampfireProtection", nameof(ProtectAtCamp));
        Patch(typeof(WindChillZone), "Update", nameof(ApplyWind));
        Patch(typeof(WindChillZone), "FixedUpdate", nameof(PushWithWind));
        Patch(typeof(Character), "RPCEndGame", nameof(EndRun));
        harmony.Patch(BuildGuard.Method(typeof(Character), "TryCheckpoint"),
            transpiler: new HarmonyMethod(typeof(SharedWorldHooks), nameof(RouteCheckpointOwner)));
        var fades = new HarmonyMethod(BuildGuard.MethodByName(typeof(SharedWorldHooks), nameof(RouteFadesToHost)));
        harmony.Patch(BuildGuard.Method(typeof(Character), nameof(Character.RPCA_PassOut)), transpiler: fades);
        harmony.Patch(BuildGuard.Method(typeof(Character), "RPCA_UnPassOut"), transpiler: fades);
        void Patch(Type type, string name, string hook) => harmony.Patch(BuildGuard.Method(type, name),
            postfix: new HarmonyMethod(BuildGuard.MethodByName(typeof(SharedWorldHooks), hook)));
    }

    private static void ApplyFog(FogSphere __instance)
    {
        var scouts = ActiveScouts;
        if (scouts == null) return;
        foreach (var guest in scouts.Guests)
        {
            var scout = guest.Character;
            if (!scout || scout.data.dead) continue;
            var fog = Mathf.Approximately(__instance.ENABLE, 1f)
                && Vector3.Distance(__instance.fogPoint, scout.Center) > __instance.currentSize;
            scout.data.isInFog = fog;
            if (fog)
                scout.refs.afflictions.AddStatus(scout.data.isSkeleton ? CharacterAfflictions.STATUSTYPE.Injury : CharacterAfflictions.STATUSTYPE.Cold,
                    (scout.data.isSkeleton ? 0.0105f / 8f : 0.0105f) * Time.deltaTime);
        }
    }

    private static void ProtectAtCamp(Campfire __instance, Affliction_NoHunger ___s_CampfireBuff)
    {
        var scouts = ActiveScouts;
        if (scouts == null) return;
        foreach (var guest in scouts.Guests)
        {
            var scout = guest.Character;
            if (!scout || scout.data.dead || !scout.refs.afflictions.canGetHungry ||
                Vector3.Distance(__instance.transform.position, scout.Center) > __instance.moraleBoostRadius)
                continue;
            scout.refs.afflictions.AddAffliction(___s_CampfireBuff);
        }
    }

    private static void ApplyWind(WindChillZone __instance, ref float ___windPlayerFactor)
    {
        var scouts = ActiveScouts;
        if (scouts == null || !__instance.windActive) return;
        var bounds = __instance.windZoneBounds;
        var hostFactor = ___windPlayerFactor;
        try
        {
            foreach (var guest in scouts.Guests)
            {
                var scout = guest.Character;
                if (scout && !scout.data.dead && bounds.Contains(scout.Center))
                    ApplyWindStatus(__instance, scout);
            }
        }
        finally { ___windPlayerFactor = hostFactor; }
    }

    private static void PushWithWind(WindChillZone __instance, float ___currentForceMult)
    {
        var scouts = ActiveScouts;
        if (scouts == null || !__instance.windActive || TimeWaiting(__instance) < __instance.delayBeforeForce) return;
        var bounds = __instance.windZoneBounds;
        foreach (var guest in scouts.Guests)
        {
            var scout = guest.Character;
            if (scout && !scout.data.dead && bounds.Contains(scout.Center))
                ApplyWindForce(__instance, scout, ___currentForceMult);
        }
    }

    private static bool HostHasLocalView(Character character) => character == Character.localCharacter;

    private static IEnumerable<CodeInstruction> RouteFadesToHost(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.DeclaredPropertyGetter(typeof(Character), nameof(Character.IsLocal));
        var hostOnly = BuildGuard.MethodByName(typeof(SharedWorldHooks), nameof(HostHasLocalView));
        var matched = false;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = hostOnly;
                matched = true;
            }
            yield return instruction;
        }
        if (!matched) throw new InvalidOperationException("Jalur fade karakter lokal berubah.");
    }

    private static IEnumerable<CodeInstruction> RouteCheckpointOwner(IEnumerable<CodeInstruction> instructions)
    {
        var local = BuildGuard.Field(typeof(Character), nameof(Character.localCharacter));
        var owner = BuildGuard.Method(typeof(SharedWorldHooks), nameof(CheckpointOwner), typeof(Character));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(local))
            {
                matches++;
                instruction.opcode = OpCodes.Ldarg_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, owner);
            }
            else yield return instruction;
        }
        if (matches != 2) throw new InvalidOperationException("Tujuan pemulihan inventory checkpoint berubah.");
    }

    private static Character CheckpointOwner(Character scout) =>
        SeatHooks.Scouts?.Find(scout) != null ? scout : Character.localCharacter;

    private static void EndRun()
    {
        var scouts = ActiveScouts;
        if (scouts == null) return;
        if (Character.localCharacter) Character.localCharacter.gameOver = true;
        foreach (var guest in scouts.Guests)
            if (guest.Character) guest.Character.gameOver = true;
    }
}
