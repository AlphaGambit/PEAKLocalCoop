using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace PeakLocalCoop;

internal static class SeatPing
{
    private static readonly Func<Character, bool> CanDoInput = AccessTools.MethodDelegate<Func<Character, bool>>(
        BuildGuard.Method(typeof(Character), "CanDoInput"));

    private static Camera aimCamera;

    public static void Install(Harmony harmony)
    {
        harmony.Patch(BuildGuard.Method(typeof(PointPinger), "DoPing"),
            prefix: new HarmonyMethod(typeof(SeatPing), nameof(BeginAim)),
            finalizer: new HarmonyMethod(typeof(SeatPing), nameof(EndAim)));
        harmony.Patch(BuildGuard.Method(typeof(PointPinger), "TryGetPingHit", typeof(RaycastHit).MakeByRefType(), typeof(Vector3)),
            transpiler: new HarmonyMethod(typeof(SeatPing), nameof(RouteRay)));
        harmony.Patch(BuildGuard.Method(typeof(PointPinger), "get_canPing"),
            postfix: new HarmonyMethod(typeof(SeatPing), nameof(CheckInput)));
        harmony.Patch(BuildGuard.Method(typeof(PointPinger), "ReceivePoint_Rpc", typeof(Vector3), typeof(Vector3)),
            transpiler: new HarmonyMethod(typeof(SeatPing), nameof(RouteVisibility)));
    }

    private static void CheckInput(PointPinger __instance, ref bool __result)
    {
        if (__result && PartyRuntime.Current?.IsLocalRoom == true)
            __result = CanDoInput(__instance.character);
    }

    private static void BeginAim(PointPinger __instance, out Camera __state)
    {
        __state = aimCamera;
        var runtime = PartyRuntime.Current;
        if (runtime?.IsLocalRoom != true || !__instance.character) return;
        aimCamera = runtime.GetGuestCamera(__instance.character);
        if (!aimCamera && __instance.character == Character.localCharacter) aimCamera = runtime.GetCamera(1);
    }

    private static void EndAim(Camera __state) => aimCamera = __state;

    private static IEnumerable<CodeInstruction> RouteRay(IEnumerable<CodeInstruction> instructions)
    {
        var original = BuildGuard.Method(typeof(Camera), nameof(Camera.ScreenPointToRay), typeof(Vector3));
        var target = BuildGuard.Method(typeof(SeatPing), nameof(RayFor), typeof(Camera), typeof(Vector3));
        var matched = false;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                matched = true;
                instruction.opcode = OpCodes.Call;
                instruction.operand = target;
            }
            yield return instruction;
        }
        if (!matched) throw new InvalidOperationException("Jalur bidik ping PEAK berubah.");
    }

    private static Ray RayFor(Camera original, Vector3 screenPoint) => aimCamera
        ? aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0))
        : original.ScreenPointToRay(screenPoint);

    private static IEnumerable<CodeInstruction> RouteVisibility(IEnumerable<CodeInstruction> instructions)
    {
        var host = BuildGuard.Field(typeof(Character), nameof(Character.localCharacter));
        var target = BuildGuard.Method(typeof(SeatPing), nameof(Viewer), typeof(PointPinger));
        var count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(host))
            {
                count++;
                instruction.opcode = OpCodes.Ldarg_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, target);
            }
            else yield return instruction;
        }
        if (count != 2) throw new InvalidOperationException("Jalur visibilitas ping PEAK berubah.");
    }

    private static Character Viewer(PointPinger pinger) =>
        SeatHooks.Scouts?.Find(pinger.character) != null ? pinger.character : Character.localCharacter;
}
