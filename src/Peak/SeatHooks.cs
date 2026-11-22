using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using Zorro.ControllerSupport;
using NetworkPlayer = Photon.Realtime.Player;

namespace PeakLocalCoop;

internal static class SeatHooks
{
    public static SeatActions HostActions { get; private set; }
    public static OfflineScouts Scouts { get; set; }
    public static readonly Dictionary<CharacterInput, SeatActions> Inputs = new Dictionary<CharacterInput, SeatActions>();
    private static readonly Dictionary<CharacterMovement, Character> movements = new Dictionary<CharacterMovement, Character>();
    private static SeatActions currentActions;
    public static Character DirectedScout { get; private set; }

    public static void Add(Character character, SeatActions actions, bool host = false)
    {
        Inputs.Add(character.input, actions);
        movements.Add(character.refs.movement, character);
        if (host) HostActions = actions;
    }

    public static SeatActions ActionsFor(Character character) =>
        character && Inputs.TryGetValue(character.input, out var actions) ? actions : null;

    public static void Remove(Character character)
    {
        if (!character) return;
        movements.Remove(character.refs.movement);
        if (Inputs.TryGetValue(character.input, out var actions))
        {
            Inputs.Remove(character.input);
            actions.Dispose();
        }
    }

    public static void Clear()
    {
        foreach (var input in Inputs.Values) input.Dispose();
        Inputs.Clear();
        HostActions = null;
        movements.Clear();
    }

    public static void Install(Harmony harmony)
    {
        Patch(AccessTools.DeclaredPropertyGetter(typeof(Character), nameof(Character.IsLocal)), postfix: nameof(IsLocal));
        Patch(BuildGuard.Method(typeof(PhotonNetwork), "RPC", typeof(PhotonView), typeof(string),
            typeof(RpcTarget), typeof(NetworkPlayer), typeof(bool), typeof(object[])), prefix: nameof(RouteGuestRpc), finalizer: nameof(RestoreGuestRpc));
        Patch(BuildGuard.Method(typeof(CharacterInput), "Sample", typeof(bool)), nameof(BeginSample), nameof(ClearBlockedItemInput), nameof(EndSample));
        Patch(BuildGuard.Method(typeof(CharacterInput), nameof(CharacterInput.HotbarKeyWasPressed), typeof(int)), nameof(BeginHotbar), finalizer: nameof(EndSample));
        Patch(BuildGuard.Method(typeof(CharacterInput), nameof(CharacterInput.HotbarKeyIsPressed), typeof(int)), nameof(BeginHotbar), finalizer: nameof(EndSample));
        Patch(BuildGuard.Method(typeof(CharacterMovement), "CameraLook"), nameof(BeginLook), finalizer: nameof(EndLook));
        Patch(BuildGuard.Method(typeof(InputHandler), nameof(InputHandler.GetCurrentUsedInputScheme)), postfix: nameof(Scheme));
        Patch(BuildGuard.Method(typeof(Character), "CanDoInput"), postfix: nameof(CanDoInput));
        Patch(BuildGuard.Method(typeof(UIInputHandler), "Sample"), nameof(BeginUi), finalizer: nameof(EndUi));

        void Patch(MethodBase original, string prefix = null, string postfix = null, string finalizer = null)
        {
            HarmonyMethod Hook(string name) => name == null ? null : new HarmonyMethod(BuildGuard.MethodByName(typeof(SeatHooks), name));
            harmony.Patch(original ?? throw new MissingMethodException("Missing local co-op hook."),
                prefix: Hook(prefix), postfix: Hook(postfix), finalizer: Hook(finalizer));
        }
    }

    private static void IsLocal(Character __instance, ref bool __result)
    {
        if (!__result && Scouts?.Find(__instance) != null) __result = true;
    }

    private static void RouteGuestRpc(ref NetworkPlayer player, out Character __state)
    {
        __state = DirectedScout;
        if (Scouts?.IsActive != true) return;
        if (Scouts.Contains(player))
        {
            DirectedScout = Scouts.FindActor(player.ActorNumber);
            player = PhotonNetwork.LocalPlayer;
        }
        else
            DirectedScout = ReferenceEquals(player, PhotonNetwork.LocalPlayer) ? Character.localCharacter : null;
    }

    private static void RestoreGuestRpc(Character __state) => DirectedScout = __state;

    private static void BeginSample(CharacterInput __instance, out SeatActions __state)
    {
        __state = null;
        if (Scouts?.IsActive == true && Inputs.TryGetValue(__instance, out var pad)) Enter(pad, out __state);
    }

    private static void ClearBlockedItemInput(CharacterInput __instance, bool __0, SeatActions __state)
    {
        if (__0 || __state?.Scheme != InputScheme.Gamepad) return;
        __instance.scrollBackwardWasPressed = false;
        __instance.scrollForwardWasPressed = false;
        __instance.scrollBackwardIsPressed = false;
        __instance.scrollForwardIsPressed = false;
        __instance.scrollInput = 0;
        __instance.pingWasPressed = false;
        __instance.selectBackpackWasPressed = false;
    }

    private static void BeginHotbar(CharacterInput __instance, out SeatActions __state)
    {
        __state = null;
        if (currentActions == null) BeginSample(__instance, out __state);
    }

    private static void BeginLook(CharacterMovement __instance, out (SeatActions Previous, bool Legacy) __state)
    {
        __state = (currentActions, PerformancePaths.UseLegacyInput);
        if (!movements.TryGetValue(__instance, out var character))
        {
            __state.Legacy = false;
            return;
        }
        if (__state.Legacy)
            BeginSample(character.input, out __state.Previous);
        else if (Scouts?.IsActive == true && Inputs.TryGetValue(character.input, out var actions))
            currentActions = actions;
    }

    private static void EndLook((SeatActions Previous, bool Legacy) __state)
    {
        if (__state.Legacy) EndSample(__state.Previous);
        else currentActions = __state.Previous;
    }

    private static void Enter(SeatActions pad, out SeatActions state)
    {
        pad.InstallGameplay();
        currentActions = pad;
        state = pad;
    }

    private static void EndSample(SeatActions __state)
    {
        if (__state == null) return;
        __state.RestoreGameplay();
        currentActions = null;
    }

    private static void Scheme(ref InputScheme __result)
    {
        if (Scouts?.IsActive == true)
            __result = currentActions?.Scheme ?? InputScheme.KeyboardMouse;
    }

    private static void BeginUi(out SeatActions __state)
    {
        __state = Scouts?.IsActive == true ? HostActions : null;
        __state?.InstallInterface();
    }

    private static void EndUi(SeatActions __state) => __state?.RestoreInterface();

    private static void CanDoInput(Character __instance, ref bool __result)
    {
        if (Scouts?.IsActive != true || !Inputs.TryGetValue(__instance.input, out var actions)) return;
        if (Scouts.Find(__instance) != null)
            __result = actions.Connected && !__instance.data.usingWheel && !GUIManager.InPauseMenu
                && !GUIManager.instance.windowBlockingInput
                && !LoadingScreenHandler.loading && Character.localCharacter && !Character.localCharacter.gameOver;
        else if (!actions.Connected)
            __result = false;
    }
}
