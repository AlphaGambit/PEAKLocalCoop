using System;
using System.Collections.Generic;
using HarmonyLib;
using Peak.Afflictions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Zorro.ControllerSupport;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

// Only copy the visual branches. The original GUIManager and its input stay with P1.
internal sealed class GuestHud : IDisposable
{
    private static readonly Action<StaminaBar> StartBar = Native<StaminaBar>("Start");
    private static readonly Action<StaminaBar> TickBar = Native<StaminaBar>("Update");
    private static readonly Action<InputIcon> InitPrompt = Native<InputIcon>("Awake");
    private static readonly Action<InputIcon, InputScheme> SetPrompt =
        AccessTools.MethodDelegate<Action<InputIcon, InputScheme>>(BuildGuard.Method(typeof(InputIcon), "SetText", typeof(InputScheme)));
    private static readonly Dictionary<Character, StaminaBar> seatBars = new();
    private static readonly System.Reflection.FieldInfo InitialSlotSize = BuildGuard.Field(typeof(InventoryItemUI), "startingSizeDelta");
    private static Character rendering;
    private readonly GameObject root;
    private readonly RectTransform panel;
    private readonly RectTransform units;
    private readonly GameObject gameplay;
    private readonly GameObject dyingBar;
    private readonly Image dyingFill;
    private readonly Gradient dyingGradient;
    private readonly GameObject dyingMushrooms;
    private readonly GameObject dyingFlag;
    private readonly Animator dyingAnimation;
    private bool playedDeath;
    private readonly StaminaBar bar;
    private readonly InventoryItemUI[] slots;
    private readonly InventoryItemUI bag;
    private readonly InventoryItemUI temporary;
    private readonly BackpackWheel wheel;
    private readonly Canvas canvas;
    private readonly GuestInventory inventory;
    private readonly LocalParty.Seat seat;
    private readonly InputIcon[] prompts;
    private readonly TextMeshProUGUI guide;
    private readonly TextMeshProUGUI hoverName;
    private readonly GuestReticle reticle;
    private readonly GuestEmote emote;
    private readonly UI_Rope ropeMeter;
    private Gamepad lastPad;
    private readonly Character scout;
    private readonly Camera camera;
    private BackpackWheelSlice highlighted;
    private int lastSelection = -2;
    private float nextItemRefresh;

    private static Action<T> Native<T>(string method) =>
        AccessTools.MethodDelegate<Action<T>>(BuildGuard.Method(typeof(T), method));

