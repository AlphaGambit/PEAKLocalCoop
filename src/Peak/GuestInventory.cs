using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PeakLocalCoop;

internal sealed class GuestInventory : IDisposable
{
    private readonly Character scout;
    private BackpackReference target;
    private BackpackSlot.BackpackType kind;
    private int slotCount;
    public BackpackReference Target => target;
    public int SlotCount => slotCount;
    public BackpackSlot.BackpackType Kind => kind;
    public int Selected { get; private set; }
    private int WearEntries => target.type == BackpackReference.BackpackType.Item ? 1 : 0;
    public bool FuelSelected => kind == BackpackSlot.BackpackType.Jetpack && Selected >= WearEntries;
    public int Count => IsOpen ? WearEntries + (kind == BackpackSlot.BackpackType.Jetpack ? 1 : slotCount) : 0;
    private bool InReach => scout && target.exists && target.locationTransform &&
        (target.locationTransform.position - scout.Center).sqrMagnitude <= 36f;
    public bool IsOpen => InReach && scout.data.usingBackpackWheel;

    public GuestInventory(Character scout) => this.scout = scout;

    public void Open(BackpackReference backpack, int slots, BackpackSlot.BackpackType type)
    {
        if (!backpack.exists || !scout || scout.data.dead) return;
        target = backpack;
        kind = type;
        slotCount = Mathf.Clamp(slots, 0, backpack.GetData().itemSlots.Length);
        Selected = 0;
        scout.data.usingBackpackWheel = true;
    }

    public void Tick(Gamepad pad, bool togglePressed)
    {
        if (!scout) return;
        if (pad == null || !pad.added || scout.data.dead || scout.data.fullyPassedOut || !InReach)
            Close();
        if (pad == null || !pad.added || scout.data.dead || scout.data.fullyPassedOut || GUIManager.InPauseMenu ||
            GUIManager.instance.windowBlockingInput || scout.data.usingEmoteWheel)
            return;

        if (togglePressed)
        {
            if (IsOpen) Close();
            else if (!scout.player.backpackSlot.IsEmpty())
            {
                var equipped = scout.player.backpackSlot;
                if (equipped.prefab is Backpack backpack)
                    Open(BackpackReference.GetFromEquippedBackpack(scout), backpack.slotCount, equipped.backpackType);
            }
        }

        if (!IsOpen) return;
        if (pad.buttonEast.wasPressedThisFrame)
        {
            Close();
            return;
        }
        if (Count == 0) return;
        if (pad.dpad.right.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame)
            Selected = (Selected + 1) % Count;
        if (pad.dpad.left.wasPressedThisFrame || pad.dpad.up.wasPressedThisFrame)
            Selected = (Selected + Count - 1) % Count;
        if (pad.buttonSouth.wasPressedThisFrame)
        {
            Execute();
            Close();
        }
    }

    private void Execute()
    {
        if (target.TryGetBackpackItem(out var bag) && !bag) return;
        if (bag && Selected == 0)
        {
            if (!bag.blockInteraction) bag.Wear(scout);
            return;
        }

        if (FuelSelected)
        {
            if (!scout.data.currentItem || !scout.data.currentItem.UIData.canBackpack) return;
            if (bag) bag.AddFuel(scout);
            else target.view.GetComponent<CharacterBackpackHandler>().AddFuelToJetpack(scout);
            return;
        }

        var index = (byte)(Selected - WearEntries);
        var slot = target.GetData().itemSlots[index];
        if (!slot.IsEmpty())
        {
            if (target.GetVisuals().TryGetSpawnedItem(index, out var picked) && picked)
                picked.Interact(scout);
            return;
        }
        if (!scout.data.currentItem || !scout.data.currentItem.UIData.canBackpack) return;
        if (bag) bag.Stash(scout, index);
        else target.view.GetComponent<CharacterBackpackHandler>().StashInBackpack(scout, index);
    }

    public string Label(int index)
    {
        if (target.type == BackpackReference.BackpackType.Item)
        {
            if (index == 0) return "Pakai ransel";
            index--;
        }
        if (kind == BackpackSlot.BackpackType.Jetpack) return "Isi bahan bakar";
        var slot = target.GetData().itemSlots[index];
        return slot.IsEmpty() ? "Kosong / simpan" : slot.prefab.GetName();
    }

    public void Close()
    {
        if (scout) scout.data.usingBackpackWheel = false;
        target = default;
    }

    public void Dispose() => Close();

    public static void Install(Harmony harmony)
    {
        var method = BuildGuard.Method(typeof(GUIManager), nameof(GUIManager.OpenBackpackWheel),
            typeof(BackpackReference), typeof(int), typeof(BackpackSlot.BackpackType));
        harmony.Patch(method, prefix: new HarmonyMethod(BuildGuard.MethodByName(typeof(GuestInventory), nameof(OpenGuest))));
    }

    private static bool OpenGuest(BackpackReference backpackReference, int slotCount,
        BackpackSlot.BackpackType backpackType)
    {
        var inventory = PartyRuntime.Current?.InventoryFor(SeatInteraction.CurrentScout);
        if (inventory == null) return true;
        inventory.Open(backpackReference, slotCount, backpackType);
        return false;
    }
}
