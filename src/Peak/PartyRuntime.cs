using System;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Peak;
using Peak.Network;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Zorro.ControllerSupport;
using NetworkRoom = Photon.Realtime.Room;

namespace PeakLocalCoop;

internal sealed class PartyRuntime : IDisposable
{
    public static PartyRuntime Current { get; private set; }
    public LocalParty Party { get; } = new LocalParty();
    public CheckpointGate Checkpoint { get; private set; } = new CheckpointGate();
    public bool Requested { get; private set; }
    public string Status { get; private set; }
    public bool ShowCheckpointHint { get; private set; }
    public bool IsLocalRoom => Requested && PhotonNetwork.OfflineMode && PhotonNetwork.InRoom
        && (ownRoom == null || ReferenceEquals(ownRoom, PhotonNetwork.CurrentRoom));

    private readonly ManualLogSource log;
    private readonly OfflineScouts scouts;
    private NetworkRoom ownRoom;
    private Character host;
    private SplitViews views;
    private HostMenuInput hostMenu;
    private bool bindingFailed;

    public PartyRuntime(ManualLogSource logger, OfflineScouts adapter)
    {
        log = logger;
        scouts = adapter;
        Party.Reset();
        Current = this;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    public void Begin(MainMenu menu)
    {
        if (Requested || !menu || !GameHandler.Initialized || NetworkingUtilities.EnteringRoom) return;
        Requested = true;
        bindingFailed = false;
        Status = null;
        try
        {
            BuildGuard.Method(typeof(MainMenu), "StartSoloSession").Invoke(menu, null);
        }
        catch (Exception exception)
        {
            Requested = false;
            Fail(exception);
        }
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        Unbind();
        Checkpoint = new CheckpointGate();
        if (scene.name == MainMenu.SceneName && Requested)
        {
            Requested = false;
            ownRoom = null;
            Party.Reset();
            bindingFailed = false;
        }
    }

    public void Tick()
    {
        if (!Requested || bindingFailed || LoadingScreenHandler.loading) return;
        if (ownRoom != null && !IsLocalRoom)
        {
            Unbind();
            ownRoom = null;
            Requested = false;
            Party.Reset();
            return;
        }
        if (!IsLocalRoom || !Character.localCharacter || !Player.localPlayer ||
            !GameHandler.IsInMultiplayerScene || !MainCamera.instance || !Interaction.instance ||
            (GameHandler.IsInGameplayScene && !GameHandler.IsOnIslandAndInitialized))
            return;

        if (host != Character.localCharacter)
        {
            try
            {
                BindScene();
            }
            catch (Exception error)
            {
                Unbind();
                Fail(error);
                return;
            }
        }
        ShowCheckpointHint = Checkpoint.Available() && Party.Seats.Count < 4;
        foreach (var seat in Party.Seats.Skip(1))
        {
            var guest = GetScout(seat);
            var actions = guest == null ? null : SeatHooks.ActionsFor(guest.Character);
            if (actions != null && actions.Connected != (seat.Pad != null && seat.Pad.added))
                actions.Pair(seat.Pad);
            views?.Inventory(seat.Number)?.Tick(seat.Pad, actions?.BackpackPressed == true);
        }
    }

    private void BindScene()
    {
        Unbind();
        ownRoom = PhotonNetwork.CurrentRoom;
        scouts.SetRoom(ownRoom);
        host = Character.localCharacter;
        SeatHooks.Add(host, new SeatActions(InputScheme.KeyboardMouse, Keyboard.current, Mouse.current), host: true);
        hostMenu = new HostMenuInput();
        if (Party.Seats.Count > 1) views = new SplitViews();
        foreach (var seat in Party.Seats.Skip(1))
        {
            var spawn = SpawnPoint.GetSpawnPoint(seat.Number);
            var position = spawn.transform.position;
            if (spawn == SpawnPoint.LocalSpawnPoint)
                position += spawn.transform.right * ((seat.Number - 1) * 1.55f);
            SpawnGuest(seat, spawn.transform, position, spawn.startPassedOut && GameHandler.IsOnIsland);
        }
        log.LogInfo($"Local Co-op di {host.gameObject.scene.name}. {Party.Seats.Count} kursi, Unity {Application.unityVersion}, {Screen.width}×{Screen.height}, {SystemInfo.graphicsDeviceName}.");
    }

    private void SpawnGuest(LocalParty.Seat seat, Transform anchor, Vector3 position, bool beachIntro)
    {
        var scout = scouts.Create(seat.Number, seat.Id, anchor, position);
        try
        {
            SeatHooks.Add(scout.Character, new SeatActions(InputScheme.Gamepad, seat.Pad));
            if (beachIntro) BuildGuard.Method(typeof(Character), "StartPassedOutOnTheBeach").Invoke(scout.Character, null);
            views.Add(seat, scout.Character);
        }
        catch
        {
            SeatHooks.Remove(scout.Character);
            scouts.Remove(scout);
            throw;
        }
    }

    public bool CanJoin => host && IsLocalRoom && !bindingFailed && !LoadingScreenHandler.loading
        && Checkpoint.Available();

    public bool TryJoin(Gamepad pad)
    {
        if (!CanJoin) return false;
        var campfire = Checkpoint.Available();
        var seat = Party.Add(pad);
        if (seat == null) return false;
        var newViews = views == null;
        try
        {
            var position = CheckpointGate.FindSpawn(campfire, seat.Number);
            if (newViews) views = new SplitViews();
            SpawnGuest(seat, campfire.transform, position, beachIntro: false);
            Status = null;
            log.LogInfo($"P{seat.Number} bergabung di checkpoint {MapHandler.CurrentSegmentNumber}. ID karakter {seat.Id:N}.");
            return true;
        }
        catch (Exception error)
        {
            Party.RemoveLast(seat);
            if (newViews)
            {
                views?.Dispose();
                views = null;
            }
            Status = $"Gagal bergabung. {error.GetBaseException().Message}";
            log.LogWarning($"Join P{seat.Number} dibatalkan. {error}");
            return false;
        }
    }

    private OfflineScouts.Scout GetScout(LocalParty.Seat seat) =>
        scouts.Guests.FirstOrDefault(scout => scout.Id == seat.Id);

    public Character GetCharacter(LocalParty.Seat seat) => seat.Number == 1 ? host : GetScout(seat)?.Character;
    public Camera GetCamera(int seat) =>
        views?.GetCamera(seat) ?? (seat == 1 && MainCamera.instance ? MainCamera.instance.GetComponent<Camera>() : null);
    public Character GetViewingScout(Camera camera)
    {
        if (camera == GetCamera(1)) return host;
        foreach (var guest in scouts.Guests)
            if (GetCamera(guest.Actor.ActorNumber) == camera) return guest.Character;
        return null;
    }

    public Camera GetGuestCamera(Character scout) =>
        scouts.Find(scout) is { } guest ? GetCamera(guest.Actor.ActorNumber) : null;

    public Interaction GuestInteraction(Character scout) => views?.GetInteraction(scout.photonView.OwnerActorNr);

    public GuestInventory InventoryFor(int seat) => views?.Inventory(seat);

    public GuestInventory InventoryFor(Character scout) =>
        !scout ? null : views?.Inventory(scout.photonView.OwnerActorNr);

    public (Character Target, float Distance) Spectating(int seat) => views?.Spectating(seat) ?? default;

    public IInteractible Hovered(int seat) => views?.Hovered(seat);

    public void AttachReplacement(LocalParty.Seat seat, Gamepad pad)
    {
        if (!Party.AttachReplacement(seat, pad)) return;
        var scout = GetScout(seat);
        if (scout != null) SeatHooks.ActionsFor(scout.Character)?.Pair(pad);
        Status = null;
        log.LogInfo($"Controller P{seat.Number} tersambung kembali.");
    }

    public void LateTick()
    {
        if (!IsLocalRoom || !host || bindingFailed || LoadingScreenHandler.loading) return;
        try
        {
            views?.Tick();
        }
        catch (Exception error)
        {
            Unbind();
            Fail(error);
        }
    }

    private void Fail(Exception error)
    {
        bindingFailed = true;
        Status = $"Local Co-op dinonaktifkan untuk sesi ini. {error.GetBaseException().Message}";
        log.LogError($"{Status}\n{error}");
    }

    private void Unbind()
    {
        views?.Dispose();
        views = null;
        SeatGhosts.Clear();
        PingPresentation.Clear();
        MirrorPresentation.Clear();
        hostMenu?.Dispose();
        hostMenu = null;
        SeatHooks.Clear();
        scouts.Clear();
        host = null;
        ShowCheckpointHint = false;
    }

    public void Dispose()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        Unbind();
        if (Current == this) Current = null;
    }

    public static void Install(Harmony harmony)
    {
        var prefix = new HarmonyMethod(BuildGuard.MethodByName(typeof(PartyRuntime), nameof(AllowVanillaSave)));
        harmony.Patch(BuildGuard.Method(typeof(Quicksave), nameof(Quicksave.SaveNow)), prefix: prefix);
        harmony.Patch(BuildGuard.Method(typeof(Quicksave), nameof(Quicksave.DestroySaveData)), prefix: prefix);
    }

    private static bool AllowVanillaSave() => Current?.IsLocalRoom != true;
}
