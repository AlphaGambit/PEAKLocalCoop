using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class HostMenuInput : IDisposable
{
    private readonly InputSystemUIInputModule module;
    private readonly InputActionAsset original;
    private readonly InputActionAsset actions;

    public HostMenuInput()
    {
        if (!EventSystem.current || !EventSystem.current.TryGetComponent(out module) || !module.actionsAsset)
            return;
        original = module.actionsAsset;
        actions = InputActionAsset.FromJson(original.ToJson());
        try
        {
            actions.LoadBindingOverridesFromJson(original.SaveBindingOverridesAsJson());
            actions.devices = new InputDevice[] { Keyboard.current, Mouse.current }
                .Where(device => device != null && device.added).ToArray();
            module.actionsAsset = actions;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (!actions) return;
        if (module && module.actionsAsset == actions)
            module.actionsAsset = original;
        actions.Disable();
        Object.Destroy(actions);
    }
}
