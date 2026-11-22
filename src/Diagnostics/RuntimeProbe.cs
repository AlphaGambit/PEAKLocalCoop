using System;
using System.Linq;
using BepInEx.Logging;
using Peak.Network;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Zorro.ControllerSupport;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class RuntimeProbe
{
    private readonly ManualLogSource log;
    private readonly PartyRuntime runtime;
    private readonly OfflineScouts scouts;
    private readonly LocalCoopUi ui;

    public RuntimeProbe(ManualLogSource log, PartyRuntime runtime, OfflineScouts scouts, LocalCoopUi ui)
    {
        this.log = log;
        this.runtime = runtime;
        this.scouts = scouts;
        this.ui = ui;
    }

    public System.Collections.IEnumerator Proof()
    {
        while (SceneManager.GetActiveScene().name != MainMenu.SceneName ||
               !Object.FindFirstObjectByType<MainMenu>() || !GameHandler.Instance)
            yield return null;
        yield return new WaitForSecondsRealtime(2f);
        log.LogInfo("PROBE: entering the stock offline solo flow.");
        BuildGuard.Method(typeof(MainMenu), "StartSoloSession").Invoke(Object.FindFirstObjectByType<MainMenu>(), null);
        while (!PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom || !Character.localCharacter ||
               !Player.localPlayer || LoadingScreenHandler.loading || SceneManager.GetActiveScene().name != "Airport")
            yield return null;
        yield return new WaitForSecondsRealtime(2f);

        var p1 = Character.localCharacter;
        var p1Inventory = Player.localPlayer;
        var pad = InputSystem.AddDevice<Gamepad>();
        var asset = InputSystem.actions;
        try
        {
            scouts.SetRoom(PhotonNetwork.CurrentRoom);
            var anchor = SpawnPoint.LocalSpawnPoint.transform;
            var guest = scouts.Create(2, Guid.NewGuid(), anchor, p1.Center + p1.transform.right * 1.5f + Vector3.up * 0.2f);
            SeatHooks.Add(p1, new SeatActions(InputScheme.KeyboardMouse, Keyboard.current, Mouse.current), host: true);
            SeatHooks.Add(guest.Character, new SeatActions(InputScheme.Gamepad, pad));
            var p2 = guest.Character;
            log.LogInfo($"PROBE: P1 view {p1.photonView.ViewID}, P2 view {p2.photonView.ViewID}, room actors {PhotonNetwork.CurrentRoom.PlayerCount}, world {p1.gameObject.scene.name}/{p2.gameObject.scene.name}, pipeline {GraphicsSettings.currentRenderPipeline?.GetType().Name}.");
            yield return new WaitForSecondsRealtime(2f);

            var p1Origin = p1.Center;
            var p2Origin = p2.Center;
            var p1Look = p1.data.lookValues;
            var p2Look = p2.data.lookValues;
            var p2Moved = 0f;
            var p2SampledMovement = false;
            foreach (var direction in new[] { Vector2.up, Vector2.left, Vector2.down })
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = direction });
                yield return new WaitForSecondsRealtime(0.7f);
                p2SampledMovement |= p2.input.movementInput.sqrMagnitude > 0.5f;
                p2Moved = Mathf.Max(p2Moved, Vector3.Distance(p2Origin, p2.Center));
                if (p2Moved > 0.25f) break;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.right * 0.6f });
            yield return new WaitForSecondsRealtime(0.35f);
            var p1Moved = Vector3.Distance(p1Origin, p1.Center);
            var p1LookDelta = Vector2.Distance(p1Look, p1.data.lookValues);
            var p2LookDelta = Vector2.Distance(p2Look, p2.data.lookValues);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            var isolated = p1 == Character.localCharacter && p1Inventory == Player.localPlayer
                && p1.player != p2.player && p1.gameObject.scene == p2.gameObject.scene
                && PlayerHandler.GetAllPlayerCharacters().Contains(p2)
                && p1.input.movementInput.sqrMagnitude < 0.001f && p1LookDelta < 0.01f
                && p1Moved < 0.20f && p2SampledMovement && p2Moved > 0.20f && p2LookDelta > 0.1f;
            log.LogInfo($"PROBE: movement {(isolated ? "PASS" : "FAIL")}, P1 displaced {p1Moved:F2} m/look {p1LookDelta:F2}, P2 displaced {p2Moved:F2} m/look {p2LookDelta:F2}.");
            yield return new WaitForSecondsRealtime(0.5f);

            var controls = asset.FindAction("Interact").bindings;
            log.LogInfo("PROBE: interaction bindings " + string.Join(", ", controls.Select(binding => binding.effectivePath)));
            var prefab = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values
                .First(item => item.UIData.canDrop && item is not Backpack);
            var item = PhotonNetwork.InstantiateItemRoom(prefab.name, p2.Center + Vector3.up, Quaternion.identity).GetComponent<Item>();
            var p1Before = p1.player.itemSlots.Count(slot => !slot.IsEmpty());
            var p2Before = p2.player.itemSlots.Count(slot => !slot.IsEmpty());
            item.Interact(p2);
            yield return null;
            var pickup = p1.player.itemSlots.Count(slot => !slot.IsEmpty()) == p1Before
                && p2.player.itemSlots.Count(slot => !slot.IsEmpty()) == p2Before + 1 && !item;
            log.LogInfo($"PROBE: native pickup {(pickup ? "PASS" : "FAIL")}, item {prefab.name}, form Item.Interact(P2).");
            log.LogInfo($"PROBE: complete, result {(isolated && pickup ? "PASS" : "FAIL")}.");
        }
        finally
        {
            SeatHooks.Clear();
            scouts.Clear();
            if (pad.added) InputSystem.RemoveDevice(pad);
        }
        yield return new WaitForSecondsRealtime(1f);
        Application.Quit();
    }

    public System.Collections.IEnumerator DirectLaunchCheck()
    {
        while (!ui.LobbyOpen) yield return null;
        yield return new WaitForSecondsRealtime(1f);
        var stockMenu = Object.FindFirstObjectByType<MainMenuMainPage>(FindObjectsInactive.Include);
        var stockPlay = (UnityEngine.UI.Button)BuildGuard.Field(typeof(MainMenuMainPage), "m_playButton").GetValue(stockMenu);
        log.LogInfo($"FAST-CHECK: Direct3D11 {SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11}, automatic lobby {ui.LobbyOpen}, stock play hidden {!stockPlay.gameObject.activeInHierarchy}.");
        var pad = InputSystem.AddDevice<Gamepad>();
        yield return Press(pad, GamepadButton.Start);
        CaptureSlice("direct-lobby");
        var selection = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        if (runtime.Party.Seats.Count != 2 || !selection || selection.name != "Enter Airport")
        {
            log.LogError("FAST-CHECK: join or controller focus failed.");
            Application.Quit();
            yield break;
        }
        UnityEngine.EventSystems.ExecuteEvents.Execute(selection,
            new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current),
            UnityEngine.EventSystems.ExecuteEvents.submitHandler);
        var limit = Time.realtimeSinceStartup + 50f;
        while (Time.realtimeSinceStartup < limit && !runtime.GetCamera(2)) yield return null;
        if (!runtime.GetCamera(2))
        {
            log.LogError($"FAST-CHECK: Airport failed. {runtime.Status}");
            Application.Quit();
            yield break;
        }
        yield return new WaitForSecondsRealtime(2f);
        log.LogInfo($"FAST-CHECK: two players {runtime.Party.Seats.Count == 2}, offline {runtime.IsLocalRoom}, scene {SceneManager.GetActiveScene().name}.");
        CaptureSlice("direct-airport");
        yield return CheckSeatInput(pad, 2);
        log.LogInfo("FAST-CHECK: returning to title through the native room exit.");
        try
        {
            GameHandler.GetService<ConnectionService>().StateMachine.SwitchState<DisconnectingState>(false);
            NetworkConnector.LeaveRoom();
        }
        catch (Exception error)
        {
            log.LogError($"FAST-CHECK: native room exit failed. {error}");
            Application.Quit();
            yield break;
        }
        yield return new WaitForSecondsRealtime(25f);
        log.LogError("FAST-CHECK: direct process should have exited after returning to title.");
        Application.Quit();
    }

    public System.Collections.IEnumerator Slice(bool menuOnly = false)
    {
        while (SceneManager.GetActiveScene().name != MainMenu.SceneName ||
               !Object.FindFirstObjectByType<MainMenu>() || !GameHandler.Initialized || NetworkingUtilities.EnteringRoom)
            yield return null;
        yield return new WaitForSecondsRealtime(2f);
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
        yield return new WaitForSecondsRealtime(4f);
        var menuDeadline = Time.realtimeSinceStartup + 60f;
        while ((!ui.TitleButton || !ui.TitleButton.isActiveAndEnabled || !ui.TitleButton.IsInteractable())
            && Time.realtimeSinceStartup < menuDeadline)
            yield return null;
        var entry = ui.TitleButton;
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (!entry || !entry.isActiveAndEnabled || !entry.IsInteractable() || !eventSystem)
        {
            log.LogError("SLICE: native Local Co-op button unavailable.");
            Application.Quit();
            yield break;
        }
        var parent = entry.transform.parent;
        var index = entry.transform.GetSiblingIndex();
        var prev = parent.GetChild(index - 1);
        var followingRow = parent.GetChild(index + 1);
        log.LogInfo($"UI: native title row between {prev.name} and {followingRow.name}. Helping hand icon {entry.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Any(i => i.name == "Helping hand icon" && i.texture)}.");
        var eventData = new UnityEngine.EventSystems.PointerEventData(eventSystem);
        eventSystem.SetSelectedGameObject(null);
        var native = prev.GetComponent<UnityEngine.UI.Button>();
        log.LogInfo($"UI: Play Offline target {native.targetGraphic?.GetType().Name}; components {string.Join(", ", native.GetComponents<MonoBehaviour>().Select(it => it.GetType().Name))}; graphics {string.Join(", ", native.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Select(it => it.name))}.");
        foreach (var candidate in new[] { native, entry })
            UnityEngine.EventSystems.ExecuteEvents.Execute(candidate.gameObject, eventData,
                UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
        yield return new WaitForSecondsRealtime(0.4f);
        var iconGraphic = entry.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Single(i => i.name == "Helping hand icon");
        var hoverIconCenter = IconCenterInSocket(iconGraphic.rectTransform);
        var nativeHover = native.transform.localScale.x;
        var guestHover = entry.transform.localScale.x;
        UnityEngine.EventSystems.ExecuteEvents.Execute(native.gameObject, eventData,
            UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
        yield return new WaitForSecondsRealtime(0.4f);
        CaptureSlice("title-hover");
        UnityEngine.EventSystems.ExecuteEvents.Execute(entry.gameObject, eventData,
            UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
        yield return new WaitForSecondsRealtime(0.4f);
        CaptureSlice("title");
        var idleIconCenter = IconCenterInSocket(iconGraphic.rectTransform);
        log.LogInfo($"UI: icon socket {iconGraphic.transform.parent.name}; hover/idle center {hoverIconCenter}/{idleIconCenter} (relative to socket).");
        log.LogInfo($"UI: title native/coop hover scale {nativeHover:F2}/{guestHover:F2}, idle {entry.transform.localScale.x:F2}; transition {native.transition}/{entry.transition}.");
        yield return new WaitForSecondsRealtime(0.5f);
        var click = new UnityEngine.EventSystems.PointerEventData(eventSystem)
        {
            button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
        };
        UnityEngine.EventSystems.ExecuteEvents.Execute(entry.gameObject, click, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        var pad = InputSystem.AddDevice<Gamepad>();
        yield return Press(pad, GamepadButton.Start);
        if (runtime.Party.Seats.Count != 2)
        {
            log.LogError("SLICE: lobby join FAIL.");
            Application.Quit();
            yield break;
        }
        var seat = runtime.Party.Seats[1];
        CaptureSlice("lobby");
        yield return null;
        var selection = eventSystem.currentSelectedGameObject;
        var lobbySelection = selection && selection.name == "Enter Airport";
        log.LogInfo($"UI: controller focus on lobby start {lobbySelection}.");
        if (lobbySelection)
            UnityEngine.EventSystems.ExecuteEvents.Execute(selection,
                new UnityEngine.EventSystems.BaseEventData(eventSystem), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
        else
            ui.StartLobby(Object.FindFirstObjectByType<MainMenu>());
        while (!runtime.GetCharacter(seat) || !runtime.GetCamera(2))
        {
            if (runtime.Status != null)
            {
                log.LogError($"SLICE: FAIL {runtime.Status}");
                Application.Quit();
                yield break;
            }
            yield return null;
        }
        yield return new WaitForSecondsRealtime(3f);
        var before = runtime.GetCharacter(seat);
        log.LogInfo($"SLICE: Airport actor {before.photonView.OwnerActorNr}, listener count {Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled)}, viewport {runtime.GetCamera(2).pixelRect}.");
        var gui = GUIManager.instance;
        var guestRoot = GameObject.Find("Local HUD P2").transform.Find("Viewport/HUD/Gameplay/Native reticle");
        var guestDot = (RectTransform)guestRoot.Find(gui.reticleDefault.name);
        var hostDot = (RectTransform)gui.reticleDefault.transform;
        var hostCamera = runtime.GetCamera(1);
        var guestCamera = runtime.GetCamera(2);
        var hostBounds = IndicatorBounds(hostDot, gui.hudCanvas.worldCamera);
        var guestBounds = IndicatorBounds(guestDot, null);
        var hostCenter = hostBounds.center - hostCamera.pixelRect.center;
        var guestCenter = guestBounds.center - guestCamera.pixelRect.center;
        log.LogInfo($"UI: reticle centered {(hostCenter.sqrMagnitude < 0.1f && guestCenter.sqrMagnitude < 0.1f ? "PASS" : "FAIL")}.");
        log.LogInfo($"UI: native and P2 default reticle sizes {hostBounds.size}/{guestBounds.size}, relative centers {hostCenter}/{guestCenter}.");
        var hostThrowVisible = gui.throwGO.activeSelf;
        var items = before.refs.items;
        var priorThrow = items.throwChargeLevel;
        try
        {
            items.throwChargeLevel = 0.5f;
            runtime.LateTick();
            var meter = guestRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                .FirstOrDefault(image => image.name == gui.throwBar.name && image.gameObject.activeInHierarchy);
            log.LogInfo($"UI: P2 native throw meter visible {meter != null}, fill {(meter ? meter.fillAmount : 0f):F3}, host independent {hostThrowVisible == gui.throwGO.activeSelf}.");
        }
        finally
        {
            items.throwChargeLevel = priorThrow;
            runtime.LateTick();
        }
        CaptureSlice("airport");
        if (menuOnly)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            Application.Quit();
            yield break;
        }
        var mirrorFlagCheck = Environment.GetCommandLineArgs().Contains("-peak-coop-mirror-flag-check");
        if (mirrorFlagCheck) yield return CheckAirportMirror();
        var kiosk = Object.FindFirstObjectByType<AirportCheckInKiosk>();
        if (!kiosk)
        {
            log.LogError("SLICE: Airport kiosk missing.");
            Application.Quit();
            yield break;
        }

        kiosk.StartGame(0);
        log.LogInfo("SLICE: native kiosk initiated transition.");
        while (!GameHandler.IsOnIslandAndInitialized || !runtime.GetCharacter(seat) || runtime.GetCharacter(seat) == before || !runtime.GetCamera(2))
        {
            if (runtime.Status != null)
            {
                log.LogError($"SLICE: FAIL {runtime.Status}");
                Application.Quit();
                yield break;
            }
            yield return null;
        }
        yield return new WaitForSecondsRealtime(mirrorFlagCheck ? 2f : 10f);

        if (Environment.GetCommandLineArgs().Contains("-peak-coop-item-check"))
        {
            yield return CheckRopeCannon(seat);
            Application.Quit();
            yield break;
        }

        if (Environment.GetCommandLineArgs().Contains("-peak-coop-feed-check"))
        {
            var first = Character.localCharacter;
            var second = runtime.GetCharacter(seat);
            yield return CheckFeeding(first, second);
            yield return CheckFeeding(second, first);
            Application.Quit();
            yield break;
        }

        if (mirrorFlagCheck)
        {
            yield return CheckFlagPlacement(Character.localCharacter, runtime.GetCharacter(seat), pad);
            Application.Quit();
            yield break;
        }

        var logicCheck = Environment.GetCommandLineArgs().Contains("-peak-coop-logic-check");
        var host = Character.localCharacter;
        var p2 = runtime.GetCharacter(seat);
        if (Environment.GetCommandLineArgs().Contains("-peak-coop-world-check"))
        {
            yield return CheckWorldRegression(host, p2);
            Application.Quit();
            yield break;
        }
        if (logicCheck) yield return CheckAreaEffects(host, p2);
        var camera = runtime.GetCamera(2);
        log.LogInfo($"SLICE: island actors {PhotonNetwork.CurrentRoom.PlayerCount}, view {host.photonView.ViewID}/{p2.photonView.ViewID}, ID {seat.Id:N}, world {host.gameObject.scene.name}/{p2.gameObject.scene.name}, listener count {Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled)}.");
        var prefab = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values
            .First(item => item.UIData.canDrop && item is not Backpack);
        var item = PhotonNetwork.InstantiateItemRoom(prefab.name,
            camera.transform.position + camera.transform.forward * 1.05f,
            Quaternion.identity).GetComponent<Item>();
        item.SetKinematicRPC(true, item.transform.position, item.transform.rotation);
        yield return new WaitForSecondsRealtime(0.4f);
        var hover = ReferenceEquals(runtime.Hovered(2), item);
        var p1Inventory = host.player.itemSlots.Count(slot => !slot.IsEmpty());
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West));
        yield return new WaitForSecondsRealtime(0.35f);
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return new WaitForSecondsRealtime(0.3f);
        var picked = !item && p2.player.itemSlots.Any(slot => !slot.IsEmpty()) &&
            host.player.itemSlots.Count(slot => !slot.IsEmpty()) == p1Inventory;
        log.LogInfo($"SLICE: P2 ray hit test item {hover}, native controller pickup {picked}, P1 untouched {host.player.itemSlots.Count(slot => !slot.IsEmpty()) == p1Inventory}.");
        CaptureSlice("island");
        if (!picked)
        {
            Application.Quit();
            yield break;
        }

        var pickedSlot = p2.player.itemSlots.First(slot => !slot.IsEmpty());
        var countBeforeDrop = CountGroundItems(prefab.itemID);
        yield return Press(pad, GamepadButton.North);
        yield return new WaitForSecondsRealtime(0.4f);
        log.LogInfo($"SLICE: native drop {(pickedSlot.IsEmpty() && CountGroundItems(prefab.itemID) == countBeforeDrop + 1 ? "PASS" : "FAIL")}.");

        var raced = PhotonNetwork.InstantiateItemRoom(prefab.name, p2.Center + Vector3.up, Quaternion.identity).GetComponent<Item>();
        var inventory1 = InventorySize(host);
        var inventory2 = InventorySize(p2);
        raced.Interact(p2);
        raced.Interact(host);
        yield return null;
        log.LogInfo($"SLICE: same-frame pickup contention {(!raced && InventorySize(host) == inventory1 && InventorySize(p2) == inventory2 + 1 ? "PASS" : "FAIL")}.");

        var injury = CharacterAfflictions.STATUSTYPE.Injury;
        var originalInjury = p2.refs.afflictions.GetCurrentStatus(injury);
        p2.refs.afflictions.SetStatus(injury, 0.25f, false);
        yield return new WaitForSecondsRealtime(0.7f);
        var guestBar = GameObject.Find("Local HUD P2").GetComponentInChildren<StaminaBar>(true);
        var guestInjury = guestBar.afflictions.First(affliction => affliction.afflictionType == injury);
        var hostInjury = GUIManager.instance.bar.afflictions.First(affliction => affliction.afflictionType == injury);
        log.LogInfo($"UI: injury segment P2 {guestInjury.size:F1}, P1 {hostInjury.size:F1}. Independent bars {guestInjury.size > hostInjury.size}.");
        CaptureSlice("stamina");
        yield return new WaitForSecondsRealtime(0.4f);
        p2.refs.afflictions.SetStatus(injury, originalInjury, false);
        var stable = InventorySize(p2);
        InputSystem.RemoveDevice(pad);
        yield return new WaitForSecondsRealtime(0.3f);
        var afterDisconnect = runtime.GetCharacter(seat);
        var replacement = InputSystem.AddDevice<Gamepad>();
        yield return Press(replacement, GamepadButton.Start);
        log.LogInfo($"SLICE: controller replacement {(afterDisconnect == p2 && runtime.GetCharacter(seat) == p2 && InventorySize(p2) == stable && seat.Pad == replacement ? "PASS" : "FAIL")}.");

        var bagPrefab = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values.OfType<Backpack>()
            .First(item => item.backpackType == BackpackSlot.BackpackType.Backpack);
        var bag = PhotonNetwork.InstantiateItemRoom(bagPrefab.name, camera.transform.position + camera.transform.forward * 1.1f, Quaternion.identity)
            .GetComponent<Backpack>();
        bag.SetKinematicRPC(true, bag.transform.position, bag.transform.rotation);
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Press(replacement, GamepadButton.West);
        log.LogInfo($"SLICE: guest backpack only {(runtime.InventoryFor(2)?.IsOpen == true && !host.data.usingBackpackWheel ? "PASS" : "FAIL")}.");
        yield return Press(replacement, GamepadButton.DpadRight);
        log.LogInfo($"UI: D-pad backpack selection {runtime.InventoryFor(2)?.Selected}, P1 wheel closed {!host.data.usingBackpackWheel}.");
        CaptureSlice("backpack");
        yield return new WaitForSecondsRealtime(0.4f);
        if (logicCheck)
        {
            bag.SetKinematicRPC(true, p2.Center + Vector3.up * 18f, bag.transform.rotation);
            yield return new WaitForSecondsRealtime(0.5f);
            log.LogInfo($"LOGIC: remote backpack closes {(!p2.data.usingBackpackWheel && !host.data.usingBackpackWheel ? "PASS" : "FAIL")}.");
        }
        else yield return Press(replacement, GamepadButton.East);
        PhotonNetwork.Destroy(bag.gameObject);

        var camp = MapHandler.CurrentCampfire;
        Vector3 near;
        Vector3 nearGuest;
        try
        {
            near = CheckpointGate.FindSpawn(camp, 1);
            nearGuest = CheckpointGate.FindSpawn(camp, 2);
        }
        catch (Exception error)
        {
            log.LogError($"SLICE: campfire test could not position scouts. {error.Message}");
            Application.Quit();
            yield break;
        }
        yield return TryClimb(replacement, p2, host, camp.transform.position);
        Warp(host, near);
        Warp(p2, nearGuest);
        yield return new WaitForSecondsRealtime(2f);
        var segmentBefore = MapHandler.CurrentSegmentNumber;
        camp.DebugLight();
        var deadline = Time.realtimeSinceStartup + 12f;
        while (!runtime.CanJoin && Time.realtimeSinceStartup < deadline) yield return null;
        log.LogInfo($"SLICE: campfire candidate lit {camp.Lit}, in range {camp.EveryoneInRange()}, join gate {runtime.CanJoin}, segment {segmentBefore}/{MapHandler.CurrentSegmentNumber}.");

        if (runtime.CanJoin)
        {
            for (var number = 3; number <= 4; number++)
            {
                var next = InputSystem.AddDevice<Gamepad>();
                yield return Press(next, GamepadButton.Start);
                yield return new WaitForSecondsRealtime(1.2f);
                if (runtime.Party.Seats.Count != number)
                {
                    log.LogError($"SLICE: checkpoint P{number} FAIL. {runtime.Status}");
                    break;
                }
                var joined = runtime.GetCharacter(runtime.Party.Seats[number - 1]);
                var viewport = runtime.GetCamera(number).pixelRect;
                log.LogInfo($"SLICE: checkpoint P{number} actor {joined.photonView.OwnerActorNr}, items {InventorySize(joined)}, stamina {joined.data.currentStamina:F2}, view {viewport}, P2 inventory preserved {InventorySize(p2) == stable}, segment {MapHandler.CurrentSegmentNumber}.");
                var guestHud = GameObject.Find($"Local HUD P{number}");
                var hotbarSlots = guestHud ? guestHud.GetComponentsInChildren<InventoryItemUI>()
                    .Where(slot => !slot.isBackpack && !slot.isTemporarySlot && slot.outline.isActiveAndEnabled).ToArray()
                    : System.Array.Empty<InventoryItemUI>();
                var corners = new Vector3[4];
                var inside = hotbarSlots.All(slot =>
                {
                    slot.rectTransform.GetWorldCorners(corners);
                    return corners.All(point => viewport.Contains(RectTransformUtility.WorldToScreenPoint(null, point)));
                });
                log.LogInfo($"UI: P{number} visible hotbar frames {hotbarSlots.Length}/{joined.player.itemSlots.Length}, inside viewport {inside}.");
                CaptureSlice($"p{number}");
            }
            if (runtime.Party.Seats.Count == 4)
            {
                if (logicCheck)
                {
                    for (var i = 2; i < runtime.Party.Seats.Count; i++)
                    {
                        var joinedSeat = runtime.Party.Seats[i];
                        yield return CheckSeatInput(joinedSeat.Pad, joinedSeat.Number);
                        yield return CheckGuestInventory(joinedSeat, prefab);
                    }
                    yield return CheckControllerTools(runtime.Party.Seats[2], host);
                    yield return CheckConsumedItem(runtime.Party.Seats[2], prefab, host);
                    yield return CheckRescueAim(runtime.Party.Seats[2]);
                    yield return CheckJetpack(runtime.Party.Seats[3], host);
                }
                else yield return CompareQuality();
            }
            if (logicCheck)
            {
                yield return CheckTrapDamage(host, p2);
                yield return CheckCollisionTraps(host, p2);
                yield return CheckBanana(host, p2);
                yield return CheckPassOut(p2, host);
            }
            else
            {
                p2.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.9f);
                p2.photonView.RPC("RPCA_PassOut", RpcTarget.All);
                yield return null;
            }
            p2.refs.interactible.Interact(host);
            yield return null;
            log.LogInfo($"SLICE: native carry {(host.data.carriedPlayer == p2 && p2.data.carrier == host && !host.data.fullyPassedOut ? "PASS" : "FAIL")}.");
            host.photonView.RPC("RPCA_Drop", RpcTarget.All, p2.photonView);
            if (logicCheck)
            {
                InputSystem.QueueStateEvent(replacement, new GamepadState().WithButton(GamepadButton.West));
                var deathDeadline = Time.realtimeSinceStartup + 12f;
                while (!p2.data.dead && Time.realtimeSinceStartup < deathDeadline) yield return null;
                InputSystem.QueueStateEvent(replacement, new GamepadState());
                log.LogInfo($"DEATH: native hold-to-die {(p2.data.dead && !host.data.dead ? "PASS" : "FAIL")}, timer {p2.data.deathTimer:F2}.");
                if (p2.data.dead) yield return CheckGhost(p2, 2, 1);
            }
            else p2.photonView.RPC("RPCA_Die", RpcTarget.All);
            yield return new WaitForSecondsRealtime(0.35f);
            log.LogInfo($"SLICE: mixed death {(p2.data.dead && !host.data.dead && !host.gameOver ? "PASS" : "FAIL")}.");
            if (logicCheck && runtime.Party.Seats.Count == 4)
            {
                var previousView = runtime.Spectating(2);
                yield return Press(replacement, GamepadButton.DpadRight);
                var after = runtime.Spectating(2);
                log.LogInfo($"DEATH: P2 ghost follows changed scout {(p2.Ghost && p2.Ghost.m_target == after.Target ? "PASS" : "FAIL")}.");
                yield return Press(replacement, GamepadButton.LeftShoulder);
                var farther = runtime.Spectating(2);
                yield return Press(replacement, GamepadButton.RightShoulder);
                var closer = runtime.Spectating(2);
                log.LogInfo($"CTRL: P2 spectate dpad and bumpers {(previousView.Target && after.Target && previousView.Target != after.Target && farther.Distance > after.Distance && closer.Distance < farther.Distance ? "PASS" : "FAIL")}, distance {after.Distance:F2}/{farther.Distance:F2}/{closer.Distance:F2}.");
            }
            p2.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, nearGuest, true, (int)MapHandler.CurrentSegmentNumber);
            yield return new WaitForSecondsRealtime(2f);
            log.LogInfo($"SLICE: native revive {(!p2.data.dead && !p2.data.fullyPassedOut ? "PASS" : "FAIL")}.");

            if (logicCheck)
            {
                log.LogInfo($"DEATH: P2 ghost removed at revive {(!p2.Ghost ? "PASS" : "FAIL")}.");
                host.photonView.RPC("RPCA_Die", RpcTarget.All);
                yield return CheckGhost(host, 1, 2);
                host.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, near, true, (int)MapHandler.CurrentSegmentNumber);
                yield return new WaitForSecondsRealtime(1.5f);
                log.LogInfo($"DEATH: P1 ghost removed at revive {(!host.Ghost ? "PASS" : "FAIL")}.");
            }

            Warp(host, camp.transform.position - camp.transform.forward * 55f + Vector3.up * 5f);
            yield return new WaitForSecondsRealtime(0.7f);
            Warp(host, near);
            yield return new WaitForSecondsRealtime(1.6f);
            log.LogInfo($"SLICE: checkpoint re-entry gate {runtime.CanJoin}, closed {!runtime.CanJoin}.");

            if (logicCheck)
            {
                yield return CheckPersonalCheckpoint(host, p2, prefab);
                yield return CheckRopeCannon(seat);
            }

            foreach (var character in PlayerHandler.GetAllPlayerCharacters().ToArray())
                if (character && !character.data.dead) character.photonView.RPC("RPCA_Die", RpcTarget.All);
            yield return new WaitForSecondsRealtime(0.8f);
            log.LogInfo($"SLICE: all scouts dead {PlayerHandler.GetAllPlayerCharacters().All(character => character.data.dead)}, shared end screen {GUIManager.instance.endScreen.isOpen}.");
        }
        yield return new WaitForSecondsRealtime(2f);
        Application.Quit();
    }

    private System.Collections.IEnumerator CheckWorldRegression(Character host, Character guest)
    {
        log.LogInfo($"WORLD: focused world regression on {guest.gameObject.scene.name}.");
        yield return CheckTrapDamage(host, guest);
        yield return CheckCollisionTraps(host, guest);
        yield return CheckBanana(host, guest);
        var item = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values
            .First(candidate => candidate.UIData.canDrop && candidate is not Backpack);
        yield return CheckPersonalCheckpoint(host, guest, item);

        var camera = runtime.GetCamera(2);
        var position = camera.transform.position + camera.transform.forward * 2f;
        guest.photonView.RPC("ReceivePoint_Rpc", RpcTarget.All, position, -camera.transform.forward);
        yield return null;
        var marker = (GameObject)BuildGuard.Field(typeof(PointPinger), "pingInstance").GetValue(guest.GetComponent<PointPinger>());
        if (marker) yield return CheckPingPresentation(marker.transform, runtime.GetCamera(1), camera);
        else log.LogError("WORLD: native marker spawn FAIL.");
        log.LogInfo("WORLD: focused regression ended.");
    }

    private System.Collections.IEnumerator CheckTrapDamage(Character host, Character guest)
    {
        Collider BodyCollider(Character character) => character.GetComponentsInChildren<Collider>()
            .First(collider => collider.enabled && CharacterRagdoll.TryGetCharacterFromCollider(collider, out var hit) && hit == character);
        const CharacterAfflictions.STATUSTYPE injury = CharacterAfflictions.STATUSTYPE.Injury;
        var hostAfflictions = host.refs.afflictions;
        var guestAfflictions = guest.refs.afflictions;
        var hostStart = hostAfflictions.GetCurrentStatus(injury);
        var guestStart = guestAfflictions.GetCurrentStatus(injury);
        var fixture = new GameObject("Local Co-op trap diagnostic");
        fixture.SetActive(false);
        var saw = fixture.AddComponent<Peak.MovingSawBlade>();
        saw.timeout = 10f;
        saw.initialInjury = saw.constantInjury = 0.2f;
        BuildGuard.Field(typeof(Peak.MovingSawBlade), "lastPlayedSound").SetValue(saw, Time.time);
        var touch = BuildGuard.Method(typeof(Peak.MovingSawBlade), "OnTriggerStay", typeof(Collider));
        try
        {
            touch.Invoke(saw, new object[] { BodyCollider(host) });
            touch.Invoke(saw, new object[] { BodyCollider(guest) });
            yield return new WaitForSecondsRealtime(0.4f);
            var hostAfter = hostAfflictions.GetCurrentStatus(injury);
            var guestAfter = guestAfflictions.GetCurrentStatus(injury);
            touch.Invoke(saw, new object[] { BodyCollider(guest) });
            yield return new WaitForSecondsRealtime(0.3f);
            var independent = hostAfter > hostStart && guestAfter > guestStart;
            var clean = guestAfflictions.GetCurrentStatus(injury) == guestAfter;
            log.LogInfo($"DEATH: saw damage and per-scout cooldown {(independent && clean ? "PASS" : "FAIL")}, P1 {hostStart:F2}/{hostAfter:F2}, P2 {guestStart:F2}/{guestAfter:F2}, duplicate blocked {clean}.");
        }
        finally
        {
            hostAfflictions.SetStatus(injury, hostStart, false);
            guestAfflictions.SetStatus(injury, guestStart, false);
            Object.Destroy(fixture);
        }
    }

    private System.Collections.IEnumerator CheckCollisionTraps(Character host, Character guest)
    {
        Collider BodyCollider(Character scout) => scout.GetComponentsInChildren<Collider>()
            .First(collider => collider.enabled && CharacterRagdoll.TryGetCharacterFromCollider(collider, out var hit) && hit == scout);
        Collision Coinciding(Collider collider)
        {
            var collision = new Collision();
            var pairField = BuildGuard.Field(typeof(Collision), "m_Pair");
            var pair = System.Activator.CreateInstance(pairField.FieldType);
            BuildGuard.Field(pairField.FieldType, "m_OtherColliderID").SetValue(pair, collider.GetEntityId());
            pairField.SetValue(collision, pair);
            object contact = new ContactPoint();
            BuildGuard.Field(typeof(ContactPoint), "m_Point").SetValue(contact, collider.bounds.center);
            BuildGuard.Field(typeof(Collision), "m_LegacyContacts").SetValue(collision, new[] { (ContactPoint)contact });
            if (collision.collider != collider) throw new InvalidOperationException("Collider diagnostik tidak cocok.");
            return collision;
        }
        var hostHit = Coinciding(BodyCollider(host));
        var guestHit = Coinciding(BodyCollider(guest));
        const CharacterAfflictions.STATUSTYPE injury = CharacterAfflictions.STATUSTYPE.Injury;
        var hostStart = host.refs.afflictions.GetCurrentStatus(injury);
        var guestStart = guest.refs.afflictions.GetCurrentStatus(injury);
        var fixture = new GameObject("Local Co-op collision diagnostic");
        fixture.SetActive(false);
        try
        {
            var roller = fixture.AddComponent<Peak.SpikeRoller>();
            roller.injury = 0.15f;
            BuildGuard.Field(typeof(Peak.SpikeRoller), "liftForce").SetValue(roller, 0f);
            roller.timeout = 5f;
            yield return HitBoth(roller);

            var axe = fixture.AddComponent<SwingingAxe>();
            axe.rb = fixture.AddComponent<Rigidbody>();
            axe.maxInjury = 0.15f;
            axe.swingSpeedMult = 1f;
            axe.knockback = 0f;
            BuildGuard.Field(typeof(SwingingAxe), "fallTimeMult").SetValue(axe, 0f);
            yield return HitBoth(axe);
        }
        finally { Object.Destroy(fixture); }

        System.Collections.IEnumerator HitBoth(MonoBehaviour trap)
        {
            try
            {
                var hit = BuildGuard.Method(trap.GetType(), "OnCollisionEnter", typeof(Collision));
                hit.Invoke(trap, new object[] { hostHit });
                hit.Invoke(trap, new object[] { guestHit });
                yield return new WaitForSecondsRealtime(0.3f);
                var hostDelta = host.refs.afflictions.GetCurrentStatus(injury) - hostStart;
                var guestDelta = guest.refs.afflictions.GetCurrentStatus(injury) - guestStart;
                log.LogInfo($"WORLD: {trap.GetType().Name} P1/P2 independent damage {(hostDelta > 0.1f && guestDelta > 0.1f ? "PASS" : "FAIL")}, injury {hostDelta:F2}/{guestDelta:F2}.");
            }
            finally
            {
                host.refs.afflictions.SetStatus(injury, hostStart, false);
                guest.refs.afflictions.SetStatus(injury, guestStart, false);
            }
        }
    }

    private System.Collections.IEnumerator CheckBanana(Character host, Character guest)
    {
        var prefabs = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values;
        var prefab = prefabs.FirstOrDefault(candidate => candidate.GetComponent<BananaPeel>())
            ?? prefabs.First(candidate => candidate.UIData.canDrop && candidate is not Backpack);
        var item = PhotonNetwork.InstantiateItemRoom(prefab.name, guest.Center, Quaternion.identity).GetComponent<Item>();
        var peel = item.GetComponent<BananaPeel>();
        if (!peel) peel = item.gameObject.AddComponent<BananaPeel>();
        peel.slipSFX ??= System.Array.Empty<SFX_Instance>();
        item.photonView.RefreshRpcMonoBehaviourCache();
        var hostFall = host.data.fallSeconds;
        var guestFall = guest.data.fallSeconds;
        var grounded = guest.data.isGrounded;
        var velocity = guest.data.avarageVelocity;
        var hostGrounded = host.data.isGrounded;
        try
        {
            BuildGuard.Field(typeof(BananaPeel), "item").SetValue(peel, item);
            BuildGuard.Field(typeof(BananaPeel), "counter").SetValue(peel, 3f);
            host.data.isGrounded = false;
            guest.data.isGrounded = true;
            guest.data.avarageVelocity = Vector3.forward * 2f;
            BuildGuard.Method(typeof(BananaPeel), "Update").Invoke(peel, null);
            log.LogInfo($"WORLD: banana slip follows P2 {(guest.data.fallSeconds > guestFall && host.data.fallSeconds == hostFall ? "PASS" : "FAIL")}.");
        }
        finally
        {
            host.data.isGrounded = hostGrounded;
            guest.data.isGrounded = grounded;
            guest.data.avarageVelocity = velocity;
            guest.data.fallSeconds = guestFall;
            if (item) PhotonNetwork.Destroy(item.gameObject);
        }
        yield return null;
    }

    private System.Collections.IEnumerator CheckPersonalCheckpoint(Character host, Character guest, Item prefab)
    {
        var flagItem = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values
            .Select(candidate => candidate.GetComponent<CheckpointConstructable>()).First(candidate => candidate);
        var hostItems = InventorySize(host);
        var guestItems = InventorySize(guest);
        var flag = PhotonNetwork.Instantiate(flagItem.constructedPrefab.name, guest.Center + Vector3.up, Quaternion.identity)
            .GetComponent<CheckpointFlag>();
        flag.Initialize(guest);
        var item = PhotonNetwork.InstantiateItemRoom(prefab.name, guest.Center + Vector3.up, Quaternion.identity).GetComponent<Item>();
        var previousDrops = guest.refs.items.droppedItems.ToArray();
        guest.refs.items.droppedItems.Clear();
        guest.refs.items.droppedItems.Add(item.photonView);
        var activated = (bool)BuildGuard.Method(typeof(Character), "TryCheckpoint").Invoke(guest, null);
        yield return new WaitForSecondsRealtime(0.4f);
        log.LogInfo($"WORLD: P2 personal flag recovers own item {(activated && InventorySize(guest) == guestItems + 1 && InventorySize(host) == hostItems ? "PASS" : "FAIL")}, P1 {hostItems}/{InventorySize(host)}, P2 {guestItems}/{InventorySize(guest)}.");
        if (item) PhotonNetwork.Destroy(item.gameObject);
        guest.refs.items.droppedItems.AddRange(previousDrops.Where(view => view));
    }

    private System.Collections.IEnumerator CheckPassOut(Character scout, Character host)
    {
        var before = scout.refs.afflictions.statusSum;
        scout.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Injury, 1.1f);
        var deadline = Time.realtimeSinceStartup + 10f;
        while (!scout.data.fullyPassedOut && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
        var bar = GameObject.Find("Native guest dying bar");
        var passed = scout.data.fullyPassedOut && !scout.data.dead && !host.data.fullyPassedOut
            && scout.data.deathTimer > 0 && bar && bar.transform.IsChildOf(GameObject.Find("Local HUD P2").transform);
        log.LogInfo($"DEATH: injury causes native pass-out {(passed ? "PASS" : "FAIL")}, status {before:F2}/{scout.refs.afflictions.statusSum:F2}, timer {scout.data.deathTimer:F2}, dying bar {bar != null}, ghost while passed out {scout.Ghost != null}.");
    }

    private System.Collections.IEnumerator CheckGhost(Character owner, int ownerSeat, int otherSeat)
    {
        var deadline = Time.realtimeSinceStartup + 5f;
        while (!owner.Ghost && Time.realtimeSinceStartup < deadline) yield return null;
        var ghost = owner.Ghost;
        if (!ghost)
        {
            log.LogError($"DEATH: P{ownerSeat} native ghost spawn FAIL.");
            yield break;
        }
        var ownCamera = runtime.GetCamera(ownerSeat);
        var otherCamera = runtime.GetCamera(otherSeat);
        var renderedInOther = false;
        var hiddenInOwn = false;
        void CheckRender(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            if (camera == ownCamera)
                hiddenInOwn |= ghost.PlayerRenderers.All(render => render.forceRenderingOff);
            if (camera == otherCamera)
                renderedInOther |= ghost.PlayerRenderers.Any(render => render.enabled && !render.forceRenderingOff);
        }
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += CheckRender;
        try { yield return new WaitForSecondsRealtime(0.6f); }
        finally { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= CheckRender; }
        log.LogInfo($"DEATH: P{ownerSeat} native ghost {(ghost.m_owner == owner && ghost.m_target && renderedInOther && hiddenInOwn ? "PASS" : "FAIL")}, owner correct {ghost.m_owner == owner}, live target {ghost.m_target != null}, visible to P{otherSeat} {renderedInOther}, hidden on own camera {hiddenInOwn}.");
    }

    private System.Collections.IEnumerator CheckAreaEffects(Character host, Character guest)
    {
        var distance = Vector3.Distance(host.Center, guest.Center);
        var radius = Mathf.Min(0.75f, distance * 0.3f);
        var view = GameUtils.instance.GetComponent<PhotonView>();
        view.RPC("AddMassAffliction", RpcTarget.All, guest.Center, radius, 1);
        yield return null;
        log.LogInfo($"LOGIC: radius effect reaches only P2 {(guest.infiniteStam && !host.infiniteStam ? "PASS" : "FAIL")}.");
        yield return new WaitForSecondsRealtime(1.5f);
        view.RPC("AddMassAffliction", guest.photonView.Owner, guest.Center, distance + 4f, 1);
        yield return null;
        log.LogInfo($"LOGIC: directed effect reaches only P2 {(guest.infiniteStam && !host.infiniteStam ? "PASS" : "FAIL")}.");
        yield return new WaitForSecondsRealtime(1.5f);

        var marker = new GameObject("Co-op lava diagnostic");
        marker.transform.position = guest.Center - Vector3.up;
        var lava = marker.AddComponent<Lava>();
        lava.enabled = false;
        try
        {
            BuildGuard.Field(typeof(Lava), "bounds").SetValue(lava, new Bounds(guest.Center, Vector3.one * 0.8f));
            BuildGuard.Field(typeof(Lava), "height").SetValue(lava, 5f);
            BuildGuard.Field(typeof(Lava), "heat").SetValue(lava, 0.15f);
            BuildGuard.Field(typeof(Lava), "heatRate").SetValue(lava, 0f);
            const CharacterAfflictions.STATUSTYPE heat = CharacterAfflictions.STATUSTYPE.Hot;
            var p1 = host.refs.afflictions.GetCurrentStatus(heat);
            var p2 = guest.refs.afflictions.GetCurrentStatus(heat);
            BuildGuard.Method(typeof(Lava), "Update").Invoke(lava, null);
            var isolated = guest.refs.afflictions.GetCurrentStatus(heat) > p2 &&
                host.refs.afflictions.GetCurrentStatus(heat) == p1;
            guest.refs.afflictions.SetStatus(heat, p2, false);
            log.LogInfo($"LOGIC: bounded lava heat reaches only P2 {(isolated ? "PASS" : "FAIL")}.");
        }
        finally { Object.Destroy(marker); }
    }

    private System.Collections.IEnumerator CheckControllerTools(LocalParty.Seat seat, Character host)
    {
        var guest = runtime.GetCharacter(seat);
        var pad = seat.Pad;
        Warp(guest, CheckpointGate.FindSpawn(MapHandler.PreviousCampfire, seat.Number));
        yield return WaitForPhysics(0.6f);
        var guestHud = GameObject.Find($"Local HUD P{seat.Number}");
        var prefab = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values
            .First(item => item.TryGetComponent<RopeSpool>(out var spool) && !spool.isAntiRope);
        var ground = PhotonNetwork.InstantiateItemRoom(prefab.name, guest.Center + Vector3.up, Quaternion.identity)
            .GetComponent<Item>();
        ground.Interact(guest);
        yield return null;
        var slot = guest.player.itemSlots.FirstOrDefault(pocket => !pocket.IsEmpty() && pocket.prefab == prefab);
        if (slot == null)
        {
            log.LogError("CTRL: rope spool pickup FAIL.");
            yield break;
        }
        var items = guest.refs.items;
        items.EquipSlot(Zorro.Core.Optionable<byte>.Some(slot.itemSlotID));
        yield return new WaitForSecondsRealtime(0.8f);
        var spool = guest.data.currentItem ? guest.data.currentItem.GetComponent<RopeSpool>() : null;
        if (spool)
        {
            var segmentField = BuildGuard.Field(typeof(RopeSpool), "segments");
            float Segments() => (float)segmentField.GetValue(spool);
            var direction = BuildGuard.Field(typeof(RopeSpool), "scroll");
            var initial = Segments();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return WaitForPhysics(0.75f);
            var extended = Segments();
            var outward = (float)direction.GetValue(spool);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            yield return WaitForPhysics(0.75f);
            var retracted = Segments();
            var inward = (float)direction.GetValue(spool);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return WaitForPhysics(0.3f);
            var meter = guestHud.GetComponentInChildren<UI_Rope>();
            log.LogInfo($"CTRL: P{seat.Number} bumpers {(extended > initial + 1f && retracted < extended - 1f ? "PASS" : "FAIL")}, native rope length {initial:F1}/{extended:F1}/{retracted:F1}, scroll {outward:F2}/{inward:F2}, guest meter {meter != null}, host scroll clear {!host.input.scrollForwardIsPressed && !host.input.scrollBackwardIsPressed}.");
        }
        else log.LogError("CTRL: rope spool equip FAIL.");

        var hostSlot = host.refs.items.currentSelectedSlot;
        yield return Press(pad, GamepadButton.DpadRight);
        var right = items.currentSelectedSlot;
        yield return Press(pad, GamepadButton.DpadLeft);
        var left = items.currentSelectedSlot;
        yield return Press(pad, GamepadButton.DpadDown);
        var down = items.currentSelectedSlot;
        var rightWorks = right.IsSome && right.Value != slot.itemSlotID;
        var leftWorks = left.IsSome && left.Value == slot.itemSlotID;
        log.LogInfo($"CTRL: P{seat.Number} dpad hotbar {(rightWorks && leftWorks && down.IsNone && hostSlot.Equals(host.refs.items.currentSelectedSlot) ? "PASS" : "FAIL")}, right {rightWorks}, left {leftWorks}, down {down.IsNone}.");

        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadUp));
        yield return WaitForPhysics(0.4f);
        var wheel = guestHud.GetComponentInChildren<EmoteWheel>();
        var hostWheel = GUIManager.instance.emoteWheel.activeSelf;
        var wheelOpen = wheel && guest.data.usingEmoteWheel && !hostWheel;
        if (wheelOpen)
        {
            var aim = (Vector2)wheel.slices[0].GetUpVector();
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = aim }.WithButton(GamepadButton.DpadUp));
            yield return WaitForPhysics(0.4f);
        }
        var emoteField = BuildGuard.Field(typeof(EmoteWheel), "chosenEmoteData");
        var chosen = wheel && emoteField.GetValue(wheel) is EmoteWheelData;
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return WaitForPhysics(0.3f);
        var playing = BuildGuard.Field(typeof(CharacterAnimations), "emoting");
        var onlyGuest = (bool)playing.GetValue(guest.refs.animations) && !(bool)playing.GetValue(host.refs.animations);
        log.LogInfo($"CTRL: P{seat.Number} dpad emote {(wheelOpen && chosen && onlyGuest && !guest.data.usingEmoteWheel ? "PASS" : "FAIL")}, wheel {wheelOpen}, selected {chosen}, guest only {onlyGuest}.");

        var originalLook = guest.data.lookValues;
        guest.data.lookValues = new Vector2(host.data.lookValues.x + 110f, 0f);
        BuildGuard.Method(typeof(Character), "RecalculateLookDirections").Invoke(guest, null);
        yield return new WaitForSecondsRealtime(0.25f);
        var camera = runtime.GetCamera(seat.Number);
        var terrain = (int)HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainMap);
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            target.layer = Enumerable.Range(0, 32).First(i => (terrain & (1 << i)) != 0);
            target.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 0.65f, camera.transform.rotation);
            target.transform.localScale = new Vector3(1.65f, 1.65f, 0.1f);
            Physics.SyncTransforms();
            yield return Press(pad, GamepadButton.RightStick);
            var owned = BuildGuard.Field(typeof(PointPinger), "pingInstance");
            var guestPing = (GameObject)owned.GetValue(guest.GetComponent<PointPinger>());
            var hostPing = (GameObject)owned.GetValue(host.GetComponent<PointPinger>());
            var hit = guestPing && target.GetComponent<Collider>().bounds.SqrDistance(guestPing.transform.position) < 0.015f;
            log.LogInfo($"CTRL: P{seat.Number} ping uses own viewport {(hit && !hostPing ? "PASS" : "FAIL")}, guest marker {guestPing != null}, target hit {hit}, P1 marker {hostPing != null}.");
            if (guestPing) yield return CheckPingPresentation(guestPing.transform, runtime.GetCamera(1), camera);
        }
        finally
        {
            Object.Destroy(target);
            guest.data.lookValues = originalLook;
            BuildGuard.Method(typeof(Character), "RecalculateLookDirections").Invoke(guest, null);
        }

        items.EquipSlot(Zorro.Core.Optionable<byte>.Some(slot.itemSlotID));
        yield return WaitForPhysics(0.4f);
        yield return Press(pad, GamepadButton.North);
    }

    private System.Collections.IEnumerator CheckPingPresentation(Transform marker, Camera main, Camera guest)
    {
        var seenMain = false;
        var seenGuest = false;
        var mainRotation = Quaternion.identity;
        var guestRotation = Quaternion.identity;
        var mainScale = Vector3.zero;
        var guestScale = Vector3.zero;
        void Sample(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            if (!marker) return;
            if (camera == main) { mainRotation = marker.rotation; mainScale = marker.localScale; seenMain = true; }
            if (camera == guest) { guestRotation = marker.rotation; guestScale = marker.localScale; seenGuest = true; }
        }
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += Sample;
        try { yield return new WaitForSecondsRealtime(0.4f); }
        finally { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= Sample; }
        var delta = Quaternion.Angle(mainRotation, guestRotation);
        var scaleDelta = Vector3.Distance(mainScale, guestScale);
        log.LogInfo($"WORLD: ping presentation per camera {(seenMain && seenGuest && (delta > 0.01f || scaleDelta > 0.001f) ? "PASS" : "FAIL")}, rotation delta {delta:F2} degrees, scale delta {scaleDelta:F3}.");
    }

    private System.Collections.IEnumerator CheckConsumedItem(LocalParty.Seat seat, Item prefab, Character host)
    {
        var scout = runtime.GetCharacter(seat);
        var ground = PhotonNetwork.InstantiateItemRoom(prefab.name, scout.Center + Vector3.up, Quaternion.identity)
            .GetComponent<Item>();
        var hostCount = InventorySize(host);
        ground.Interact(scout);
        yield return null;
        var slot = scout.player.itemSlots.FirstOrDefault(pocket => !pocket.IsEmpty() && pocket.prefab == prefab);
        if (slot == null)
        {
            log.LogError("LOGIC: cooking item could not be picked up.");
            yield break;
        }
        scout.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.Some(slot.itemSlotID));
        yield return new WaitForSecondsRealtime(0.5f);
        var held = scout.data.currentItem;
        if (!held || !held.cooking)
        {
            log.LogError("LOGIC: cooking item could not be equipped.");
            yield break;
        }
        BuildGuard.Method(typeof(ItemCooking), "CookingExplodeImpl").Invoke(held.cooking, null);
        yield return null;
        log.LogInfo($"LOGIC: P{seat.Number} ruined food clears its own slot {(slot.IsEmpty() && InventorySize(host) == hostCount ? "PASS" : "FAIL")}.");
    }

    private System.Collections.IEnumerator CheckRescueAim(LocalParty.Seat seat)
    {
        var scout = runtime.GetCharacter(seat);
        var prefab = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values
            .First(candidate => candidate.GetComponentInChildren<RescueHook>(true));
        var ground = PhotonNetwork.InstantiateItemRoom(prefab.name, scout.Center + Vector3.up, Quaternion.identity)
            .GetComponent<Item>();
        ground.Interact(scout);
        yield return null;
        var slot = scout.player.itemSlots.FirstOrDefault(pocket => !pocket.IsEmpty() && pocket.prefab == prefab);
        if (slot == null)
        {
            log.LogError("LOGIC: rescue hook could not be picked up.");
            yield break;
        }
        scout.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.Some(slot.itemSlotID));
        yield return new WaitForSecondsRealtime(0.6f);
        var hook = scout.data.currentItem?.GetComponentInChildren<RescueHook>(true);
        var camera = runtime.GetCamera(seat.Number);
        var localRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        var actualRay = hook ? (Ray)BuildGuard.Method(typeof(RescueHook), "GetMiddleScreenRay").Invoke(hook, null) : default;
        log.LogInfo($"LOGIC: P{seat.Number} rescue hook uses its panel {(Vector3.Distance(localRay.origin, actualRay.origin) < 0.001f && Vector3.Angle(localRay.direction, actualRay.direction) < 0.1f ? "PASS" : "FAIL")}.");
    }

    private System.Collections.IEnumerator CheckJetpack(LocalParty.Seat seat, Character host)
    {
        var scout = runtime.GetCharacter(seat);
        var prefab = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values.OfType<Backpack>()
            .First(candidate => candidate.backpackType == BackpackSlot.BackpackType.Jetpack);
        var ground = PhotonNetwork.InstantiateItemRoom(prefab.name, scout.Center + Vector3.up, Quaternion.identity)
            .GetComponent<Backpack>();
        ground.SetKinematicRPC(true, ground.transform.position, ground.transform.rotation);
        yield return null;
        var bag = runtime.InventoryFor(seat.Number);
        ground.Interact(scout);
        yield return null;
        if (scout.player.backpackSlot.IsEmpty())
        {
            bag.Open(BackpackReference.GetFromBackpackItem(ground), ground.slotCount, ground.backpackType);
            yield return Press(seat.Pad, GamepadButton.South);
        }
        yield return new WaitForSecondsRealtime(0.5f);
        var equipped = scout.player.backpackSlot;
        if (equipped.IsEmpty() || !equipped.data.TryGetDataEntry<FloatItemData>(DataEntryKey.Fuel, out var fuel))
        {
            log.LogError("LOGIC: jetpack could not be equipped.");
            yield break;
        }
        bag.Open(BackpackReference.GetFromEquippedBackpack(scout), prefab.slotCount, prefab.backpackType);
        var fuelEntry = bag.IsOpen && bag.Count == 1 && bag.FuelSelected;
        bag.Close();
        scout.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.None);
        fuel.Value = 50f;
        var hostBackpack = host.player.backpackSlot.prefab;
        yield return new WaitForSecondsRealtime(0.5f);
        var fueled = (bool)BuildGuard.Method(typeof(CharacterMovement), "TryJetpack").Invoke(scout.refs.movement, null);
        log.LogInfo($"LOGIC: P{seat.Number} jetpack fuel entry {fuelEntry}, own fuel consumed {fueled && fuel.Value < 50f}, P1 backpack unchanged {host.player.backpackSlot.prefab == hostBackpack}.");
    }

    private System.Collections.IEnumerator CheckGuestInventory(LocalParty.Seat seat, Item prefab)
    {
        var scout = runtime.GetCharacter(seat);
        var others = runtime.Party.Seats.Where(other => other != seat).Select(runtime.GetCharacter).ToArray();
        var counts = others.Select(InventorySize).ToArray();
        var original = InventorySize(scout);
        var item = PhotonNetwork.InstantiateItemRoom(prefab.name, scout.Center + Vector3.up, Quaternion.identity)
            .GetComponent<Item>();
        item.Interact(scout);
        yield return null;
        var picked = !item && InventorySize(scout) == original + 1;
        var pocket = scout.player.itemSlots.FirstOrDefault(slot => !slot.IsEmpty() && slot.prefab == prefab);
        if (pocket != null)
        {
            scout.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.Some(pocket.itemSlotID));
            yield return new WaitForSecondsRealtime(0.4f);
        }
        var worldBefore = CountGroundItems(prefab.itemID);
        yield return Press(seat.Pad, GamepadButton.North);
        var dropped = InventorySize(scout) == original && CountGroundItems(prefab.itemID) == worldBefore + 1;
        var isolated = others.Select(InventorySize).SequenceEqual(counts);
        log.LogInfo($"LOGIC: P{seat.Number} pickup {picked}, controller drop {dropped}, other inventories unchanged {isolated}.");
    }

    private System.Collections.IEnumerator CheckAirportMirror()
    {
        var mirror = Object.FindObjectsByType<Mirror>(FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.isActiveAndEnabled);
        if (!mirror)
        {
            log.LogError($"MIRROR: FAIL no native Airport mirror, alternatives {Object.FindObjectsByType<MirrorCameraScript>(FindObjectsSortMode.None).Length}/{Object.FindObjectsByType<MirrorReflection>(FindObjectsSortMode.None).Length}.");
            yield break;
        }
        var surface = MirrorPresentation.Surface(mirror);
        var reflection = MirrorPresentation.Reflection(mirror);
        var panels = new[] { runtime.GetCamera(1), runtime.GetCamera(2) };
        var normal = surface.up;
        var middle = surface.GetComponent<Renderer>().bounds.center;
        var hop = 0;
        var samples = new Vector3[2, 2];
        var errors = new float[2, 2];
        var seen = new bool[2, 2];

        void Aim(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            for (var i = 0; i < panels.Length; i++)
            {
                var offset = i == 0 ? hop * 0.35f : -0.3f;
                var pos = middle + normal * 1.8f + surface.right * offset;
                panels[i].transform.SetPositionAndRotation(pos, Quaternion.LookRotation(middle - pos));
            }
        }
        void Read(ScriptableRenderContext context, Camera panel)
        {
            var i = Array.IndexOf(panels, panel);
            if (i < 0) return;
            seen[hop, i] = true;
            samples[hop, i] = reflection.transform.position;
            var expected = surface.position + Vector3.Reflect(panel.transform.position - surface.position, normal) +
                surface.forward * (float)BuildGuard.Field(typeof(Mirror), "verticalOffset").GetValue(mirror);
            errors[hop, i] = Vector3.Distance(expected, samples[hop, i]);
        }

        RenderPipelineManager.beginContextRendering += Aim;
        RenderPipelineManager.endCameraRendering += Read;
        try
        {
            yield return new WaitForEndOfFrame();
            hop = 1;
            CaptureSlice("mirror");
            yield return new WaitForEndOfFrame();
            var mainMoves = Vector3.Distance(samples[0, 0], samples[1, 0]) > 0.25f;
            var guestStill = Vector3.Distance(samples[0, 1], samples[1, 1]) < 0.001f;
            var pass = seen.Cast<bool>().All(value => value) && errors.Cast<float>().Max() < 0.002f &&
                mainMoves && guestStill && !reflection.enabled;
            log.LogInfo($"MIRROR: {(pass ? "PASS" : "FAIL")} native reflection follows P1 {mainMoves}, P2 stays fixed {guestStill}, expected reflection error {errors.Cast<float>().Max():F4}, automatic camera disabled {!reflection.enabled}.");
        }
        finally
        {
            RenderPipelineManager.beginContextRendering -= Aim;
            RenderPipelineManager.endCameraRendering -= Read;
        }
    }

    private System.Collections.IEnumerator CheckFlagPlacement(Character host, Character guest, Gamepad pad)
    {
        var prefab = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values
            .First(item => item.GetComponent<CheckpointConstructable>());
        var hostInventory = InventorySize(host);
        var groundItem = PhotonNetwork.InstantiateItemRoom(prefab.name, guest.Center + Vector3.up, Quaternion.identity)
            .GetComponent<Item>();
        groundItem.Interact(guest);
        yield return null;
        var slot = guest.player.itemSlots.FirstOrDefault(pocket => !pocket.IsEmpty() && pocket.prefab == prefab);
        if (slot == null)
        {
            log.LogError("FLAG: FAIL pickup.");
            yield break;
        }
        guest.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.Some(slot.itemSlotID));
        yield return new WaitForSecondsRealtime(0.8f);
        var held = guest.data.currentItem;
        if (!held || !held.TryGetComponent<CheckpointConstructable>(out var constructable))
        {
            log.LogError("FLAG: FAIL equip.");
            yield break;
        }

        var valid = BuildGuard.Field(typeof(Constructable), "valid");
        var hitField = BuildGuard.Field(typeof(Constructable), "currentConstructHit");
        var previewField = BuildGuard.Field(typeof(Constructable), "currentPreview");
        var recalc = BuildGuard.Method(typeof(Character), "RecalculateLookDirections");
        var ready = false;
        var initialLook = guest.data.lookValues;
        foreach (var angle in new[] { 45f, 60f, 75f, -45f, -60f, -75f })
        {
            for (var turn = 0; turn < 4 && !ready; turn++)
            {
                guest.data.lookValues = new Vector2(initialLook.x + 90f * turn, angle);
                recalc.Invoke(guest, null);
                yield return new WaitForSecondsRealtime(0.15f);
                ready = (bool)valid.GetValue(constructable) &&
                    ((RaycastHit)hitField.GetValue(constructable)).collider &&
                    (ConstructablePreview)previewField.GetValue(constructable);
            }
            if (ready) break;
        }
        var priorFlags = guest.data.checkpointFlags.ToArray();
        var hostFlags = host.data.checkpointFlags.Count;
        if (!ready)
        {
            log.LogError("FLAG: FAIL no valid placement surface.");
            yield break;
        }
        var starts = (Action)BuildGuard.Field(typeof(Item), "OnPrimaryStarted").GetValue(held);
        var finishes = (Action)BuildGuard.Field(typeof(Item), "OnPrimaryFinishedCast").GetValue(held);
        log.LogInfo($"FLAG: subscribers start [{string.Join(", ", starts?.GetInvocationList().Select(d => d.Method.DeclaringType.Name + "." + d.Method.Name) ?? Enumerable.Empty<string>())}], finish [{string.Join(", ", finishes?.GetInvocationList().Select(d => d.Method.DeclaringType.Name + "." + d.Method.Name) ?? Enumerable.Empty<string>())}], cast duration {BuildGuard.Field(typeof(Item), "usingTimePrimary").GetValue(held)}.");
        var constructing = BuildGuard.Field(typeof(Constructable), "constructing");
        var castProgress = AccessTools.PropertyGetter(typeof(Item), "castProgress");
        var sawPress = false;
        var sawHeld = false;
        var sawConstructing = false;
        var maxProgress = 0f;
        var firstPhase = true;
        var startCalls = 0;
        var finishCalls = 0;
        var cancelCalls = 0;
        held.OnPrimaryStarted += () => startCalls++;
        held.OnPrimaryFinishedCast += () => finishCalls++;
        held.OnPrimaryCancelled += () => cancelCalls++;
        var heldAction = SeatHooks.ActionsFor(guest).Asset.FindAction("UsePrimary");
        log.LogInfo($"FLAG: native binding {string.Join(", ", heldAction.controls.Select(control => control.path))}, can use {held.CanUsePrimary()}, items enabled {guest.refs.items.isActiveAndEnabled}, struggling {guest.refs.afflictions.isStruggling}, position {guest.Center}.");
        InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f });
        var deadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < deadline && guest.data.checkpointFlags.Count <= priorFlags.Length)
        {
            yield return null;
            if (!held) break;
            sawPress |= guest.input.usePrimaryWasPressed;
            sawHeld |= guest.input.usePrimaryIsPressed;
            sawConstructing |= (bool)constructing.GetValue(constructable);
            maxProgress = Mathf.Max(maxProgress, (float)castProgress.Invoke(held, null));
            if (!firstPhase || !pad.rightTrigger.isPressed) continue;
            firstPhase = false;
            log.LogInfo($"FLAG: first held frame pressed {sawPress}, constructing {sawConstructing}, usable {held.CanUsePrimary()}, valid {valid.GetValue(constructable)}, hit {((RaycastHit)hitField.GetValue(constructable)).collider != null}, raw {pad.rightTrigger.isPressed}, paired {heldAction.IsPressed()}, allowed {SeatHooks.AllowsGuestInput(guest, SeatHooks.ActionsFor(guest))}, wheel {guest.data.usingWheel}, blocked {GUIManager.instance.windowBlockingInput}, pause {GUIManager.InPauseMenu}, game over {host.gameOver}.");
        }
        log.LogInfo($"FLAG: cast trace pressed {sawPress}, held {sawHeld}, constructing {sawConstructing}, progress {maxProgress:F3}, event calls start/finish/cancel {startCalls}/{finishCalls}/{cancelCalls}.");
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return null;
        var flag = guest.data.checkpointFlags.FirstOrDefault(candidate => !priorFlags.Contains(candidate));
        var placed = flag && guest.data.checkpointFlags.Count == priorFlags.Length + 1 &&
            host.data.checkpointFlags.Count == hostFlags && slot.IsEmpty() && InventorySize(host) == hostInventory;
        log.LogInfo($"FLAG: {(placed ? "PASS" : "FAIL")} P2 native controller placement, owner {flag != null}, consumed {slot.IsEmpty()}, P1 inventory unchanged {InventorySize(host) == hostInventory}.");
        if (!placed)
        {
            yield break;
        }

        var target = flag.transform.position + Vector3.up;
        Warp(host, target);
        yield return new WaitForSecondsRealtime(1.1f);
        var hostPosition = host.Center;
        var occupied = Vector3.Distance(hostPosition, target) < 0.9f;
        BuildGuard.Method(typeof(Character), "DieInstantly").Invoke(guest, null);
        yield return new WaitForSecondsRealtime(2f);
        var returned = !guest.warping && !guest.data.dead && guest.data.fullyConscious &&
            Vector3.Distance(guest.Center, target) < 2.1f;
        var separated = Vector3.Distance(guest.Center, host.Center) > 0.85f;
        var start = guest.Center;
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up });
        yield return new WaitForSecondsRealtime(0.85f);
        InputSystem.QueueStateEvent(pad, new GamepadState());
        var moved = Vector3.Distance(guest.Center, start) > 0.35f;
        var hostStayed = Vector3.Distance(host.Center, hostPosition) < 1.5f;
        log.LogInfo($"FLAG: {(occupied && returned && separated && moved && hostStayed ? "PASS" : "FAIL")} P2 respawn and controller movement, occupied {occupied}, returned {returned}, separated {separated}, moved {moved}, P1 stayed {hostStayed}.");
    }

    private System.Collections.IEnumerator CheckFeeding(Character giver, Character receiver)
    {
        var prefabs = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values;
        foreach (var name in new[] { "Bandage", "Apple Berry" })
        {
            var prefab = prefabs.FirstOrDefault(candidate =>
                candidate.name.Replace(" ", "").Replace("_", "").IndexOf(
                    name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) >= 0);
            if (!prefab)
            {
                log.LogError($"FEED: FAIL missing prefab {name}.");
                continue;
            }
            var recipientInventory = InventorySize(receiver);
            var item = PhotonNetwork.InstantiateItemRoom(prefab.name, giver.Center + Vector3.up, Quaternion.identity)
                .GetComponent<Item>();
            item.Interact(giver);
            yield return null;
            var slot = giver.player.itemSlots.FirstOrDefault(pocket => !pocket.IsEmpty() && pocket.prefab == prefab);
            if (slot == null)
            {
                log.LogError($"FEED: FAIL pickup {name}.");
                continue;
            }
            giver.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.Some(slot.itemSlotID));
            yield return new WaitForSecondsRealtime(0.8f);
            var held = giver.data.currentItem;
            if (!held || held.trueHolderCharacter != giver)
            {
                log.LogError($"FEED: FAIL equip {name}.");
                continue;
            }
            receiver.FeedItem(held);
            yield return new WaitForSecondsRealtime(0.8f);
            var consumed = slot.IsEmpty() && (!held || !held.gameObject.activeInHierarchy);
            var recipientUnchanged = InventorySize(receiver) == recipientInventory;
            var direction = giver == Character.localCharacter ? "P1 to P2" : "P2 to P1";
            log.LogInfo($"FEED: {(consumed && recipientUnchanged ? "PASS" : "FAIL")} {direction} {name}, consumed {consumed}, recipient inventory unchanged {recipientUnchanged}.");
        }
    }

    private System.Collections.IEnumerator CheckRopeCannon(LocalParty.Seat seat)
    {
        var guest = runtime.GetCharacter(seat);
        var hostCamera = runtime.GetCamera(1);
        var guestCamera = runtime.GetCamera(2);
        var prefab = Zorro.Core.SingletonAsset<ItemDatabase>.Instance.itemLookup.Values
            .First(candidate => candidate.GetComponentInChildren<RopeShooter>(true));
        var item = PhotonNetwork.InstantiateItemRoom(prefab.name, guest.Center + Vector3.up, Quaternion.identity)
            .GetComponent<Item>();
        item.Interact(guest);
        yield return null;
        var equipped = guest.player.itemSlots.First(slot => !slot.IsEmpty() && slot.prefab == prefab);
        guest.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.Some(equipped.itemSlotID));
        yield return new WaitForSecondsRealtime(0.8f);
        var actualItem = guest.data.currentItem;
        var gun = actualItem ? actualItem.GetComponentInChildren<RopeShooter>(true) : null;
        if (!gun)
        {
            log.LogError("ITEM: Rope Cannon equip failed.");
            yield break;
        }
        var look = guest.data.lookValues;
        var originalGrounded = guest.data.isGrounded;
        guest.data.lookValues = new Vector2(Character.localCharacter.data.lookValues.x + 110f, 0f);
        BuildGuard.Method(typeof(Character), "RecalculateLookDirections").Invoke(guest, null);
        yield return new WaitForSecondsRealtime(0.3f);
        var targets = new GameObject[2];
        try
        {
            var mask = (int)HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainMap);
            var layer = Enumerable.Range(0, 32).First(i => (mask & (1 << i)) != 0);
            var cameras = new[] { hostCamera, guestCamera };
            for (var i = 0; i < cameras.Length; i++)
            {
                targets[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                targets[i].layer = layer;
                var camera = cameras[i];
                targets[i].transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 0.75f, camera.transform.rotation);
                targets[i].transform.localScale = new Vector3(0.25f, 0.25f, 0.08f);
            }
            Physics.SyncTransforms();
            guest.data.isGrounded = true;
            var willAttach = BuildGuard.Method(typeof(RopeShooter), "WillAttach", typeof(RaycastHit).MakeByRefType());
            var args = new object[] { default(RaycastHit) };
            var ready = (bool)willAttach.Invoke(gun, args);
            var preview = (RaycastHit)args[0];
            var before = Object.FindObjectsByType<RopeAnchorProjectile>(FindObjectsSortMode.None).ToHashSet();
            BuildGuard.Method(typeof(RopeShooter), "OnPrimaryFinishedCast").Invoke(gun, null);
            var projectile = Object.FindObjectsByType<RopeAnchorProjectile>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => !before.Contains(candidate));
            var lastPoint = projectile ? (Vector3)BuildGuard.Field(typeof(RopeAnchorProjectile), "lastShotTo").GetValue(projectile) : default;
            var hitP2 = ready && preview.collider && preview.collider.gameObject == targets[1];
            var launchedToPreview = projectile && Vector3.Distance(lastPoint, preview.point) < 0.05f;
            log.LogInfo($"ITEM: Rope Cannon {(hitP2 && launchedToPreview ? "PASS" : "FAIL")}, equipped by P2 {actualItem.holderCharacter == guest}, preview P2 target {hitP2}, projectile matched preview {launchedToPreview}, camera dot {Vector3.Dot(hostCamera.transform.forward, guestCamera.transform.forward):F2}.");
        }
        catch (Exception error)
        {
            log.LogError($"ITEM: Rope Cannon test failed. {error.GetBaseException()}");
        }
        finally
        {
            foreach (var target in targets) if (target) Object.Destroy(target);
            guest.data.isGrounded = originalGrounded;
            guest.data.lookValues = look;
        }
        yield return null;
    }

    private System.Collections.IEnumerator TryClimb(Gamepad pad, Character scout, Character host, Vector3 vicinity)
    {
        var mask = HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainMap);
        var recalc = BuildGuard.Method(typeof(Character), "RecalculateLookDirections");
        var positions = new[] { 4f, 7f, 10f, 13f, 16f, 20f, 24f, 28f };
        var originalHostLook = host.data.lookValues;
        scout.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.None);
        yield return null;
        foreach (var radius in positions)
        {
            for (var step = 0; step < 18; step++)
            {
                var heading = Quaternion.Euler(0f, step * 20f, 0f) * Vector3.forward;
                var sample = vicinity + radius * heading;
                if (!Physics.Raycast(sample + Vector3.up * 5f, Vector3.down, out var floor, 10f, mask) || floor.normal.y < 0.85f)
                    continue;
                for (var angle = 0; angle < 8; angle++)
                {
                    var direction = Quaternion.Euler(0f, angle * 45f, 0f) * Vector3.forward;
                    if (!Physics.Raycast(floor.point + Vector3.up * 1.65f, direction, out var wall, 1.20f, mask)
                        || wall.distance < 0.55f || Mathf.Abs(wall.normal.y) > 0.55f)
                        continue;
                    Warp(scout, floor.point + Vector3.up * 0.1f);
                    yield return new WaitForSecondsRealtime(1.2f);
                    scout.data.lookValues = new Vector2(angle * 45f, 0f);
                    recalc.Invoke(scout, null);
                    yield return new WaitForSecondsRealtime(0.2f);
                    var camera = runtime.GetCamera(2);
                    if (!Physics.Raycast(camera.transform.position, camera.transform.forward, out _, 1.25f, mask))
                        continue;
                    var primary = InputSystem.actions.FindAction("UsePrimary");
                    log.LogInfo("SLICE: primary binding " + string.Join(", ", primary.bindings.Select(binding => binding.effectivePath)));
                    var guestReticle = GameObject.Find("Local HUD P2").transform.Find("Viewport/HUD/Gameplay/Native reticle");
                    var stateBefore = ActiveReticle(guestReticle);
                    var hostBefore = GUIManager.instance.reticleDefaultImage;
                    InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f, leftStick = Vector2.up });
                    yield return new WaitForSecondsRealtime(0.55f);
                    var climbed = scout.data.isClimbing && !host.data.isClimbing && host.data.lookValues == originalHostLook;
                    if (climbed)
                    {
                        var during = ActiveReticle(guestReticle);
                        log.LogInfo($"UI: P2 reticle idle {stateBefore}, climbing {during}; host HUD retained {hostBefore == GUIManager.instance.reticleDefaultImage}.");
                        CaptureSlice("climb-reticle");
                        yield return new WaitForEndOfFrame();
                    }
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    yield return new WaitForSecondsRealtime(0.3f);
                    if (climbed)
                    {
                        log.LogInfo("SLICE: guest native climb PASS.");
                        yield break;
                    }
                    log.LogInfo($"SLICE: climb attempt grade {wall.normal.y:F2}, held item {scout.data.currentItem != null}, sincePress {scout.data.sincePressClimb:F2}.");
                }
            }
        }
        log.LogWarning("SLICE: guest native climb inconclusive. No candidate gave a stable wall climb.");
    }

    private static Vector2 IconCenterInSocket(RectTransform icon) =>
        icon.parent.InverseTransformPoint(icon.TransformPoint(icon.rect.center));

    private static Rect IndicatorBounds(RectTransform transform, Camera camera)
    {
        var corners = new Vector3[4];
        transform.GetWorldCorners(corners);
        var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static string ActiveReticle(Transform root) => root.Cast<Transform>()
        .FirstOrDefault(child => child.gameObject.activeSelf)?.name ?? "hidden";

    private System.Collections.IEnumerator CompareQuality()
    {
        if (GraphicsSettings.currentRenderPipeline is not UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset pipeline)
            yield break;
        var path = OutputPath("quality.csv");
        if (path == null) yield break;
        var sampler = new FrameSampler(log, path);
        var baseLook = Character.localCharacter.data.lookValues;
        AimTogether(baseLook, staggered: false);
        yield return new WaitForSecondsRealtime(2f);
        yield return sampler.Sample(4, "quality-reference", 12f);

        var originalScale = pipeline.renderScale;
        var originalShadowDistance = pipeline.shadowDistance;
        try
        {
            pipeline.shadowDistance = originalShadowDistance * 0.5f;
            yield return new WaitForSecondsRealtime(2f);
            yield return sampler.Sample(4, "half-shadow-distance", 12f);
            pipeline.shadowDistance = originalShadowDistance;
            pipeline.renderScale = originalScale * 0.75f;
            yield return new WaitForSecondsRealtime(2f);
            yield return sampler.Sample(4, "three-quarter-render-scale", 12f);
        }
        finally
        {
            pipeline.renderScale = originalScale;
            pipeline.shadowDistance = originalShadowDistance;
        }
    }

    public System.Collections.IEnumerator Benchmark()
    {
        while (SceneManager.GetActiveScene().name != MainMenu.SceneName ||
               !Object.FindFirstObjectByType<MainMenu>() || !GameHandler.Initialized || NetworkingUtilities.EnteringRoom)
            yield return null;
        yield return new WaitForSecondsRealtime(2f);
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
        runtime.Begin(Object.FindFirstObjectByType<MainMenu>());
        var hostSeat = runtime.Party.Seats[0];
        while (!runtime.GetCharacter(hostSeat) && runtime.Status == null) yield return null;
        if (runtime.Status != null)
        {
            log.LogError($"BENCH: Airport unavailable. {runtime.Status}");
            Application.Quit();
            yield break;
        }
        yield return new WaitForSecondsRealtime(2f);

        Object.FindFirstObjectByType<AirportCheckInKiosk>().StartGame(0);
        while ((!GameHandler.IsOnIslandAndInitialized || !runtime.GetCharacter(hostSeat)) && runtime.Status == null)
            yield return null;
        if (runtime.Status != null)
        {
            log.LogError($"BENCH: island unavailable. {runtime.Status}");
            Application.Quit();
            yield break;
        }
        yield return new WaitForSecondsRealtime(10f);
        var host = runtime.GetCharacter(hostSeat);
        var camp = MapHandler.CurrentCampfire;
        Warp(host, CheckpointGate.FindSpawn(camp, 1));
        yield return new WaitForSecondsRealtime(2f);
        camp.DebugLight();
        var deadline = Time.realtimeSinceStartup + 20f;
        while (!runtime.CanJoin && Time.realtimeSinceStartup < deadline) yield return null;
        if (!runtime.CanJoin)
        {
            log.LogError($"BENCH: checkpoint unavailable (lit {camp.Lit}, in range {camp.EveryoneInRange()}).");
            Application.Quit();
            yield break;
        }
        yield return new WaitForSecondsRealtime(7f);
        var baseLook = host.data.lookValues;
        var path = OutputPath("frames.csv");
        if (path == null)
        {
            log.LogError("BENCH: pass an absolute -logFile path to write frame samples.");
            Application.Quit();
            yield break;
        }

        var pipeline = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        log.LogInfo(FormattableString.Invariant($"BENCH configuration: {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType}, VRAM capacity {SystemInfo.graphicsMemorySize} MB, {Screen.width}x{Screen.height}, {QualitySettings.names[QualitySettings.GetQualityLevel()]}, VSync {QualitySettings.vSyncCount}, target {Application.targetFrameRate}, URP scale {pipeline?.renderScale}, shadows {pipeline?.shadowDistance} m."));
        var sampler = new FrameSampler(log, path);
        if (Environment.GetCommandLineArgs().Any(arg => arg == "-peak-coop-perf" || arg == "-peak-coop-perf-api"))
        {
            yield return ComparePerformance(sampler, pipeline, baseLook);
            Application.Quit();
            yield break;
        }
        for (var count = 1; count <= 4; count++)
        {
            if (!runtime.CanJoin) break;
            if (count > 1)
            {
                var pad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
                yield return sampler.Sample(count, "checkpoint-join", 4f);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                if (runtime.Party.Seats.Count != count)
                {
                    log.LogError($"BENCH: seat P{count} unavailable. {runtime.Status}");
                    break;
                }
                yield return CheckSeatInput(pad, count);
            }
            AimTogether(baseLook, staggered: false);
            yield return new WaitForSecondsRealtime(1f);
            yield return sampler.Sample(count, "camp-aligned", 15f);
            AimTogether(baseLook, staggered: true);
            yield return new WaitForSecondsRealtime(1f);
            yield return sampler.Sample(count, "camp-varied", 15f);
        }
        log.LogInfo($"BENCH samples saved at {path}.");
        Application.Quit();
    }

    private System.Collections.IEnumerator ComparePerformance(FrameSampler sampler,
        UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset pipeline, Vector2 baseLook)
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        yield return Press(pad, GamepadButton.Start);
        if (runtime.Party.Seats.Count != 2 || !runtime.GetCamera(2))
        {
            log.LogError("PERF: P2 unavailable.");
            yield break;
        }
        var host = runtime.GetCamera(1);
        var guest = runtime.GetCamera(2);
        var seat = runtime.Party.Seats[1];
        var scout = runtime.GetCharacter(seat);
        var recorder = scout.GetComponent<Photon.Voice.Unity.Recorder>();
        var sourceRecording = recorder && recorder.RecordingEnabled;
        var hostOcclusion = host.useOcclusionCulling;
        var guestOcclusion = guest.useOcclusionCulling;
        log.LogInfo($"PERF: {Screen.currentResolution.refreshRateRatio.value:F2} Hz, {SystemInfo.graphicsMultiThreaded} graphics multithreaded, GPU occlusion {pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras}, guest recorder active {sourceRecording}; viewports {host.pixelRect}/{guest.pixelRect}.");
        try
        {
            if (Environment.GetCommandLineArgs().Contains("-peak-coop-perf-api"))
            {
                log.LogInfo($"PERF render API: {SystemInfo.graphicsDeviceType}, threading {SystemInfo.renderingThreadingMode}.");
                AimTogether(baseLook, staggered: false);
                yield return new WaitForSecondsRealtime(8f);
                CaptureSlice("aligned");
                yield return sampler.Sample(2, "api-aligned", 15f);
                AimTogether(baseLook, staggered: true);
                yield return new WaitForSecondsRealtime(4f);
                CaptureSlice("varied");
                yield return sampler.Sample(2, "api-varied", 15f);
                yield break;
            }
            yield return CheckSeatInput(pad, 2);
            for (var i = 0; i < 2; i++)
            {
                var varied = i == 1;
                var label = varied ? "varied" : "aligned";
                AimTogether(baseLook, staggered: varied);
                // ABBA to reduce sensitivity to background activity and boost clocks.
                foreach (var pass in new[] { "legacy-a", "optimized-a", "optimized-b", "legacy-b" })
                {
                    var legacy = pass.StartsWith("legacy", StringComparison.Ordinal);
                    PerformancePaths.UseLegacyInput = legacy;
                    if (recorder) recorder.RecordingEnabled = legacy || sourceRecording;
                    yield return new WaitForSecondsRealtime(4f);
                    yield return sampler.Sample(2, $"{label}-{pass}", 12f);
                }
            }
            PerformancePaths.UseLegacyInput = false;
            if (recorder) recorder.RecordingEnabled = sourceRecording;
            AimTogether(baseLook, staggered: false);
            host.useOcclusionCulling = guest.useOcclusionCulling = false;
            yield return new WaitForSecondsRealtime(4f);
            yield return sampler.Sample(2, "aligned-without-cpu-occlusion", 12f);
            host.useOcclusionCulling = hostOcclusion;
            guest.useOcclusionCulling = guestOcclusion;
            yield return new WaitForSecondsRealtime(4f);
            yield return sampler.Sample(2, "aligned-cpu-occlusion-control", 12f);
        }
        finally
        {
            PerformancePaths.UseLegacyInput = false;
            if (recorder) recorder.RecordingEnabled = sourceRecording;
            host.useOcclusionCulling = hostOcclusion;
            guest.useOcclusionCulling = guestOcclusion;
        }
    }

    private System.Collections.IEnumerator CheckSeatInput(Gamepad pad, int seatNumber)
    {
        var party = runtime.Party.Seats.Select(runtime.GetCharacter).ToArray();
        var look = party.Select(scout => scout.data.lookValues).ToArray();
        var guest = party[seatNumber - 1];
        var original = guest.Center;
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up, rightStick = Vector2.right * 0.4f });
        yield return new WaitForSecondsRealtime(0.55f);
        var delta = guest.Center - original;
        var guestMove = new Vector2(delta.x, delta.z).magnitude;
        var ownStick = guest.input.movementInput.sqrMagnitude > 0.5f;
        var guestLookDelta = Vector2.Distance(guest.data.lookValues, look[seatNumber - 1]);
        var others = party.Where((scout, index) => index != seatNumber - 1)
            .All(scout => scout.input.movementInput.sqrMagnitude < 0.001f &&
                Vector2.Distance(scout.data.lookValues, look[Array.IndexOf(party, scout)]) < 0.01f);
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Press(pad, GamepadButton.Start);
        log.LogInfo($"BENCH: P{seatNumber} isolated inputs {(ownStick && guestLookDelta > 1f && others ? "PASS" : "FAIL")}, horizontal move {guestMove:F2} m, look {guestLookDelta:F2}, repeated join rejected {runtime.Party.Seats.Count == party.Length}.");
    }

    private void AimTogether(Vector2 baseline, bool staggered)
    {
        var recalculate = BuildGuard.Method(typeof(Character), "RecalculateLookDirections");
        foreach (var seat in runtime.Party.Seats)
        {
            var scout = runtime.GetCharacter(seat);
            scout.data.lookValues = baseline + (staggered ? new Vector2((seat.Number - 1) * 90f + 45f, 0f) : Vector2.zero);
            recalculate.Invoke(scout, null);
        }
    }

    private static System.Collections.IEnumerator WaitForPhysics(float seconds)
    {
        var target = Time.fixedTime + seconds;
        var deadline = Time.realtimeSinceStartup + 5f;
        while (Time.fixedTime < target && Time.realtimeSinceStartup < deadline) yield return null;
    }

    private static System.Collections.IEnumerator Press(Gamepad pad, GamepadButton button)
    {
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
        yield return new WaitForSecondsRealtime(0.3f);
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return new WaitForSecondsRealtime(0.3f);
    }

    private static int InventorySize(Character scout) =>
        scout.player.itemSlots.Count(slot => !slot.IsEmpty()) +
        (scout.player.backpackSlot.IsEmpty() ? 0 : 1) + (scout.player.tempFullSlot.IsEmpty() ? 0 : 1);

    private static int CountGroundItems(ushort id) =>
        Item.ALL_ITEMS.Count(item => item && item.itemID == id && item.itemState == ItemState.Ground);

    private static void Warp(Character scout, Vector3 point) =>
        scout.photonView.RPC("WarpPlayerRPC", RpcTarget.All, point, false);

    private static void CaptureSlice(string phase)
    {
        var path = OutputPath($"{phase}.png");
        if (path != null) ScreenCapture.CaptureScreenshot(path);
    }

    private static string OutputPath(string suffix)
    {
        var args = Environment.GetCommandLineArgs();
        var logArgument = Array.IndexOf(args, "-logFile");
        if (logArgument < 0 || logArgument + 1 >= args.Length) return null;
        var logPath = args[logArgument + 1];
        return System.IO.Path.IsPathRooted(logPath) ? System.IO.Path.ChangeExtension(logPath, suffix) : null;
    }
}