    public static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.PropertyGetter(typeof(Character), nameof(Character.observedCharacter)),
            prefix: new HarmonyMethod(typeof(GuestHud), nameof(Observed)));
        var restore = new HarmonyMethod(typeof(GuestHud), nameof(RestoreBar));
        harmony.Patch(BuildGuard.Method(typeof(Character), nameof(Character.MoraleBoost), typeof(float), typeof(int)),
            prefix: new HarmonyMethod(typeof(GuestHud), nameof(BeginMorale)), finalizer: restore);
        foreach (var name in new[] { nameof(Affliction_InfiniteStamina.OnApplied), nameof(Affliction_InfiniteStamina.OnRemoved) })
            harmony.Patch(BuildGuard.Method(typeof(Affliction_InfiniteStamina), name),
                prefix: new HarmonyMethod(typeof(GuestHud), nameof(BeginSugar)), finalizer: restore);
    }

    private static void BeginMorale(Character __instance, out StaminaBar __state) => __state = UseBar(__instance);
    private static void BeginSugar(Affliction_InfiniteStamina __instance, out StaminaBar __state) => __state = UseBar(__instance.character);

    private static StaminaBar UseBar(Character owner)
    {
        if (!owner || !seatBars.TryGetValue(owner, out var target) || !target || !GUIManager.instance) return null;
        var previous = GUIManager.instance.bar;
        GUIManager.instance.bar = target;
        return previous;
    }

    private static void RestoreBar(StaminaBar __state)
    {
        if (__state && GUIManager.instance) GUIManager.instance.bar = __state;
    }

    private static bool Observed(ref Character __result)
    {
        if (ReferenceEquals(rendering, null)) return true;
        __result = rendering;
        return false;
    }

    public GuestHud(LocalParty.Seat seat, Character scout, Camera camera, GuestInventory inventory)
    {
        this.seat = seat;
        this.scout = scout;
        this.camera = camera;
        this.inventory = inventory;
        var source = GUIManager.instance;
        root = new GameObject($"Local HUD P{seat.Number}", typeof(RectTransform), typeof(Canvas));
        root.SetActive(false);
        try
        {
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            panel = TrailLobby.Rect(root.transform, "Viewport", Vector2.zero, Vector2.zero);
            panel.gameObject.AddComponent<RectMask2D>();
            units = TrailLobby.Rect(panel, "HUD", Vector2.zero, Vector2.zero);

            var gameplayRoot = TrailLobby.Rect(units, "Gameplay", Vector2.zero, Vector2.zero);
            gameplayRoot.anchorMin = Vector2.zero;
            gameplayRoot.anchorMax = Vector2.one;
            gameplayRoot.offsetMin = gameplayRoot.offsetMax = Vector2.zero;
            gameplay = gameplayRoot.gameObject;
            var dyingTemplate = source.dyingBarObject.transform;
            dyingBar = Object.Instantiate(source.dyingBarObject, units, false);
            dyingBar.name = "Native guest dying bar";
            dyingBar.SetActive(false);
            Transform Copy(Transform original) => GuestReticle.FindClone(original, dyingTemplate, dyingBar.transform);
            dyingFill = Copy(source.dyingBarImage.transform).GetComponent<Image>();
            dyingGradient = source.dyingBarGradient;
            dyingMushrooms = Copy(source.dyingBarMushrooms.transform).gameObject;
            dyingFlag = Copy(source.dyingBarFlag.transform).gameObject;
            dyingAnimation = Copy(source.dyingBarAnimator.transform).GetComponent<Animator>();

            var group = Object.Instantiate(source.bar.transform.parent, gameplayRoot, false);
            bar = group.GetComponentInChildren<StaminaBar>(true);
            bar.enabled = false;
            bar.moraleBoostText.enabled = false;
            bar.rainbowStamina.enabled = false;
            var layout = Object.Instantiate(source.items[0].transform.parent, gameplayRoot, false);
            layout.gameObject.SetActive(true);
            slots = new InventoryItemUI[source.items.Length];
            for (var i = 0; i < slots.Length; i++) slots[i] = CloneSlot(layout, source.items[i]);
            bag = CloneSlot(layout, source.backpack);
            temporary = CloneSlot(layout, source.temporaryItem);
            prompts = layout.GetComponentsInChildren<InputIcon>(true);
            foreach (var prompt in prompts)
            {
                prompt.enabled = false; // Do not subscribe a guest prompt to P1's device changes.
                InitPrompt(prompt);
                SetPrompt(prompt, InputScheme.Gamepad);
            }
            var nameFont = source.interactNameText.font;
            var marker = TrailLobby.Text(gameplayRoot, nameFont, $"P{seat.Number}", new Vector2(90, 48),
                Vector2.zero, 34, Color.white, TextAlignmentOptions.MidlineLeft);
            marker.overflowMode = TextOverflowModes.Overflow;
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0, 1);
            marker.rectTransform.anchoredPosition = new Vector2(75, -40);
            marker.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            emote = new GuestEmote(source.emoteWheel, gameplayRoot, scout);
            reticle = new GuestReticle(source, scout, gameplayRoot, emote.Root);
            ropeMeter = Object.Instantiate(source.ui_rope, gameplayRoot, false);
            ropeMeter.gameObject.SetActive(false);
            hoverName = TrailLobby.Text(gameplayRoot, nameFont, "", new Vector2(850, 62), new Vector2(0, -55),
                source.interactNameText.fontSize, source.interactNameText.color, TextAlignmentOptions.Center);
            hoverName.fontSharedMaterial = source.interactNameText.fontSharedMaterial;

            wheel = Object.Instantiate(source.backpackWheel, gameplayRoot, false);
            wheel.enabled = false; // GuestInventory owns selection, confirmation and cancellation.
            wheel.gameObject.SetActive(false);
            foreach (var button in wheel.GetComponentsInChildren<Button>(true))
                button.navigation = new Navigation { mode = Navigation.Mode.None };
            var sprites = Zorro.Core.SingletonAsset<InputSpriteData>.Instance;
            guide = TrailLobby.Text(wheel.transform, wheel.chosenItemText.font,
                $"D-pad  Pilih      {sprites.GetSpriteTagFromInputPathGamepad("buttonSouth")} Konfirmasi      {sprites.GetSpriteTagFromInputPathGamepad("buttonEast")} Tutup",
                new Vector2(1250, 80), Vector2.zero, 32, Color.white, TextAlignmentOptions.Center);
            guide.rectTransform.anchorMin = guide.rectTransform.anchorMax = new Vector2(0.5f, 0.13f);
            var shade = guide.gameObject.AddComponent<Shadow>();
            shade.effectDistance = new Vector2(1, -2);

            root.SetActive(true);
            StartBar(bar);
            // The live host template already hid zero-valued ailments during its own Start.
            bar.afflictions = group.GetComponentsInChildren<BarAffliction>(true);
            UpdateViewport();
            seatBars.Add(scout, bar);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static InventoryItemUI CloneSlot(Transform layout, InventoryItemUI original)
    {
        var slot = layout.GetChild(original.transform.GetSiblingIndex()).GetComponent<InventoryItemUI>();
        slot.enabled = false;
        var size = (Vector2)InitialSlotSize.GetValue(original);
        if (size.x > 0 && size.y > 0) slot.rectTransform.sizeDelta = size;
        slot.Clear();
        slot.Start(); // Cache the slot geometry before its first selection tween.
        return slot;
    }

    private void UpdateViewport()
    {
        var viewport = camera.rect;
        if (panel.anchorMin != viewport.min) panel.anchorMin = viewport.min;
        if (panel.anchorMax != viewport.max) panel.anchorMax = viewport.max;
        var scale = Mathf.Max(0.01f, camera.pixelHeight / 1080f);
        var size = new Vector2(camera.pixelWidth / scale, camera.pixelHeight / scale);
        if (units.sizeDelta != size) units.sizeDelta = size;
        if (units.localScale.x != scale) units.localScale = Vector3.one * scale;
    }

    private void RefreshDevice()
    {
        var pad = seat.Pad;
        if (pad == lastPad && guide.spriteAsset) return;
        lastPad = pad;
        var sprites = Zorro.Core.SingletonAsset<InputSpriteData>.Instance;
        var layout = pad?.layout ?? "";
        var atlas = layout.Contains("DualSense") ? sprites.ps5Sprites :
            layout.Contains("DualShock") ? sprites.ps4Sprites :
            layout.Contains("Switch") ? sprites.switchSprites : sprites.xboxSprites;
        guide.spriteAsset = atlas;
        foreach (var prompt in prompts) prompt.GetComponent<TMP_Text>().spriteAsset = atlas;
    }

    public void Tick(IInteractible hovered)
    {
        UpdateViewport();
        emote.Tick(seat.Pad, inventory.IsOpen);
        canvas.enabled = !LoadingScreenHandler.loading && !GUIManager.InPauseMenu;
        var incapacitated = scout.data.dead || scout.data.fullyPassedOut;
        gameplay.SetActive(!incapacitated);
        dyingBar.SetActive(incapacitated);
        if (!canvas.enabled || !scout.player) return;
        if (incapacitated)
        {
            var remaining = 1f - Mathf.Clamp01(scout.data.deathTimer);
            dyingFill.fillAmount = remaining;
            dyingFill.color = dyingGradient.Evaluate(remaining);
            dyingMushrooms.SetActive(scout.refs.afflictions.willZombify);
            dyingFlag.SetActive(scout.data.checkpointFlags.Count > 0);
            if (!playedDeath && scout.data.dead) dyingAnimation.Play("Dead", 0, 0f);
            playedDeath = scout.data.dead;
            return;
        }
        playedDeath = false;
        RefreshDevice();
        reticle.Tick(!inventory.IsOpen && !emote.IsOpen);
        var held = scout.data.currentItem;
        var spool = held ? held.GetComponent<RopeSpool>() : null;
        var showRope = spool && !scout.data.usingWheel;
        if (ropeMeter.gameObject.activeSelf != showRope) ropeMeter.gameObject.SetActive(showRope);
        if (showRope && spool.rope) ropeMeter.UpdateRope(spool.rope.GetRopeSegments().Count);
        hoverName.text = !scout.data.usingWheel && hovered is Object target && target ? hovered.GetName() : "";
        var previous = Character.localCharacter;
        var previousRendering = rendering;
        try
        {
            rendering = Character.localCharacter = scout;
            bar.ChangeBar();
            TickBar(bar);
            var selected = scout.refs.items.currentSelectedSlot;
            var id = selected.IsSome ? (int)selected.Value : -1;
            if (Time.unscaledTime >= nextItemRefresh || id != lastSelection)
            {
                var player = scout.player;
                for (var i = 0; i < slots.Length; i++)
                {
                    var present = i < player.itemSlots.Length;
                    slots[i].gameObject.SetActive(present);
                    if (!present) continue;
                    var item = player.itemSlots[i];
                    slots[i].SetItem(item);
                    if (item.IsEmpty()) slots[i].outline.enabled = true;
                    if (id != lastSelection) slots[i].SetSelected();
                }
                bag.SetItem(player.backpackSlot);
                if (id != lastSelection) bag.SetSelected();
                var tempSlot = player.GetItemSlot(250);
                temporary.gameObject.SetActive(!tempSlot.IsEmpty());
                if (temporary.gameObject.activeSelf) temporary.SetItem(tempSlot);
                nextItemRefresh = Time.unscaledTime + 0.1f;
                lastSelection = id;
            }
            DrawWheel();
        }
        finally
        {
            rendering = previousRendering;
            Character.localCharacter = previous;
        }
    }

    private void DrawWheel()
    {
        if (!inventory.IsOpen)
        {
            if (wheel.gameObject.activeSelf) wheel.gameObject.SetActive(false);
            highlighted = null;
            return;
        }
        if (!wheel.gameObject.activeSelf)
            wheel.InitWheel(inventory.Target, inventory.SlotCount, inventory.Kind);
        if (inventory.Count == 0) return;
        var index = inventory.Selected + (inventory.Target.type == BackpackReference.BackpackType.Item ? 0 : 1);
        var slice = inventory.FuelSelected
            ? wheel.jetpackSlice : wheel.slices[index];
        if (highlighted == slice) return;
        var pointer = new PointerEventData(EventSystem.current);
        if (highlighted)
        {
            highlighted.Dehover();
            highlighted.button.OnPointerExit(pointer);
        }
        highlighted = slice;
        slice.button.OnPointerEnter(pointer);
        slice.Hover();
        if (string.IsNullOrEmpty(wheel.chosenItemText.text))
            wheel.chosenItemText.text = inventory.Label(inventory.Selected);
    }

    public void Dispose()
    {
        emote?.Close(play: false);
        reticle?.Dispose();
        if (!ReferenceEquals(scout, null)) seatBars.Remove(scout);
        if (!root) return;
        root.SetActive(false);
        Object.Destroy(root);
    }
}
