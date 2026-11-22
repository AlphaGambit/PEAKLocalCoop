using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.Unity;
using UnityEngine;
using NetworkPlayer = Photon.Realtime.Player;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class OfflineScouts
{
    internal sealed class Scout
    {
        public Guid Id { get; }
        public NetworkPlayer Actor { get; }
        public Player Inventory { get; set; }
        public Character Character { get; set; }

        public Scout(Guid id, NetworkPlayer actor)
        {
            Id = id;
            Actor = actor;
        }
    }

    private readonly ConstructorInfo actorConstructor = AccessTools.Constructor(typeof(NetworkPlayer),
        new[] { typeof(string), typeof(int), typeof(bool), typeof(Hashtable) })
        ?? throw new MissingMethodException(typeof(NetworkPlayer).FullName, ".ctor");
    private readonly FieldInfo prefix = BuildGuard.Field(typeof(PhotonNetwork), "currentLevelPrefix");
    private readonly FieldInfo registeredPlayers = BuildGuard.Field(typeof(PlayerHandler), "m_playerLookup");
    private readonly FieldInfo registeredCharacters = BuildGuard.Field(typeof(PlayerHandler), "m_playerCharacterLookup");
    private readonly FieldInfo sessionData = BuildGuard.Field(typeof(PlayerHandler), "m_playerSessionData");
    private readonly Dictionary<int, Scout> scouts = new Dictionary<int, Scout>();
    private Room room;

    public bool IsActive => room != null && PhotonNetwork.OfflineMode && ReferenceEquals(PhotonNetwork.CurrentRoom, room);
    public IEnumerable<Scout> Guests => scouts.Values;

    public void SetRoom(Room offlineRoom)
    {
        if (offlineRoom == null || !PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient
            || !ReferenceEquals(offlineRoom, PhotonNetwork.CurrentRoom))
            throw new InvalidOperationException("Eksperimen memerlukan room offline milik P1.");
        room = offlineRoom;
    }

    public Scout Find(Character character)
    {
        if (!IsActive || !character || !character.photonView)
            return null;
        return scouts.TryGetValue(character.photonView.OwnerActorNr, out var scout)
            && scout.Character == character ? scout : null;
    }

    public Character FindActor(int actor) => scouts.TryGetValue(actor, out var scout) ? scout.Character : null;

    public bool Contains(NetworkPlayer actor) =>
        IsActive && actor != null && scouts.TryGetValue(actor.ActorNumber, out var scout)
        && ReferenceEquals(scout.Actor, actor);

    public Scout Create(int seatNumber, Guid id, Transform anchor, Vector3 position)
    {
        if (!IsActive || !anchor || !Character.localCharacter || !Player.localPlayer)
            throw new InvalidOperationException("Karakter host dan room offline harus siap.");
        if (seatNumber < 2 || seatNumber > 4 || room.GetPlayer(seatNumber) != null)
            throw new InvalidOperationException($"Actor untuk P{seatNumber} sudah dipakai atau di luar rentang.");
        var userId = $"peak.local.{id:N}";
        var actor = (NetworkPlayer)actorConstructor.Invoke(new object[]
        {
            $"Local P{seatNumber}", seatNumber, false,
            new Hashtable { ["UserID"] = userId,
                ["PlatformFamily"] = PhotonNetwork.LocalPlayer.CustomProperties["PlatformFamily"] }
        });
        AccessTools.PropertySetter(typeof(NetworkPlayer), nameof(NetworkPlayer.UserId)).Invoke(actor, new object[] { userId });
        room.StorePlayer(actor);
        var scout = new Scout(id, actor);
        scouts.Add(seatNumber, scout);
        try
        {
            scout.Inventory = Spawn<Player>("Player", actor, Vector3.zero, Quaternion.identity);
            scout.Character = Spawn<Character>("Character", actor, position, anchor.rotation);
            scout.Character.data.spawnPoint = anchor;
            scout.Character.data.currentStamina = scout.Character.GetMaxStamina();
            if (scout.Character.player != scout.Inventory ||
                !scout.Character.photonView.IsMine || !scout.Inventory.photonView.IsMine ||
                !PlayerHandler.TryGetCharacter(seatNumber, out var registered) || registered != scout.Character)
                throw new InvalidOperationException($"PEAK gagal menghubungkan karakter dan inventory P{seatNumber}.");
            if (GameHandler.IsOnIsland)
                scout.Character.photonView.RPC("RPCA_InitializeScoutReport", RpcTarget.All);
            return scout;
        }
        catch
        {
            Remove(scout);
            throw;
        }
    }

    private T Spawn<T>(string prefab, NetworkPlayer actor, Vector3 position, Quaternion rotation) where T : Component
    {
        var obj = PhotonNetwork.PrefabPool.Instantiate(prefab, position, rotation);
        if (!obj) throw new InvalidOperationException($"Prefab {prefab} tidak ditemukan.");
        try
        {
            if (obj.activeSelf) throw new InvalidOperationException($"Prefab pool mengaktifkan {prefab} sebelum ViewID disetel.");
            var views = obj.GetComponentsInChildren<PhotonView>(true);
            if (views.Length == 0) throw new InvalidOperationException($"Prefab {prefab} tidak memiliki PhotonView.");
            var instantiationId = 0;
            foreach (var view in views)
            {
                var id = PhotonNetwork.AllocateViewID(actor.ActorNumber);
                if (instantiationId == 0) instantiationId = id;
                view.InstantiationId = instantiationId;
                view.Prefix = (byte)prefix.GetValue(null);
                BuildGuard.Field(typeof(PhotonView), "isRuntimeInstantiated").SetValue(view, true);
                view.ViewID = id;
            }

            // A guest in this offline room has no remote listener or separate microphone.
            if (obj.TryGetComponent<Recorder>(out var recorder)) recorder.RecordingEnabled = false;

            // PEAK's Awake must see a remote owner to leave its host globals alone.
            obj.SetActive(true);
            foreach (var view in views)
                view.ControllerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;

            foreach (var callback in obj.GetComponents<IPunInstantiateMagicCallback>())
                callback.OnPhotonInstantiate(new PhotonMessageInfo(actor, PhotonNetwork.ServerTimestamp, views[0]));
            return obj.GetComponent<T>() ?? throw new InvalidOperationException($"Prefab {prefab} tidak memiliki {typeof(T).Name}.");
        }
        catch
        {
            obj.SetActive(false);
            foreach (var view in obj.GetComponentsInChildren<PhotonView>(true))
                ClearView(view, actor.ActorNumber);
            PhotonNetwork.PrefabPool.Destroy(obj);
            throw;
        }
    }

    public void Remove(Scout scout)
    {
        scouts.Remove(scout.Actor.ActorNumber);
        var players = PlayerHandler.Instance;
        if (players != null)
        {
            RemoveKey(registeredPlayers, players, scout.Actor.ActorNumber);
            RemoveKey(registeredCharacters, players, scout.Actor.ActorNumber);
            RemoveKey(sessionData, players, scout.Actor.UserId);
        }
        DestroyScoutObject(scout.Character);
        DestroyScoutObject(scout.Inventory);
        room?.Players.Remove(scout.Actor.ActorNumber);
    }

    private static void RemoveKey(FieldInfo field, PlayerHandler owner, object key) =>
        ((System.Collections.IDictionary)field.GetValue(owner)).Remove(key);

    private static void DestroyScoutObject(Component component)
    {
        if (!component) return;
        component.gameObject.SetActive(false);
        if (component is Character character) Character.AllCharacters.Remove(character);
        foreach (var view in component.GetComponentsInChildren<PhotonView>(true))
        {
            if (!view) continue;
            ClearView(view, view.OwnerActorNr);
        }
        Object.Destroy(component.gameObject);
    }

    private static void ClearView(PhotonView view, int owner)
    {
        view.ViewID = 0;
        if (PhotonNetwork.CurrentRoom?.GetPlayer(owner) != null)
        {
            view.OwnerActorNr = owner;
            view.ControllerActorNr = owner;
        }
    }

    public void Clear()
    {
        foreach (var scout in new List<Scout>(scouts.Values)) Remove(scout);
        room = null;
    }
}
