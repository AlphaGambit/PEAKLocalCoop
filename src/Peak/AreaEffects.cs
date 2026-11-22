using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace PeakLocalCoop;

internal static class AreaEffects
{
    private static Character selected;
    private static readonly ConditionalWeakTable<MonoBehaviour, Dictionary<Character, float>> Timers = new();
    private static readonly AccessTools.FieldRef<Lava, float> Counter =
        AccessTools.FieldRefAccess<Lava, float>(BuildGuard.Field(typeof(Lava), "counter"));
    private static readonly AccessTools.FieldRef<HotSun, float> SunCounter =
        AccessTools.FieldRefAccess<HotSun, float>(BuildGuard.Field(typeof(HotSun), "counter"));
    private static readonly Action<HotSun> Sun = Bind<Action<HotSun>>(typeof(HotSun), "Update");
    private static readonly Action<Dynamite> Trigger = Bind<Action<Dynamite>>(typeof(Dynamite), "TestLightWick");
    private static readonly Action<InjurySphere> Injury = Bind<Action<InjurySphere>>(typeof(InjurySphere), "Update");
    private static readonly Action<PlayerMoveZone> Push = Bind<Action<PlayerMoveZone>>(typeof(PlayerMoveZone), "AddForceToCharacter");
    private static readonly Action<Lava> Heat = Bind<Action<Lava>>(typeof(Lava), "Heat");
    private static readonly Action<Lava> Contact = Bind<Action<Lava>>(typeof(Lava), "DoEffects");
    private static readonly Action<GameUtils, Vector3, float, int> Mass =
        Bind<Action<GameUtils, Vector3, float, int>>(typeof(GameUtils), "AddMassAffliction", typeof(Vector3), typeof(float), typeof(int));
    private static readonly Action<Action_ApplyMassAffliction, bool> ItemMass =
        Bind<Action<Action_ApplyMassAffliction, bool>>(typeof(Action_ApplyMassAffliction), "TryAddAfflictionToLocalCharacter", typeof(bool));
    private static readonly Action<Peak.InfiniteStamAmulet, Vector3> Amulet =
        Bind<Action<Peak.InfiniteStamAmulet, Vector3>>(typeof(Peak.InfiniteStamAmulet), "TryAddAfflictionToLocalCharacter", typeof(Vector3));

    private static readonly Action<Peak.RitualDaggerFeedBehavior, int> Ritual =
        Bind<Action<Peak.RitualDaggerFeedBehavior, int>>(typeof(Peak.RitualDaggerFeedBehavior), "RPC_RitualDaggerBuff", typeof(int));

    private static T Bind<T>(Type owner, string name, params Type[] args) where T : Delegate =>
        AccessTools.MethodDelegate<T>(BuildGuard.Method(owner, name, args));

    public static void Install(Harmony harmony)
    {
        var route = new HarmonyMethod(typeof(AreaEffects), nameof(RouteRecipient));
        harmony.Patch(BuildGuard.Method(typeof(Lava), "Heat"), transpiler: route);
        harmony.Patch(BuildGuard.Method(typeof(Lava), "DoEffects"), transpiler: route);
        harmony.Patch(BuildGuard.Method(typeof(Lava), "Update"), postfix: new HarmonyMethod(typeof(AreaEffects), nameof(GuestHeat)));
        harmony.Patch(BuildGuard.Method(typeof(Lava), "FixedUpdate"), postfix: new HarmonyMethod(typeof(AreaEffects), nameof(GuestContact)));
        Patch(typeof(HotSun), "Update", nameof(GuestSun));
        Patch(typeof(InjurySphere), "Update", nameof(GuestInjury));
        Patch(typeof(Dynamite), "TestLightWick", nameof(GuestTrigger));
        harmony.Patch(BuildGuard.Method(typeof(PlayerMoveZone), "AddForceToCharacter"), transpiler: route);
        harmony.Patch(BuildGuard.Method(typeof(PlayerMoveZone), "FixedUpdate"),
            postfix: new HarmonyMethod(typeof(AreaEffects), nameof(GuestPush)));
        harmony.Patch(BuildGuard.Method(typeof(SlipperyJellyfish), "OnTriggerEnter", typeof(Collider)),
            transpiler: new HarmonyMethod(typeof(AreaEffects), nameof(RouteJellyfishTarget)));
        Patch(typeof(GameUtils), "AddMassAffliction", nameof(GuestMass), typeof(Vector3), typeof(float), typeof(int));
        Patch(typeof(Action_ApplyMassAffliction), "TryAddAfflictionToLocalCharacter", nameof(GuestItemMass), typeof(bool));
        Patch(typeof(Peak.InfiniteStamAmulet), "TryAddAfflictionToLocalCharacter", nameof(GuestAmulet), typeof(Vector3));

        Patch(typeof(Peak.RitualDaggerFeedBehavior), "RPC_RitualDaggerBuff", nameof(GuestRitual), typeof(int));

        void Patch(Type type, string method, string postfix, params Type[] args) =>
            harmony.Patch(BuildGuard.Method(type, method, args), transpiler: route, postfix: new HarmonyMethod(typeof(AreaEffects), postfix));
    }

