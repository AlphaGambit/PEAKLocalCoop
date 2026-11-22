using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine.InputSystem;
using Zorro.ControllerSupport;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class SeatActions : IDisposable
{
    private sealed class ActionFields
    {
        private readonly FieldInfo[] fields;
        private readonly AccessTools.FieldRef<InputAction>[] references;
        private readonly InputAction[] replacements;
        private readonly InputAction[] previous;

        public ActionFields(Type owner, InputActionAsset asset)
        {
            fields = owner.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(InputAction)).ToArray();
            references = Array.ConvertAll(fields, AccessTools.StaticFieldRefAccess<InputAction>);
            replacements = new InputAction[fields.Length];
            previous = new InputAction[fields.Length];
            for (var i = 0; i < fields.Length; i++)
            {
                var original = (InputAction)fields[i].GetValue(null);
                if (original != null)
                    replacements[i] = asset.FindAction(original.id) ?? throw new InvalidOperationException($"Input action {original.name} hilang.");
            }
        }

        public void Install()
        {
            for (var i = 0; i < fields.Length; i++)
            {
                if (PerformancePaths.UseLegacyInput)
                {
                    previous[i] = (InputAction)fields[i].GetValue(null);
                    fields[i].SetValue(null, replacements[i]);
                }
                else
                {
                    ref var action = ref references[i]();
                    previous[i] = action;
                    action = replacements[i];
                }
            }
        }

        public void Restore()
        {
            for (var i = 0; i < fields.Length; i++)
            {
                if (PerformancePaths.UseLegacyInput) fields[i].SetValue(null, previous[i]);
                else references[i]() = previous[i];
            }
        }
    }

    internal InputActionAsset Asset { get; }
    public InputScheme Scheme { get; }
    private readonly ActionFields gameplay;
    private readonly ActionFields interfaceActions;
    private readonly InputAction[] hotbar;
    private readonly InputAction backpack;
    private readonly InputAction scrollForward;
    private readonly InputAction scrollBackward;
    private InputAction[] previousHotbar;
    private InputDevice[] devices;

    public bool BackpackPressed => backpack.WasPerformedThisFrame();
    public int ScrollDirection => (scrollForward.IsPressed() ? 1 : 0) - (scrollBackward.IsPressed() ? 1 : 0);

    public bool Connected
    {
        get
        {
            foreach (var device in devices)
                if (device.added) return true;
            return false;
        }
    }

    public SeatActions(InputScheme scheme, params InputDevice[] devices)
    {
        Scheme = scheme;
        var source = InputSystem.actions;
        // Round-trip only binding definitions. A live Unity asset may hold shared runtime bindings.
        Asset = InputActionAsset.FromJson(source.ToJson());
        try
        {
            Asset.LoadBindingOverridesFromJson(source.SaveBindingOverridesAsJson());
            Pair(devices);
            gameplay = new ActionFields(typeof(CharacterInput), Asset);
            interfaceActions = new ActionFields(typeof(UIInputHandler), Asset);
            backpack = Asset.FindAction("SelectBackpack", throwIfNotFound: true);
            scrollForward = Asset.FindAction("ScrollForward", throwIfNotFound: true);
            scrollBackward = Asset.FindAction("ScrollBackward", throwIfNotFound: true);
            hotbar = CharacterInput.hotbarActions.Select(action => Asset.FindAction(action.id) ?? throw new InvalidOperationException($"Hotbar action {action.name} hilang.")).ToArray();
            Asset.Enable();
            // Local gamepads share one session and do not transmit voice.
            if (scheme == InputScheme.Gamepad) Asset.FindAction("PushToTalk", throwIfNotFound: true).Disable();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Pair(params InputDevice[] devices)
    {
        this.devices = devices.Where(device => device != null && device.added).ToArray();
        Asset.devices = this.devices;
    }

    public void InstallGameplay()
    {
        gameplay.Install();
        previousHotbar = CharacterInput.hotbarActions;
        CharacterInput.hotbarActions = hotbar;
    }

    public void RestoreGameplay()
    {
        gameplay.Restore();
        CharacterInput.hotbarActions = previousHotbar;
    }

    public void InstallInterface() => interfaceActions.Install();
    public void RestoreInterface() => interfaceActions.Restore();

    public void Dispose()
    {
        Asset.Disable();
        Object.Destroy(Asset);
    }
}
