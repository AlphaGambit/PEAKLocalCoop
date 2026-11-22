using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class SeatInteraction : IDisposable
{
    private static readonly MethodInfo TickNative = BuildGuard.Method(typeof(Interaction), "LateUpdate");
    private static readonly Action<Interaction> TickOriginal = AccessTools.MethodDelegate<Action<Interaction>>(TickNative);
    private static Transform guestRaySource;
    public static Character CurrentScout { get; private set; }
    private readonly Interaction interaction;

    public Interaction Native => interaction;

    public IInteractible Hovered => interaction ? interaction.currentHovered : null;

    public SeatInteraction(Camera camera)
    {
        var primary = Interaction.instance;
        if (!primary) throw new InvalidOperationException("Interaction utama belum siap.");
        try
        {
            interaction = camera.gameObject.AddComponent<Interaction>();
            interaction.enabled = false;
            interaction.distance = primary.distance;
            interaction.area = primary.area;
            interaction.maxCharacterInteractAngle = primary.maxCharacterInteractAngle;
        }
        finally
        {
            Interaction.instance = primary;
        }
    }

    public void Tick(Character scout, Camera camera)
    {
        var previousScout = Character.localCharacter;
        var previousPlayer = Player.localPlayer;
        var previousInteraction = Interaction.instance;
        var previousSource = guestRaySource;
        var previousCurrent = CurrentScout;
        try
        {
            CurrentScout = scout;
            Character.localCharacter = scout;
            Player.localPlayer = scout.player;
            Interaction.instance = interaction;
            guestRaySource = camera.transform;
            TickOriginal(interaction);
        }
        finally
        {
            CurrentScout = previousCurrent;
            guestRaySource = previousSource;
            Interaction.instance = previousInteraction;
            Player.localPlayer = previousPlayer;
            Character.localCharacter = previousScout;
        }
    }

    public void Dispose()
    {
        if (interaction) Object.Destroy(interaction);
    }

    public static void Install(Harmony harmony)
    {
        var transpiler = new HarmonyMethod(BuildGuard.MethodByName(typeof(SeatInteraction), nameof(RouteRaySource)));
        harmony.Patch(BuildGuard.Method(typeof(Interaction), "TestItemsStuckToMe", typeof(IInteractible).MakeByRefType()),
            transpiler: transpiler);
        harmony.Patch(BuildGuard.Method(typeof(Interaction), nameof(Interaction.DoInteractableRaycasts),
            typeof(IInteractible).MakeByRefType(), typeof(float), typeof(bool)), transpiler: transpiler);
        var characterCamera = new HarmonyMethod(BuildGuard.MethodByName(typeof(SeatInteraction), nameof(RouteCharacterRaySource)));
        harmony.Patch(BuildGuard.Method(typeof(CharacterClimbing), "TryToStartWallClimb",
            typeof(bool), typeof(Vector3), typeof(bool), typeof(float)), transpiler: characterCamera);
        harmony.Patch(BuildGuard.Method(typeof(CharacterItems), "WithinClimbingSpikePreviewRange"), transpiler: characterCamera);
        harmony.Patch(BuildGuard.Method(typeof(CharacterItems), nameof(CharacterItems.RaycastClimbingSpikeStart)), transpiler: characterCamera);
        var scoutCamera = new HarmonyMethod(BuildGuard.MethodByName(typeof(SeatInteraction), nameof(RouteExplicitScoutRaySource)));
        harmony.Patch(BuildGuard.Method(typeof(ThornOnMe), nameof(ThornOnMe.IsInteractible), typeof(Character)),
            transpiler: scoutCamera);
        harmony.Patch(BuildGuard.Method(typeof(Bugfix), nameof(Bugfix.IsInteractible), typeof(Character)),
            transpiler: scoutCamera);
        harmony.Patch(BuildGuard.Method(typeof(Item), nameof(Item.Interact), typeof(Character)),
            prefix: new HarmonyMethod(BuildGuard.MethodByName(typeof(SeatInteraction), nameof(CanTake))));
    }

    private static Transform RaySource() => guestRaySource ? guestRaySource : MainCamera.instance.transform;

    private static Transform CharacterRaySource(Component component)
    {
        var runtime = PartyRuntime.Current;
        if (runtime?.IsLocalRoom == true)
        {
            var scout = component.GetComponent<Character>();
            var camera = runtime.GetGuestCamera(scout);
            if (camera) return camera.transform;
        }
        return RaySource();
    }

    private static IEnumerable<CodeInstruction> RouteRaySource(
        IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        ReplaceRaySource(instructions, original, nameof(InteractionRaySource), OpCodes.Ldarg_0);

    private static IEnumerable<CodeInstruction> RouteCharacterRaySource(
        IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        ReplaceRaySource(instructions, original, nameof(CharacterRaySource), OpCodes.Ldarg_0);

    private static IEnumerable<CodeInstruction> RouteExplicitScoutRaySource(
        IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        ReplaceRaySource(instructions, original, nameof(ScoutRaySource), OpCodes.Ldarg_1);

    private static Transform ScoutRaySource(Character scout)
    {
        var runtime = PartyRuntime.Current;
        if (runtime?.IsLocalRoom == true && runtime.GetGuestCamera(scout) is Camera camera && camera)
            return camera.transform;
        return MainCamera.instance.transform;
    }

    private static Transform InteractionRaySource(Component component)
    {
        if (PartyRuntime.Current?.IsLocalRoom == true && component is Interaction interaction &&
            interaction.TryGetComponent<Camera>(out var camera) && camera != Camera.main)
            return camera.transform;
        return RaySource();
    }

    private static IEnumerable<CodeInstruction> ReplaceRaySource(
        IEnumerable<CodeInstruction> instructions, MethodBase original, string method, OpCode argument)
    {
        var code = new List<CodeInstruction>(instructions);
        var singleton = BuildGuard.Field(typeof(MainCamera), nameof(MainCamera.instance));
        var getTransform = AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform));
        var replacement = BuildGuard.MethodByName(typeof(SeatInteraction), method);
        var matched = false;
        for (var i = 0; i < code.Count; i++)
        {
            var instruction = code[i];
            if (i + 1 < code.Count && instruction.LoadsField(singleton) && code[i + 1].Calls(getTransform))
            {
                instruction.opcode = argument;
                instruction.operand = null;
                var oldCall = code[i + 1];
                oldCall.opcode = OpCodes.Call;
                oldCall.operand = replacement;
                matched = true;
            }
            yield return instruction;
        }
        if (!matched) throw new InvalidOperationException($"Titik kamera untuk {original.Name} berubah.");
    }

    private static bool CanTake(Item __instance) =>
        SeatHooks.Scouts?.IsActive != true || __instance.itemState == ItemState.InBackpack || __instance.gameObject.activeInHierarchy;
}
