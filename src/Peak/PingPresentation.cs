using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakLocalCoop;

internal static class PingPresentation
{
    private readonly struct Pose
    {
        private readonly Transform transform;
        private readonly Vector3 scale;
        private readonly Quaternion rotation;

        public Pose(Transform transform)
        {
            this.transform = transform;
            scale = transform.localScale;
            rotation = transform.rotation;
        }

        public void Restore()
        {
            if (!transform) return;
            transform.localScale = scale;
            transform.rotation = rotation;
        }
    }

    private static readonly Action<PointPing> Draw = AccessTools.MethodDelegate<Action<PointPing>>(
        BuildGuard.Method(typeof(PointPing), "Go"));
    private static readonly HashSet<PointPing> Markers = new();
    private static readonly Stack<(Camera Camera, int FirstPose)> Rendering = new();
    private static readonly List<Pose> Previous = new();
    private static Camera viewingCamera;
    private static Character viewingScout;

    public static void Install(Harmony harmony)
    {
        harmony.Patch(BuildGuard.Method(typeof(PointPing), "Start"),
            postfix: new HarmonyMethod(typeof(PingPresentation), nameof(Register)));
        harmony.Patch(BuildGuard.Method(typeof(PointPing), "Go"),
            transpiler: new HarmonyMethod(typeof(PingPresentation), nameof(RouteViewer)));
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }

    public static void Uninstall()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        Clear();
    }

    public static void Clear()
    {
        RestoreFrom(0);
        Rendering.Clear();
        Markers.Clear();
    }

    private static void Register(PointPing __instance)
    {
        if (PartyRuntime.Current?.IsLocalRoom == true) Markers.Add(__instance);
    }

    private static void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (Markers.Count == 0) return;
        Markers.RemoveWhere(static marker => !marker);
        var runtime = PartyRuntime.Current;
        if (runtime?.IsLocalRoom != true) return;
        var viewer = runtime.GetViewingScout(camera);
        if (!viewer) return;
        var start = Previous.Count;
        viewingCamera = camera;
        viewingScout = viewer;
        try
        {
            foreach (var marker in Markers)
            {
                if (!marker.isActiveAndEnabled) continue;
                Previous.Add(new Pose(marker.transform));
                Draw(marker);
            }
        }
        catch
        {
            RestoreFrom(start);
            throw;
        }
        finally
        {
            viewingCamera = null;
            viewingScout = null;
        }
        Rendering.Push((camera, start));
    }

    private static void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (Rendering.Count != 0 && Rendering.Peek().Camera == camera)
            RestoreFrom(Rendering.Pop().FirstPose);
    }

    private static void RestoreFrom(int start)
    {
        for (var i = Previous.Count - 1; i >= start; i--) Previous[i].Restore();
        Previous.RemoveRange(start, Previous.Count - start);
    }

    private static Camera UseCamera(Camera original) => viewingCamera ? viewingCamera : original;
    private static Character UseViewer() => viewingScout ? viewingScout : Character.localCharacter;

    private static IEnumerable<CodeInstruction> RouteViewer(IEnumerable<CodeInstruction> instructions)
    {
        var camera = BuildGuard.Field(typeof(PointPing), "camera");
        var local = BuildGuard.Field(typeof(Character), nameof(Character.localCharacter));
        var cameraCalls = 0;
        var scoutCalls = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(local))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = BuildGuard.Method(typeof(PingPresentation), nameof(UseViewer));
                scoutCalls++;
            }
            yield return instruction;
            if (instruction.LoadsField(camera))
            {
                yield return new CodeInstruction(OpCodes.Call, BuildGuard.Method(typeof(PingPresentation), nameof(UseCamera), typeof(Camera)));
                cameraCalls++;
            }
        }
        if (cameraCalls != 2 || scoutCalls != 1)
            throw new InvalidOperationException("Jalur visual marker ping PEAK berubah.");
    }
}