    private static Character Recipient() => selected ?? SeatHooks.DirectedScout ?? Character.localCharacter;

    private static IEnumerable<CodeInstruction> RouteRecipient(IEnumerable<CodeInstruction> instructions)
    {
        var local = BuildGuard.Field(typeof(Character), nameof(Character.localCharacter));
        var recipient = BuildGuard.Method(typeof(AreaEffects), nameof(Recipient));
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(local))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = recipient;
            }
            yield return instruction;
        }
    }

    private static void ForGuests<T>(T owner, Action<T> apply)
    {
        var scouts = SeatHooks.Scouts;
        if (scouts?.IsActive != true || selected || SeatHooks.DirectedScout) return;
        try
        {
            foreach (var guest in scouts.Guests)
            {
                var scout = guest.Character;
                if (!scout || scout.data.dead) continue;
                selected = scout;
                apply(owner);
            }
        }
        finally { selected = null; }
    }

    private static IEnumerable<CodeInstruction> RouteJellyfishTarget(IEnumerable<CodeInstruction> instructions)
    {
        // The native collision handler already validates the Character loaded into local 0.
        var local = BuildGuard.Field(typeof(Character), nameof(Character.localCharacter));
        var recipient = BuildGuard.Method(typeof(AreaEffects), nameof(CollisionRecipient), typeof(Character));
        var match = false;
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(local))
            {
                match = true;
                instruction.opcode = OpCodes.Ldloc_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, recipient);
            }
            else yield return instruction;
        }
        if (!match) throw new InvalidOperationException("Jalur target benturan ubur-ubur berubah.");
    }

    private static Character CollisionRecipient(Character hit) =>
        SeatHooks.Scouts?.IsActive == true ? hit : Character.localCharacter;

    private static void WithScoutTimer<T>(T zone, ref float timer, Action<T> apply) where T : MonoBehaviour
    {
        var counters = Timers.GetOrCreateValue(zone);
        var original = timer;
        timer = counters.TryGetValue(selected, out var value) ? value : 0f;
        try { apply(zone); }
        finally
        {
            counters[selected] = timer;
            timer = original;
        }
    }

    private static void GuestHeat(Lava __instance) =>
        ForGuests(__instance, static lava => WithScoutTimer(lava, ref Counter(lava), Heat));
    private static void GuestSun(HotSun __instance) =>
        ForGuests(__instance, static sun => WithScoutTimer(sun, ref SunCounter(sun), Sun));
    private static void GuestTrigger(Dynamite __instance) => ForGuests(__instance, Trigger);
    private static void GuestInjury(InjurySphere __instance) => ForGuests(__instance, Injury);
    private static void GuestPush(PlayerMoveZone __instance) => ForGuests(__instance, static zone =>
    {
        if (zone.zoneBounds.Contains(selected.Center)) Push(zone);
    });

    private static void GuestContact(Lava __instance) => ForGuests(__instance, Contact);
    private static void GuestMass(GameUtils __instance, Vector3 __0, float __1, int __2) =>
        ForGuests(__instance, target => Mass(target, __0, __1, __2));
    private static void GuestItemMass(Action_ApplyMassAffliction __instance, bool __0) =>
        ForGuests(__instance, target => ItemMass(target, __0));
    private static void GuestRitual(Peak.RitualDaggerFeedBehavior __instance, int __0) =>
        ForGuests(__instance, target => Ritual(target, __0));
    private static void GuestAmulet(Peak.InfiniteStamAmulet __instance, Vector3 __0) =>
        ForGuests(__instance, target => Amulet(target, __0));
}
