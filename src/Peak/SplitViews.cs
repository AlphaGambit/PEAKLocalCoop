using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class SplitViews : IDisposable
{
    private sealed class GuestView : IDisposable
    {
        public Character Scout { get; }
        public Camera Camera { get; }
        public Character Spectating { get; private set; }
        public float SpectateDistance { get; private set; } = 2.5f;
        public GuestHud Hud { get; }
        public SeatInteraction Interaction { get; }
        public GuestInventory Inventory { get; }
        private readonly GameObject root;
        private float verticalFov;
        private PlayerGhost ghost;
        private float targetSpectateDistance = 2.5f;

        public GuestView(Character scout, Camera source, LocalParty.Seat seat)
        {
            Scout = scout;
            root = new GameObject($"Local Co-op P{seat.Number} camera");
            root.SetActive(false);
            try
            {
                Camera = root.AddComponent<Camera>();
                Camera.CopyFrom(source);
                Camera.targetTexture = null;
                var sourceData = source.GetUniversalAdditionalCameraData();
                var data = Camera.GetUniversalAdditionalCameraData();
                data.SetRenderer((int)RendererIndex.GetValue(sourceData));
                data.renderPostProcessing = sourceData.renderPostProcessing;
                data.antialiasing = sourceData.antialiasing;
                data.antialiasingQuality = sourceData.antialiasingQuality;
                data.renderShadows = sourceData.renderShadows;
                data.volumeLayerMask = sourceData.volumeLayerMask;
                root.SetActive(true);
                Interaction = new SeatInteraction(Camera);
                Inventory = new GuestInventory(scout);
                Hud = new GuestHud(seat, scout, Camera, Inventory);
                Follow();
            }
            catch
            {
                Hud?.Dispose();
                Interaction?.Dispose();
                root.SetActive(false);
                Object.Destroy(root);
                throw;
            }
        }

        public void Follow(MainCameraMovement movement = null)
        {
            var direction = Scout.data.lookDirection;
            var position = GetCameraPosition(Scout, -0.5f);
            if (Scout.data.dead || Scout.data.fullyPassedOut)
            {
                var candidates = PlayerHandler.GetAllPlayerCharacters()
                    .Where(player => player && !player.data.dead && !player.data.fullyPassedOut).ToArray();
                if (candidates.Length > 0)
                {
                    var selected = Array.IndexOf(candidates, Spectating);
                    if (selected < 0) selected = 0;
                    else if (Scout.input.spectateRightWasPressed) selected = (selected + 1) % candidates.Length;
                    else if (Scout.input.spectateLeftWasPressed) selected = (selected + candidates.Length - 1) % candidates.Length;
                    Spectating = candidates[selected];
                    if (direction.sqrMagnitude < 0.0001f) direction = Spectating.data.lookDirection;
                    if (movement)
                    {
                        var input = (Scout.input.scrollBackwardIsPressed ? 1 : 0) - (Scout.input.scrollForwardIsPressed ? 1 : 0);
                        targetSpectateDistance = Mathf.Clamp(targetSpectateDistance +
                            input * Time.deltaTime * movement.spectateZoomButtonSpeed,
                            movement.spectateZoomMin, movement.spectateZoomMax);
                        SpectateDistance = Mathf.Lerp(SpectateDistance, targetSpectateDistance, Time.deltaTime * 5f);
                    }
                    Scout.data.spectateZoom = SpectateDistance;
                    position = Spectating.GetSpectatePosition() + Vector3.up * 0.5f - direction * SpectateDistance;
                }
                else
                {
                    Spectating = null;
                    position = Scout.LastLivingPosition - direction * SpectateDistance;
                }
            }
            else
            {
                Spectating = null;
                targetSpectateDistance = SpectateDistance = 2.5f;
            }
            ghost = SeatGhosts.Follow(Scout, Spectating, ghost);
            if (direction.sqrMagnitude < 0.0001f) direction = Scout.transform.forward;
            Camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        }

        public void UpdateFov(float target)
        {
            verticalFov = verticalFov > 0 ? Mathf.Lerp(verticalFov, target, Time.deltaTime * 5f) : target;
            Camera.fieldOfView = PanelFieldOfView(Camera, verticalFov);
        }

        public void Dispose()
        {
            if (!root) return;
            SeatGhosts.Remove(Scout, ghost);
            if (Camera) Camera.enabled = false;
            Hud?.Dispose();
            Inventory?.Dispose();
            Interaction?.Dispose();
            root.SetActive(false);
            Object.Destroy(root);
        }
    }

    private static readonly Func<Character, float, Vector3> GetCameraPosition =
        AccessTools.MethodDelegate<Func<Character, float, Vector3>>(
            BuildGuard.Method(typeof(Character), "GetCameraPos", typeof(float)));
    private static readonly System.Reflection.FieldInfo RendererIndex =
        BuildGuard.Field(typeof(UniversalAdditionalCameraData), "m_RendererIndex");

    private readonly SortedDictionary<int, GuestView> guests = new SortedDictionary<int, GuestView>();
    private readonly Camera primary;
    private readonly Rect originalRect;
    private readonly float originalFov;
    private readonly Canvas hud;
    private readonly RenderMode originalHudMode;
    private readonly Camera originalHudCamera;
    private readonly float originalHudDistance;

    public SplitViews()
    {
        primary = MainCamera.instance ? MainCamera.instance.GetComponent<Camera>() : null;
        if (!primary) throw new InvalidOperationException("MainCamera PEAK belum siap.");
        originalRect = primary.rect;
        originalFov = primary.fieldOfView;
        hud = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(canvas => canvas.name == "Canvas_HUD" && canvas.gameObject.scene == primary.gameObject.scene);
        if (hud)
        {
            originalHudMode = hud.renderMode;
            originalHudCamera = hud.worldCamera;
            originalHudDistance = hud.planeDistance;
            hud.renderMode = RenderMode.ScreenSpaceCamera;
            hud.worldCamera = primary;
            hud.planeDistance = Mathf.Max(primary.nearClipPlane + 0.01f, 0.2f);
        }
    }

    public void Add(LocalParty.Seat seat, Character scout)
    {
        if (guests.ContainsKey(seat.Number)) throw new InvalidOperationException($"Viewport P{seat.Number} sudah ada.");
        var view = new GuestView(scout, primary, seat);
        guests.Add(seat.Number, view);
        Arrange();
    }

    private void Arrange()
    {
        var count = guests.Count + 1;
        if (primary) primary.rect = PanelLayout.Viewport(count, 0);
        var index = 1;
        foreach (var view in guests.Values)
        {
            if (!view.Camera) continue;
            view.Camera.rect = PanelLayout.Viewport(count, index++);
            view.Camera.depth = primary.depth + index;
        }
    }

    public void Tick()
    {
        if (!primary) return;
        var verticalFov = primary.fieldOfView;
        var baseFov = originalFov;
        var climbingFov = 0f;
        if (primary.TryGetComponent<MainCameraMovement>(out var movement) && movement.isActiveAndEnabled)
        {
            var current = (float)MainCameraFov.GetValue(movement);
            if (current >= 10f) verticalFov = current;
            var configured = Fov(movement)?.Value ?? baseFov;
            baseFov = configured < 60f ? 70f : configured;
            climbingFov = ClimbFov(movement)?.Value ?? 0f;
        }
        primary.fieldOfView = PanelFieldOfView(primary, verticalFov);
        foreach (var view in guests.Values)
        {
            if (!view.Scout || !view.Camera) continue;
            view.UpdateFov(baseFov + (view.Scout.data.isClimbing ? climbingFov : 0f));
            view.Follow(movement);
            view.Interaction.Tick(view.Scout, view.Camera);
            view.Hud.Tick(view.Interaction.Hovered);
        }
    }

    private static readonly AccessTools.FieldRef<MainCameraMovement, FovSetting> Fov =
        AccessTools.FieldRefAccess<MainCameraMovement, FovSetting>(BuildGuard.Field(typeof(MainCameraMovement), "fovSetting"));
    private static readonly AccessTools.FieldRef<MainCameraMovement, ExtraFovSetting> ClimbFov =
        AccessTools.FieldRefAccess<MainCameraMovement, ExtraFovSetting>(BuildGuard.Field(typeof(MainCameraMovement), "extraFovSetting"));

    private static readonly System.Reflection.FieldInfo MainCameraFov =
        BuildGuard.Field(typeof(MainCameraMovement), "currentFov");

    private static float PanelFieldOfView(Camera camera, float vertical)
    {
        var aspect = camera.pixelWidth / (float)Mathf.Max(1, camera.pixelHeight);
        var normalHorizontal = Camera.VerticalToHorizontalFieldOfView(vertical, 16f / 9f);
        return Mathf.Clamp(Camera.HorizontalToVerticalFieldOfView(normalHorizontal, aspect), 35f, vertical);
    }

    public (Character Target, float Distance) Spectating(int seat) =>
        guests.TryGetValue(seat, out var view) ? (view.Spectating, view.SpectateDistance) : default;

    public GuestInventory Inventory(int seat) => guests.TryGetValue(seat, out var view) ? view.Inventory : null;

    public Camera GetCamera(int seat) => seat == 1 ? primary : guests.TryGetValue(seat, out var view) ? view.Camera : null;

    public Interaction GetInteraction(int seat) => guests.TryGetValue(seat, out var view) ? view.Interaction.Native : null;

    public IInteractible Hovered(int seat) => guests.TryGetValue(seat, out var view) ? view.Interaction.Hovered : null;

    public void Dispose()
    {
        foreach (var view in guests.Values) view.Dispose();
        guests.Clear();
        if (primary)
        {
            primary.rect = originalRect;
            primary.fieldOfView = originalFov;
        }
        if (hud)
        {
            hud.renderMode = originalHudMode;
            hud.worldCamera = originalHudCamera;
            hud.planeDistance = originalHudDistance;
        }
    }
}
