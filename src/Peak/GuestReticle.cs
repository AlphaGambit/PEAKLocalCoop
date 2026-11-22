using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class GuestReticle : IDisposable
{
    private static readonly Action<GUIManager> UpdateNative = AccessTools.MethodDelegate<Action<GUIManager>>(
        BuildGuard.Method(typeof(GUIManager), "UpdateReticle"));
    private static readonly Action<GUIManager> UpdateThrow = AccessTools.MethodDelegate<Action<GUIManager>>(
        BuildGuard.Method(typeof(GUIManager), "UpdateThrow"));
    private static readonly AccessTools.FieldRef<GUIManager, Character> Observed = Field<Character>("character");
    private static readonly AccessTools.FieldRef<GUIManager, GameObject> Last = Field<GameObject>("lastReticle");
    private static readonly AccessTools.FieldRef<GUIManager, float> Lock = Field<float>("reticleLock");
    private static readonly AccessTools.FieldRef<GUIManager, List<GameObject>> List = Field<List<GameObject>>("reticleList");
    private static readonly string[] NativeNames =
    {
        "reticleDefault", "reticleX", "reticleClimb", "reticleClimbJump", "reticleThrow", "reticleReach",
        "reticleGrasp", "reticleSpike", "reticleRope", "reticleClimbTry", "reticleVine", "reticleBoost",
        "reticleShoot", "reticleKick", "reticleLadder"
    };
    private static readonly AccessTools.FieldRef<GUIManager, GameObject>[] Variants =
        Array.ConvertAll(NativeNames, Field<GameObject>);
    private static readonly Dictionary<Character, GuestReticle> Active = new();
    private readonly List<GameObject> clones = new();
    private readonly GameObject[] originalVariants = new GameObject[Variants.Length];
    private readonly GameObject root;
    private readonly GameObject guestEmote;
    private readonly Image dot;
    private readonly Image shoot;
    private readonly GameObject throwIndicator;
    private readonly Image throwMeter;
    private static readonly Action<GUIManager> UpdateIndicators = gui =>
    {
        UpdateNative(gui);
        UpdateThrow(gui);
    };
    private readonly Character scout;
    private readonly GUIManager manager;
    private GameObject last;
    private float remainingLock;

    private static AccessTools.FieldRef<GUIManager, T> Field<T>(string name) =>
        AccessTools.FieldRefAccess<GUIManager, T>(BuildGuard.Field(typeof(GUIManager), name));

    public GuestReticle(GUIManager manager, Character scout, Transform parent, GameObject emoteWheel)
    {
        this.manager = manager;
        this.scout = scout;
        var template = manager.reticleDefault.transform.parent;
        root = Object.Instantiate(template.gameObject, parent, false);
        root.name = "Native reticle";
        root.SetActive(false);
        foreach (var field in Variants)
        {
            var variant = FindClone(field(manager).transform, template, root.transform).gameObject;
            variant.SetActive(false);
            clones.Add(variant);
        }
        dot = FindClone(manager.reticleDefaultImage.transform, template, root.transform).GetComponent<Image>();
        shoot = FindClone(manager.reticleShootImage.transform, template, root.transform).GetComponent<Image>();
        if (manager.throwGO.transform.IsChildOf(template))
        {
            throwIndicator = FindClone(manager.throwGO.transform, template, root.transform).gameObject;
            throwMeter = FindClone(manager.throwBar.transform, template, root.transform).GetComponent<Image>();
        }
        else
        {
            throwIndicator = Object.Instantiate(manager.throwGO, root.transform, false);
            throwMeter = FindClone(manager.throwBar.transform, manager.throwGO.transform, throwIndicator.transform)
                .GetComponent<Image>();
        }
        throwIndicator.SetActive(false);
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        guestEmote = emoteWheel;
        Active.Add(scout, this);
    }

    internal static Transform FindClone(Transform original, Transform template, Transform copy)
    {
        if (original == template) return copy;
        if (!original.parent)
            throw new InvalidOperationException($"Hierarki bidikan PEAK berubah pada {original.name}.");
        return FindClone(original.parent, template, copy).GetChild(original.GetSiblingIndex());
    }

    public void Tick(bool visible)
    {
        root.SetActive(visible);
        if (visible) DrawForGuest(UpdateIndicators);
    }

    private void DrawForGuest(Action<GUIManager> update)
    {
        var previousLocal = Character.localCharacter;
        var observed = Observed(manager);
        var lastReticle = Last(manager);
        var locked = Lock(manager);
        var list = List(manager);
        var defaultImage = manager.reticleDefaultImage;
        var shootImage = manager.reticleShootImage;
        var emote = manager.emoteWheel;
        var previousThrowIndicator = manager.throwGO;
        var previousThrowMeter = manager.throwBar;
        for (var i = 0; i < Variants.Length; i++)
        {
            originalVariants[i] = Variants[i](manager);
            Variants[i](manager) = clones[i];
        }
        Character.localCharacter = scout;
        Observed(manager) = scout;
        List(manager) = clones;
        Last(manager) = last;
        Lock(manager) = remainingLock;
        manager.reticleDefaultImage = dot;
        manager.reticleShootImage = shoot;
        manager.emoteWheel = guestEmote;
        manager.throwGO = throwIndicator;
        manager.throwBar = throwMeter;
        try
        {
            update(manager);
        }
        finally
        {
            last = Last(manager);
            remainingLock = Lock(manager);
            manager.emoteWheel = emote;
            manager.throwGO = previousThrowIndicator;
            manager.throwBar = previousThrowMeter;
            manager.reticleShootImage = shootImage;
            manager.reticleDefaultImage = defaultImage;
            List(manager) = list;
            Last(manager) = lastReticle;
            Lock(manager) = locked;
            Observed(manager) = observed;
            Character.localCharacter = previousLocal;
            for (var i = 0; i < Variants.Length; i++) Variants[i](manager) = originalVariants[i];
        }
    }

    public static void Install(Harmony harmony)
    {
        var transpiler = new HarmonyMethod(typeof(GuestReticle), nameof(RouteEvents));
        harmony.Patch(BuildGuard.Method(typeof(CharacterMovement), "Land"), transpiler: transpiler);
        harmony.Patch(BuildGuard.Method(typeof(CharacterClimbing), "RPCA_ClimbJump", typeof(Photon.Pun.PhotonMessageInfo)), transpiler: transpiler);
        harmony.Patch(BuildGuard.Method(typeof(CharacterGrabbing), "Reach"), transpiler: transpiler);
        harmony.Patch(BuildGuard.Method(typeof(CharacterGrabbing), "EnviroGrab", typeof(GameObject), typeof(float)), transpiler: transpiler);
    }

    private static IEnumerable<CodeInstruction> RouteEvents(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo method &&
                method.DeclaringType == typeof(GUIManager) &&
                method.Name is nameof(GUIManager.Grasp) or nameof(GUIManager.ReticleLand) or nameof(GUIManager.ClimbJump))
            {
                var loadOwner = new CodeInstruction(OpCodes.Ldarg_0);
                instruction.MoveLabelsTo(loadOwner);
                yield return loadOwner;
                instruction.opcode = OpCodes.Call;
                instruction.operand = BuildGuard.Method(typeof(GuestReticle), method.Name, typeof(GUIManager), typeof(Component));
            }
            yield return instruction;
        }
    }

    private static void Grasp(GUIManager gui, Component owner) => Route(gui, owner, static m => m.Grasp());
    private static void ReticleLand(GUIManager gui, Component owner) => Route(gui, owner, static m => m.ReticleLand());
    private static void ClimbJump(GUIManager gui, Component owner) => Route(gui, owner, static m => m.ClimbJump());

    private static void Route(GUIManager gui, Component owner, Action<GUIManager> show)
    {
        var character = owner.GetComponent<Character>();
        if (character && Active.TryGetValue(character, out var guest))
        {
            if (guest.root.activeInHierarchy) guest.DrawForGuest(show);
        }
        else show(gui);
    }

    public void Dispose()
    {
        Active.Remove(scout);
        if (root) root.SetActive(false);
    }
}
