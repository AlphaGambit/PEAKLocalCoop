using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace PeakLocalCoop;

internal static class ItemSeat
{
    private static readonly FieldInfo MainCameraField = BuildGuard.Field(typeof(MainCamera), nameof(MainCamera.instance));
    private static readonly FieldInfo LocalCharacterField = BuildGuard.Field(typeof(Character), nameof(Character.localCharacter));
    private static readonly FieldInfo LocalPlayerField = BuildGuard.Field(typeof(Player), nameof(Player.localPlayer));
    private static readonly FieldInfo InteractionField = BuildGuard.Field(typeof(Interaction), nameof(Interaction.instance));
    private static readonly FieldInfo ComponentItem = BuildGuard.Field(typeof(ItemComponent), "item");
    private static readonly AccessTools.FieldRef<Item, Character> LastHolder =
        AccessTools.FieldRefAccess<Item, Character>(BuildGuard.Field(typeof(Item), "lastHolderCharacter"));
    private static readonly FieldInfo ActionItem = BuildGuard.Field(typeof(ItemActionBase), "item");
    private static readonly MethodInfo TransformGetter = AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform));
    private static readonly MethodInfo MainCameraGetter = AccessTools.PropertyGetter(typeof(Camera), nameof(Camera.main));

    private static readonly AccessTools.FieldRef<RopeSpool, float> RopeScroll =
        AccessTools.FieldRefAccess<RopeSpool, float>(BuildGuard.Field(typeof(RopeSpool), "scroll"));
    private static readonly AccessTools.FieldRef<RopeSpool, float> RopeVelocity =
        AccessTools.FieldRefAccess<RopeSpool, float>(BuildGuard.Field(typeof(RopeSpool), "segsVel"));
    private static readonly AccessTools.FieldRef<RopeSpool, RopeTier> RopeAnchor =
        AccessTools.FieldRefAccess<RopeSpool, RopeTier>(BuildGuard.Field(typeof(RopeSpool), "ropeTier"));

    public static void Install(Harmony harmony)
    {
        harmony.Patch(BuildGuard.Method(typeof(RopeSpool), "Update"),
            postfix: new HarmonyMethod(typeof(ItemSeat), nameof(UpdateGuestSpool)));
        harmony.Patch(BuildGuard.Method(typeof(RescueHook), "GetMiddleScreenRay"),
            prefix: new HarmonyMethod(typeof(ItemSeat), nameof(RescueAim)));
        harmony.Patch(BuildGuard.Method(typeof(CharacterMovement), "TryJetpack"),
            transpiler: new HarmonyMethod(typeof(ItemSeat), nameof(RouteJetpackFuel)));
        harmony.Patch(BuildGuard.Method(typeof(Item), "Consume", typeof(int)),
            transpiler: new HarmonyMethod(typeof(ItemSeat), nameof(RouteConsumedSlot)));
        var patch = new HarmonyMethod(typeof(ItemSeat), nameof(RouteOwner));
        foreach (var target in new[]
        {
            BuildGuard.Method(typeof(RopeShooter), "WillAttach", typeof(RaycastHit).MakeByRefType()),
            BuildGuard.Method(typeof(RopeShooter), "OnPrimaryFinishedCast"),
            BuildGuard.Method(typeof(RopeTier), "Update"),
            BuildGuard.Method(typeof(VineShooter), "WillAttach", typeof(RaycastHit).MakeByRefType()),
            BuildGuard.Method(typeof(VineShooter), "OnPrimaryFinishedCast"),
            BuildGuard.Method(typeof(Action_RaycastDart), "FireDart"),
            BuildGuard.Method(typeof(Action_LaunchPlayer), "RunAction"),
            BuildGuard.Method(typeof(Peak.Action_RaycastSpawnSomething), "FixedUpdate"),
            BuildGuard.Method(typeof(Peak.Action_RaycastSpawnSomething), "RunAction"),
            BuildGuard.Method(typeof(Peak.Action_CloneSelectedItem), "RunAction"),
            BuildGuard.Method(typeof(Item), nameof(Item.CanUseSecondary)),
            BuildGuard.Method(typeof(Item), "StartUseSecondary"),
            BuildGuard.Method(typeof(Item), "FinishCastSecondary"),
            BuildGuard.Method(typeof(Item), "CancelUsePrimary"),
            BuildGuard.Method(typeof(Item), "CancelUseSecondary"),
            BuildGuard.Method(typeof(ItemCooking), "CookingExplodeImpl"),
            BuildGuard.Method(typeof(Constructable), "Update"),
            BuildGuard.Method(typeof(Constructable), nameof(Constructable.TryUpdatePreview)),
            BuildGuard.Method(typeof(Constructable), "CreateOrMovePreview"),
            BuildGuard.Method(typeof(Constructable), nameof(Constructable.FinishConstruction)),
            BuildGuard.Method(typeof(Dynamite), "Update")
        })
            harmony.Patch(target, transpiler: patch);
    }

    private static void UpdateGuestSpool(RopeSpool __instance)
    {
        if (SeatHooks.Scouts?.IsActive != true || !__instance.rope) return;
        var item = (Item)ComponentItem.GetValue(__instance);
        var scout = item ? item.holderCharacter : null;
        if (!scout || scout.data.currentItem != item || SeatHooks.Scouts.Find(scout) == null) return;
        var actions = SeatHooks.ActionsFor(scout);
        if (!__instance.photonView.IsMine || __instance.IsOutOfRope || RopeAnchor(__instance).LookingToPlaceAnchor) return;
        var canScroll = actions?.Connected == true && scout.data.fullyConscious && !scout.data.usingWheel &&
            !GUIManager.InPauseMenu && !GUIManager.instance.windowBlockingInput;
        var direction = canScroll ? actions.ScrollDirection : 0;
        RopeScroll(__instance) = direction * 0.4f;
        // Reverse without spending several low-FPS frames winding in the previous direction.
        if (direction * RopeVelocity(__instance) < 0 || !canScroll)
            RopeVelocity(__instance) = 0f;
    }

    private static bool RescueAim(RescueHook __instance, ref Ray __result)
    {
        var runtime = PartyRuntime.Current;
        if (runtime?.IsLocalRoom != true) return true;
        var scout = __instance.playerHoldingItem;
        var camera = runtime.GetGuestCamera(scout);
        if (!camera && scout == Character.localCharacter) camera = runtime.GetCamera(1);
        if (!camera) return true;
        __result = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return false;
    }

    private static IEnumerable<CodeInstruction> RouteConsumedSlot(IEnumerable<CodeInstruction> source)
    {
        var holder = AccessTools.PropertyGetter(typeof(Item), nameof(Item.holderCharacter));
        var inventoryOwner = BuildGuard.Method(typeof(ItemSeat), nameof(ConsumedSlotOwner), typeof(Item));
        var matches = 0;
        foreach (var instruction in source)
        {
            // The first lookup guards the native consumer event. The rest manage the inventory.
            if (instruction.Calls(holder) && ++matches > 1)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = inventoryOwner;
            }
            yield return instruction;
        }
        if (matches != 8) throw new InvalidOperationException("Jalur konsumsi inventory PEAK berubah.");
    }

    private static Character ConsumedSlotOwner(Item item)
    {
        var recipient = item.holderCharacter;
        if (PartyRuntime.Current?.IsLocalRoom != true) return recipient;
        var giver = item.trueHolderCharacter;
        // Feeding temporarily makes holderCharacter resolve to the recipient for the item effect.
        return giver ? giver : recipient;
    }

    private static IEnumerable<CodeInstruction> RouteJetpackFuel(IEnumerable<CodeInstruction> source)
    {
        var owner = BuildGuard.Field(typeof(CharacterMovement), "character");
        var matches = 0;
        foreach (var instruction in source)
        {
            if (instruction.LoadsField(LocalCharacterField))
            {
                instruction.opcode = OpCodes.Ldarg_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Ldfld, owner);
                matches++;
            }
            else yield return instruction;
        }
        if (matches != 2) throw new InvalidOperationException("Jalur data bahan bakar jetpack berubah.");
    }

    private static IEnumerable<CodeInstruction> RouteOwner(IEnumerable<CodeInstruction> source, MethodBase original)
    {
        var code = new List<CodeInstruction>(source);
        var matched = false;
        for (var i = 0; i < code.Count; i++)
        {
            var instruction = code[i];
            string target = null;
            if (instruction.LoadsField(MainCameraField) && i + 1 < code.Count && code[i + 1].Calls(TransformGetter))
            {
                target = nameof(ItemAim);
                code[i + 1].opcode = OpCodes.Nop;
                code[i + 1].operand = null;
            }
            else if (instruction.Calls(MainCameraGetter)) target = nameof(ItemCamera);
            else if (instruction.LoadsField(LocalCharacterField)) target = nameof(ItemUser);
            else if (instruction.LoadsField(LocalPlayerField)) target = nameof(ItemPlayer);
            else if (instruction.LoadsField(InteractionField)) target = nameof(ItemInteraction);
            if (target != null)
            {
                instruction.opcode = OpCodes.Ldarg_0;
                instruction.operand = null;
                matched = true;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, BuildGuard.Method(typeof(ItemSeat), target, typeof(Component)));
            }
            else yield return instruction;
        }
        if (!matched) throw new InvalidOperationException($"Jalur pemilik item berubah pada {original.DeclaringType.Name}.{original.Name}.");
    }

    private static Character GuestUser(Component source)
    {
        var runtime = PartyRuntime.Current;
        if (runtime?.IsLocalRoom != true) return null;
        var item = source as Item;
        if (!item && source is ItemComponent) item = (Item)ComponentItem.GetValue(source);
        if (!item && source is ItemActionBase) item = (Item)ActionItem.GetValue(source);
        var user = item ? item.holderCharacter : null;
        return user && runtime.GetGuestCamera(user) ? user : null;
    }

    private static Character ItemUser(Component source) => GuestUser(source) ?? Character.localCharacter;

    private static Player ItemPlayer(Component source)
    {
        var guest = GuestUser(source);
        if (guest) return guest.player;
        if (source is Item item && SeatHooks.Scouts?.IsActive == true)
        {
            var previous = LastHolder(item);
            if (SeatHooks.Scouts.Find(previous) != null) return previous.player;
        }
        return Player.localPlayer;
    }

    private static Camera ItemCamera(Component source)
    {
        var guest = GuestUser(source);
        return guest ? PartyRuntime.Current.GetGuestCamera(guest) : Camera.main;
    }

    private static Transform ItemAim(Component source)
    {
        var guest = GuestUser(source);
        return guest ? PartyRuntime.Current.GetGuestCamera(guest).transform : MainCamera.instance.transform;
    }

    private static Interaction ItemInteraction(Component source)
    {
        var guest = GuestUser(source);
        return guest ? PartyRuntime.Current.GuestInteraction(guest) ?? Interaction.instance : Interaction.instance;
    }
}
