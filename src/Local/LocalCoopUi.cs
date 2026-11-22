using System;
using System.Linq;
using UnityEngine;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class LocalCoopUi : IDisposable
{
    private readonly PartyRuntime runtime;
    private readonly ManualLogSource log;
    private readonly TitleCoopEntry title;
    private TrailLobby lobby;
    public bool LobbyOpen => lobby?.Visible == true;
    private readonly bool directLaunch;
    private bool startedDirectSession;
    private bool enteredDirectRoom;
    public Button TitleButton => title.Button;
    private int replaceSeat;
    private GUIStyle text;
    private GUIStyle prominent;
    private GUIStyle centered;
    private float scale;

    public LocalCoopUi(PartyRuntime runtime, ManualLogSource logger, bool directLaunch)
    {
        this.runtime = runtime;
        log = logger;
        this.directLaunch = directLaunch;
        title = new TitleCoopEntry(OpenLobby, logger, directLaunch);
    }

    public void OpenLobby()
    {
        if (LobbyOpen || runtime.Requested || !title.Canvas || !title.Font ||
            !GameHandler.Initialized || Peak.Network.NetworkingUtilities.EnteringRoom) return;
        if (directLaunch && !title.PrepareDirectLobby()) return;
        runtime.Party.Reset();
        lobby = new TrailLobby(title.Canvas, title.Font, title.Roster, title.Board, title.KeyboardIcon, title.GamepadIcon, CancelLobby,
            () => StartLobby(Object.FindFirstObjectByType<MainMenu>()), directLaunch ? "KELUAR" : "KEMBALI");
        lobby.Refresh(runtime.Party);
    }

    private void CloseLobby()
    {
        lobby?.Dispose();
        lobby = null;
    }

    private void CancelLobby()
    {
        CloseLobby();
        if (directLaunch) Application.Quit();
    }

    public void StartLobby(MainMenu menu)
    {
        if (!LobbyOpen || !menu || Peak.Network.NetworkingUtilities.EnteringRoom) return;
        CloseLobby();
        runtime.Begin(menu);
        startedDirectSession = directLaunch;
    }

    public void Update()
    {
        title.Update();
        var atTitle = SceneManager.GetActiveScene().name == MainMenu.SceneName;
        if (lobby != null && (!LobbyOpen || !atTitle)) CloseLobby();
        if (directLaunch)
        {
            enteredDirectRoom |= runtime.IsLocalRoom;
            if ((enteredDirectRoom && !PhotonNetwork.InRoom) ||
                (startedDirectSession && atTitle && !runtime.Requested) ||
                (GameHandler.IsInMultiplayerScene && PhotonNetwork.InRoom && !runtime.IsLocalRoom))
            {
                log.LogInfo("Sesi khusus Local Co-op selesai. Menutup proses Direct3D 11.");
                Application.Quit();
                return;
            }
            if (atTitle && !startedDirectSession) OpenLobby();
        }
        var keyboard = Keyboard.current;
        if (LobbyOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CancelLobby();
            return;
        }

        if (!LobbyOpen && !runtime.IsLocalRoom) return;
        var disconnected = runtime.Party.Seats.Skip(1)
            .Where(seat => seat.Pad == null || !seat.Pad.added).ToArray();
        if (disconnected.Length == 1) replaceSeat = disconnected[0].Number;
        else if (disconnected.All(seat => seat.Number != replaceSeat)) replaceSeat = 0;
        if (keyboard != null)
        {
            if (keyboard.f6Key.wasPressedThisFrame) ChooseReplacement(2);
            if (keyboard.f7Key.wasPressedThisFrame) ChooseReplacement(3);
            if (keyboard.f8Key.wasPressedThisFrame) ChooseReplacement(4);
        }

        foreach (var pad in Gamepad.all)
        {
            var originalSeat = disconnected.FirstOrDefault(seat => seat.Matches(pad));
            if (originalSeat != null)
            {
                runtime.AttachReplacement(originalSeat, pad);
                continue;
            }
            if (!pad.startButton.wasPressedThisFrame ||
                runtime.Party.Seats.Any(seat => seat.Matches(pad)))
                continue;
            if (replaceSeat != 0 && disconnected.FirstOrDefault(seat => seat.Number == replaceSeat) is { } seatToReplace)
                runtime.AttachReplacement(seatToReplace, pad);
            else if (LobbyOpen) runtime.Party.Add(pad);
            else if (disconnected.Length == 0) runtime.TryJoin(pad);
        }
        lobby?.Refresh(runtime.Party);
    }

    private void ChooseReplacement(int seat)
    {
        if (runtime.Party.Seats.Any(candidate => candidate.Number == seat && (candidate.Pad == null || !candidate.Pad.added)))
            replaceSeat = seat;
    }

    public void Draw()
    {
        if (!runtime.IsLocalRoom || LoadingScreenHandler.loading ||
            (!PerformancePaths.UseLegacyInput && Event.current.type != EventType.Repaint)) return;
        var requestedScale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f);
        if (requestedScale != scale || text == null)
        {
            scale = requestedScale;
            InitStyles();
        }

        var seats = runtime.Party.Seats;
        for (var i = 1; i < seats.Count; i++)
        {
            var seat = seats[i];
            var scout = runtime.GetCharacter(seat);
            if (!scout || (seat.Pad != null && seat.Pad.added && !scout.data.dead && !scout.data.fullyPassedOut))
                continue;
            var camera = runtime.GetCamera(seat.Number);
            if (camera) DrawScout(seat, scout, PanelLayout.GuiRect(camera.rect));
        }

        var line = runtime.Status;
        var checkpointHint = PerformancePaths.UseLegacyInput
            ? runtime.CanJoin && seats.Count < 4 : runtime.ShowCheckpointHint;
        if (string.IsNullOrEmpty(line) && checkpointHint)
            line = "Checkpoint · tekan Start pada controller tambahan";
        var main = runtime.GetCamera(1);
        if (main && !string.IsNullOrEmpty(line))
        {
            var panel = PanelLayout.GuiRect(main.rect);
            Label(new Rect(panel.x + 18 * scale, panel.y + 14 * scale, panel.width - 36 * scale, 32 * scale), line, prominent);
        }
    }

    private void InitStyles()
    {
        text ??= new GUIStyle(GUI.skin.label) { normal = { textColor = TrailPalette.Cream }, clipping = TextClipping.Clip };
        prominent ??= new GUIStyle(text) { fontStyle = FontStyle.Bold };
        centered ??= new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
        text.fontSize = Mathf.RoundToInt(19 * scale);
        prominent.fontSize = Mathf.RoundToInt(21 * scale);
        centered.fontSize = Mathf.RoundToInt(19 * scale);
    }

    private void DrawScout(LocalParty.Seat seat, Character scout, Rect panel)
    {
        GUI.BeginGroup(panel);
        try
        {
            if (seat.Pad == null || !seat.Pad.added)
            {
                var message = replaceSeat == seat.Number ? "Tekan Start pada controller pengganti" :
                    $"Pilih F{seat.Number + 4}, lalu tekan Start pada controller pengganti";
                Label(new Rect(14 * scale, 41 * scale, panel.width - 28 * scale, 27 * scale), message, text);
            }
            if (scout.data.dead || scout.data.fullyPassedOut)
                Label(new Rect(0, panel.height / 2, panel.width, 28 * scale), scout.data.dead ? "Tewas" : "Pingsan", centered);
        }
        finally { GUI.EndGroup(); }
    }

    private static void Label(Rect rect, string value, GUIStyle style)
    {
        var color = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.88f);
        var shadow = rect;
        shadow.position += Vector2.one;
        GUI.Label(shadow, value, style);
        GUI.color = color;
        GUI.Label(rect, value, style);
    }

    public void Dispose()
    {
        CloseLobby();
        title.Dispose();
    }
}
