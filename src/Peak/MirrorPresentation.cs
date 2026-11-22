using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PeakLocalCoop;

internal static class MirrorPresentation
{
    private static readonly AccessTools.FieldRef<Mirror, Camera> Source =
        AccessTools.FieldRefAccess<Mirror, Camera>(BuildGuard.Field(typeof(Mirror), "mainCam"));
    internal static readonly AccessTools.FieldRef<Mirror, Camera> Reflection =
        AccessTools.FieldRefAccess<Mirror, Camera>(BuildGuard.Field(typeof(Mirror), "mirrorCamera"));
    internal static readonly AccessTools.FieldRef<Mirror, Transform> Surface =
        AccessTools.FieldRefAccess<Mirror, Transform>(BuildGuard.Field(typeof(Mirror), "mirrorTransform"));
    private static readonly AccessTools.FieldRef<Mirror, bool> Initialized =
        AccessTools.FieldRefAccess<Mirror, bool>(BuildGuard.Field(typeof(Mirror), "isInitialized"));
    private static readonly Action<Mirror> UpdateReflection = AccessTools.MethodDelegate<Action<Mirror>>(
        BuildGuard.Method(typeof(Mirror), "LateUpdate"));
    private static readonly Dictionary<Mirror, (Camera Camera, Renderer Surface)> Mirrors = new();
    private static readonly Plane[] Frustum = new Plane[6];
    private static bool updatingReflection;

    public static void Install(Harmony harmony)
    {
        harmony.Patch(BuildGuard.Method(typeof(Mirror), "LateUpdate"),
            prefix: new HarmonyMethod(typeof(MirrorPresentation), nameof(UsePanelRendering)));
        RenderPipelineManager.beginCameraRendering += RenderForPanel;
    }

    public static void Uninstall()
    {
        RenderPipelineManager.beginCameraRendering -= RenderForPanel;
        Clear();
    }

    public static void Clear()
    {
        foreach (var mirror in Mirrors.Values)
            if (mirror.Camera) mirror.Camera.enabled = true;
        Mirrors.Clear();
    }

    private static bool UsePanelRendering(Mirror __instance)
    {
        if (updatingReflection || PartyRuntime.Current?.IsLocalRoom != true ||
            PartyRuntime.Current.GetCamera(2) == null || !Initialized(__instance))
            return true;
        if (!Mirrors.ContainsKey(__instance))
        {
            var reflection = Reflection(__instance);
            var surface = Surface(__instance);
            if (!reflection || !reflection.enabled || !reflection.targetTexture ||
                !surface || !surface.TryGetComponent<Renderer>(out var renderer))
                return true;
            Mirrors.Add(__instance, (reflection, renderer));
            reflection.enabled = false;
        }
        return false;
    }

    private static void RenderForPanel(ScriptableRenderContext context, Camera panel)
    {
        if (Mirrors.Count == 0 || GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset) return;
        var runtime = PartyRuntime.Current;
        if (runtime?.IsLocalRoom != true || !runtime.GetViewingScout(panel)) return;
        GeometryUtility.CalculateFrustumPlanes(panel, Frustum);
        foreach (var pair in Mirrors)
        {
            var mirror = pair.Key;
            var (reflection, surface) = pair.Value;
            if (!mirror || !mirror.isActiveAndEnabled || !reflection || !surface || !surface.enabled ||
                (panel.cullingMask & (1 << surface.gameObject.layer)) == 0 ||
                !GeometryUtility.TestPlanesAABB(Frustum, surface.bounds))
                continue;

            var previous = Source(mirror);
            try
            {
                Source(mirror) = panel;
                updatingReflection = true;
                UpdateReflection(mirror);
                // Each panel samples the native texture after its own reflection has been drawn.
#pragma warning disable CS0618 // Reuse the active URP context and avoid recursive camera callbacks.
                UniversalRenderPipeline.RenderSingleCamera(context, reflection);
#pragma warning restore CS0618
            }
            finally
            {
                updatingReflection = false;
                Source(mirror) = previous;
            }
        }
    }
}
