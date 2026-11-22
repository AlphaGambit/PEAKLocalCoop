using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class GuestEmote
{
    private static readonly Action<EmoteWheel> Init = AccessTools.MethodDelegate<Action<EmoteWheel>>(
        BuildGuard.Method(typeof(EmoteWheel), "InitWheel"));
    private static readonly Action<EmoteWheel, int> Tab = AccessTools.MethodDelegate<Action<EmoteWheel, int>>(
        BuildGuard.Method(typeof(EmoteWheel), "Tab", typeof(int)));
    private static readonly Action<CharacterAnimations, string> Play = AccessTools.MethodDelegate<Action<CharacterAnimations, string>>(
        BuildGuard.Method(typeof(CharacterAnimations), "PlayEmote", typeof(string)));
    private static readonly AccessTools.FieldRef<EmoteWheelSlice, EmoteWheelData> Data =
        AccessTools.FieldRefAccess<EmoteWheelSlice, EmoteWheelData>(BuildGuard.Field(typeof(EmoteWheelSlice), "emoteData"));
    private readonly Character scout;
    private readonly EmoteWheel wheel;
    private EmoteWheelSlice selected;
    private bool waitForRelease;

    public GameObject Root => wheel.gameObject;
    public bool IsOpen => Root.activeSelf;

    public GuestEmote(GameObject source, Transform parent, Character scout)
    {
        this.scout = scout;
        wheel = Object.Instantiate(source, parent, false).GetComponent<EmoteWheel>();
        wheel.name = "Native guest emote wheel";
        wheel.enabled = false;
        Root.SetActive(false);
        foreach (var slice in wheel.slices) slice.enabled = false;
        foreach (var graphic in Root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        foreach (var button in Root.GetComponentsInChildren<Button>(true))
            button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    public void Tick(Gamepad pad, bool backpackOpen)
    {
        var held = scout.input.emoteIsPressed;
        var available = pad != null && pad.added && !backpackOpen && !scout.data.dead &&
            !scout.data.fullyPassedOut && !scout.refs.stats.won && !scout.data.isClimbing &&
            !scout.data.isRopeClimbing && !GUIManager.InPauseMenu &&
            !GUIManager.instance.windowBlockingInput;
        if (!available)
        {
            waitForRelease = held;
            Close(play: false);
            return;
        }
        if (!held)
        {
            waitForRelease = false;
            Close(play: true);
            return;
        }
        if (waitForRelease) return;
        if (!IsOpen)
        {
            Root.SetActive(true);
            Init(wheel);
            scout.data.usingEmoteWheel = true;
        }
        if (scout.input.selectSlotBackwardWasPressed)
        {
            Select(null);
            Tab(wheel, -1);
        }
        if (scout.input.selectSlotForwardWasPressed)
        {
            Select(null);
            Tab(wheel, 1);
        }
        var aim = pad.rightStick.ReadValue();
        var bestAngle = float.MaxValue;
        EmoteWheelSlice match = null;
        foreach (var slice in wheel.slices)
        {
            var data = Data(slice);
            slice.button.interactable = data && (!data.requireGrounded || scout.data.isGrounded);
            if (!slice.button.interactable || aim.sqrMagnitude < 0.5f) continue;
            var angle = Vector3.Angle(aim, slice.GetUpVector());
            if (angle < bestAngle)
            {
                match = slice;
                bestAngle = angle;
            }
        }
        Select(match);
    }

    private void Select(EmoteWheelSlice next)
    {
        if (selected == next) return;
        var pointer = new PointerEventData(EventSystem.current);
        if (selected)
        {
            selected.button.OnPointerExit(pointer);
            selected.Dehover();
        }
        selected = next;
        if (selected)
        {
            selected.button.OnPointerEnter(pointer);
            selected.Hover();
        }
    }

    public void Close(bool play)
    {
        if (!IsOpen) return;
        var chosen = selected ? Data(selected) : null;
        Select(null);
        Root.SetActive(false);
        if (!scout) return;
        scout.data.usingEmoteWheel = false;
        if (play && chosen && (!chosen.requireGrounded || scout.data.isGrounded))
            Play(scout.refs.animations, chosen.anim);
    }
}
