using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace PeakLocalCoop;

internal static class HazardHits
{
    private static readonly ConditionalWeakTable<MonoBehaviour, Dictionary<Character, float>> LastHits = new();

    private readonly struct Hit
    {
        public Character Scout { get; }
        public float HostTime { get; }

        public Hit(Character scout, float hostTime)
        {
            Scout = scout;
            HostTime = hostTime;
        }
    }

    public static void Install(Harmony harmony)
    {
        var route = new HarmonyMethod(typeof(HazardHits), nameof(RouteTarget));
        var finish = new HarmonyMethod(typeof(HazardHits), nameof(EndHit));
        harmony.Patch(BuildGuard.Method(typeof(Peak.MovingSawBlade), "OnTriggerStay", typeof(Collider)),
            prefix: new HarmonyMethod(typeof(HazardHits), nameof(BeginSaw)), transpiler: route, finalizer: finish);
        harmony.Patch(BuildGuard.Method(typeof(Peak.SpikeRoller), "OnCollisionEnter", typeof(Collision)),
            prefix: new HarmonyMethod(typeof(HazardHits), nameof(BeginRoller)), transpiler: route, finalizer: finish);
        harmony.Patch(BuildGuard.Method(typeof(SwingingAxe), "OnCollisionEnter", typeof(Collision)),
            prefix: new HarmonyMethod(typeof(HazardHits), nameof(BeginAxe)), finalizer: finish);
        harmony.Patch(BuildGuard.MethodByName(typeof(SwingingAxe), "<OnCollisionEnter>g__BonkScout|36_0"),
            transpiler: new HarmonyMethod(typeof(HazardHits), nameof(RouteAxeTarget)));
        harmony.Patch(BuildGuard.Method(typeof(BananaPeel), "Update"),
            transpiler: new HarmonyMethod(typeof(HazardHits), nameof(RouteBananaTarget)));
    }

    private static void BeginSaw(Peak.MovingSawBlade __instance, Collider __0, ref float ____lastHitTime, out Hit __state) =>
        __state = BeginHit(__instance, __0, ref ____lastHitTime);

    private static void BeginRoller(Peak.SpikeRoller __instance, Collision __0, ref float ____lastHitTime, out Hit __state) =>
        __state = BeginHit(__instance, __0.collider, ref ____lastHitTime);

    private static void BeginAxe(SwingingAxe __instance, Collision __0, ref float ____lastHitTime, out Hit __state) =>
        __state = BeginHit(__instance, __0.collider, ref ____lastHitTime);

    private static Hit BeginHit(MonoBehaviour hazard, Collider collider, ref float lastTime)
    {
        var scouts = SeatHooks.Scouts;
        if (scouts?.IsActive != true || !collider ||
            !CharacterRagdoll.TryGetCharacterFromCollider(collider, out var hit) || scouts.Find(hit) == null) return default;
        var state = new Hit(hit, lastTime);
        lastTime = LastHits.GetOrCreateValue(hazard).TryGetValue(hit, out var time) ? time : -1f;
        return state;
    }

    private static void EndHit(MonoBehaviour __instance, ref float ____lastHitTime, Hit __state)
    {
        if (!__state.Scout) return;
        LastHits.GetOrCreateValue(__instance)[__state.Scout] = ____lastHitTime;
        ____lastHitTime = __state.HostTime;
    }

    private static Character Target(Character hit) =>
        SeatHooks.Scouts?.Find(hit) != null ? hit : Character.localCharacter;

    private static IEnumerable<CodeInstruction> RouteTarget(IEnumerable<CodeInstruction> instructions)
    {
        var field = BuildGuard.Field(typeof(Character), nameof(Character.localCharacter));
        var recipient = BuildGuard.Method(typeof(HazardHits), nameof(Target), typeof(Character));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(field))
            {
                matches++;
                // Local 0 is the collider's character, validated by the original method.
                instruction.opcode = OpCodes.Ldloc_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, recipient);
            }
            else yield return instruction;
        }
        if (matches != 1) throw new InvalidOperationException("Jalur penerima benturan perangkap berubah.");
    }
    private static IEnumerable<CodeInstruction> RouteAxeTarget(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var field = BuildGuard.Field(typeof(Character), nameof(Character.localCharacter));
        var victim = BuildGuard.Field(original.GetParameters()[0].ParameterType.GetElementType(), "hitCharacter");
        var recipient = BuildGuard.Method(typeof(HazardHits), nameof(Target), typeof(Character));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(field))
            {
                matches++;
                instruction.opcode = OpCodes.Ldarg_1;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Ldfld, victim);
                yield return new CodeInstruction(OpCodes.Call, recipient);
            }
            else yield return instruction;
        }
        if (matches != 1) throw new InvalidOperationException("Jalur korban kapak PEAK berubah.");
    }

    private static IEnumerable<CodeInstruction> RouteBananaTarget(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var field = BuildGuard.Field(typeof(Character), nameof(Character.localCharacter));
        var select = BuildGuard.Method(typeof(HazardHits), nameof(ChooseBananaVictim), typeof(BananaPeel));
        var victim = generator.DeclareLocal(typeof(Character));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            if (!instruction.LoadsField(field))
            {
                yield return instruction;
                continue;
            }
            if (matches++ == 0)
            {
                var loadPeel = new CodeInstruction(OpCodes.Ldarg_0);
                instruction.MoveLabelsTo(loadPeel);
                yield return loadPeel;
                yield return new CodeInstruction(OpCodes.Call, select);
                yield return new CodeInstruction(OpCodes.Stloc, victim);
            }
            instruction.opcode = OpCodes.Ldloc;
            instruction.operand = victim;
            yield return instruction;
        }
        if (matches != 4) throw new InvalidOperationException("Jalur korban kulit pisang PEAK berubah.");
    }

    private static Character ChooseBananaVictim(BananaPeel peel)
    {
        var host = Character.localCharacter;
        var scouts = SeatHooks.Scouts;
        if (scouts?.IsActive != true) return host;
        var position = peel.transform.position;
        var closest = 1f;
        Character chosen = null;
        Consider(host);
        foreach (var guest in scouts.Guests) Consider(guest.Character);
        return chosen ? chosen : host;

        void Consider(Character scout)
        {
            if (!scout || scout.data.dead || !scout.data.isGrounded || scout.data.avarageVelocity.sqrMagnitude < 2.25f)
                return;
            var distance = (scout.Center - position).sqrMagnitude;
            if (distance > closest || chosen && distance >= closest) return;
            closest = distance;
            chosen = scout;
        }
    }

}
