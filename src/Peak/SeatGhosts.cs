using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakLocalCoop;

internal static class SeatGhosts
{
    private sealed class VisibleGhost
    {
        public Character Owner { get; }
        public Renderer[] Renderers { get; }
        public bool[] OriginalVisibility { get; }

        public VisibleGhost(PlayerGhost ghost)
        {
            Owner = ghost.m_owner;
            Renderers = ghost.GetComponentsInChildren<Renderer>(true);
            OriginalVisibility = Array.ConvertAll(Renderers, renderer => renderer.forceRenderingOff);
        }
    }

    private static readonly Dictionary<PlayerGhost, VisibleGhost> Ghosts = new();
    private static readonly Stack<Camera> Rendering = new();

    public static void Install(Harmony harmony)
    {
        harmony.Patch(BuildGuard.Method(typeof(PlayerGhost), nameof(PlayerGhost.RPCA_InitGhost), typeof(PhotonView), typeof(PhotonView)),
            transpiler: new HarmonyMethod(typeof(SeatGhosts), nameof(RouteVisibility)),
            postfix: new HarmonyMethod(typeof(SeatGhosts), nameof(Register)));
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }

    public static void Uninstall()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        ApplyVisibility(null);
        Ghosts.Clear();
        Rendering.Clear();
    }

    public static PlayerGhost Follow(Character owner, Character target, PlayerGhost ghost)
    {
        if (!owner.data.dead || !target || owner.gameOver)
        {
            Remove(owner, ghost);
            return null;
        }
        if (!ghost)
        {
            var obj = PhotonNetwork.Instantiate("PlayerGhost", target.Center + Vector3.up * 0.5f, Quaternion.identity);
            try
            {
                ghost = obj.GetComponent<PlayerGhost>();
                if (!ghost) throw new InvalidOperationException("Prefab PlayerGhost tidak memiliki komponen PEAK.");
                ghost.m_view.RPC(nameof(PlayerGhost.RPCA_InitGhost), RpcTarget.AllBuffered, owner.photonView, target.photonView);
            }
            catch
            {
                PhotonNetwork.Destroy(obj);
                throw;
            }
        }
        else if (ghost.m_target != target)
            ghost.m_view.RPC(nameof(PlayerGhost.RPCA_SetTarget), RpcTarget.AllBuffered, target.photonView);
        return ghost;
    }

    public static void Remove(Character owner, PlayerGhost ghost)
    {
        if (!ghost) return;
        Ghosts.Remove(ghost);
        if (owner && owner.Ghost == ghost) owner.Ghost = null;
        if (PhotonNetwork.InRoom && ghost.m_view && ghost.m_view.IsMine)
            PhotonNetwork.Destroy(ghost.gameObject);
        else
            UnityEngine.Object.Destroy(ghost.gameObject);
    }

    public static void Clear()
    {
        ApplyVisibility(null);
        Ghosts.Clear();
        Rendering.Clear();
    }

    private static void Register(PlayerGhost __instance)
    {
        if (SeatHooks.Scouts?.IsActive != true || !__instance.m_owner) return;
        Ghosts[__instance] = new VisibleGhost(__instance);
    }

    private static IEnumerable<CodeInstruction> RouteVisibility(IEnumerable<CodeInstruction> instructions)
    {
        var mine = AccessTools.PropertyGetter(typeof(PhotonView), nameof(PhotonView.IsMine));
        var hide = BuildGuard.Method(typeof(SeatGhosts), nameof(HideGlobally), typeof(PhotonView));
        var matched = false;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(mine))
            {
                matched = true;
                instruction.opcode = OpCodes.Call;
                instruction.operand = hide;
            }
            yield return instruction;
        }
        if (!matched) throw new InvalidOperationException("Filter visibilitas PlayerGhost berubah.");
    }

    private static bool HideGlobally(PhotonView view) => view.IsMine && SeatHooks.Scouts?.IsActive != true;

    private static void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (Ghosts.Count == 0) return;
        Rendering.Push(camera);
        ApplyVisibility(camera);
    }

    private static void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (Rendering.Count == 0 || Rendering.Peek() != camera) return;
        Rendering.Pop();
        ApplyVisibility(Rendering.Count > 0 ? Rendering.Peek() : null);
    }

    private static void ApplyVisibility(Camera camera)
    {
        var runtime = PartyRuntime.Current;
        foreach (var entry in Ghosts)
        {
            if (!entry.Key) continue;
            var visible = entry.Value;
            var owner = visible.Owner;
            var ownerCamera = owner && runtime?.IsLocalRoom == true
                ? runtime.GetGuestCamera(owner) ?? (owner == Character.localCharacter ? runtime.GetCamera(1) : null)
                : null;
            var hide = camera && ownerCamera && camera == ownerCamera;
            for (var i = 0; i < visible.Renderers.Length; i++)
            {
                var renderer = visible.Renderers[i];
                if (renderer) renderer.forceRenderingOff = hide || visible.OriginalVisibility[i];
            }
        }
    }
}
