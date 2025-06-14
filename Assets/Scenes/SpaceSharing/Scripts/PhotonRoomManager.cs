// Copyright (c) Meta Platforms, Inc. and affiliates.

using Photon.Pun;
using Photon.Realtime;

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Assertions;

using Hashtable = ExitGames.Client.Photon.Hashtable;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;


/// <summary>
/// Manages Photon Room creation and maintenance, including custom data synchronized (shared) to the Room.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class PhotonRoomManager : MonoBehaviourPunCallbacks
{
    public static readonly RoomOptions RoomOptions = new()
    {
        IsVisible = true,
        IsOpen = true,
        BroadcastPropsChangeToAll = true,
        MaxPlayers = 0,       // no defined limit
        EmptyRoomTtl = 60000, // 1 minute
        PlayerTtl = 600000,   // 10 minutes
    };


    const string k_PubRoomsKey = "rooms";
    const string k_LastPubberKey = "pubber";
    const byte k_PacketFormat = 2;
    const byte k_PacketFormatWithPose = 3;
    const int k_PacketHeaderSz = sizeof(byte);
    const int k_UuidSize = 16;
    const int k_Pose3DSize = 7 * sizeof(float);


    // Runtime fields
    byte[] m_PacketBuf;
    string m_LastRoomName;


    #region [Monobehaviour Methods]

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        OnApplicationQuit();
    }

    IEnumerator OnApplicationPause(bool pause)
    {
        if (pause)
        {
            StopAllCoroutines();
            yield break;
        }

        yield return null;

        while (Application.internetReachability == NetworkReachability.NotReachable)
            yield return null;

        var ui = Sampleton.BaseUI;

        if (PhotonNetwork.InRoom)
        {
            if (ui)
                ui.DisplayRoomPanel();
            yield break;
        }

        if (ui)
            ui.DisplayLobbyPanel();

        if (PhotonNetwork.InLobby)
            yield break;

        if (PhotonNetwork.ReconnectAndRejoin())
        {
            Sampleton.Log($">> PhotonNetwork.ReconnectAndRejoin()");
            yield break;
        }

        if (PhotonNetwork.IsConnected && PhotonNetwork.JoinLobby())
        {
            Sampleton.Log($">> PhotonNetwork.JoinLobby()");
            yield break;
        }

        if (PhotonNetwork.ConnectUsingSettings())
        {
            Sampleton.Log($">> PhotonNetwork.ConnectUsingSettings()");
            yield break;
        }

        Sampleton.Error($">> ERR: PhotonNetwork failed to (re)connect after app resumed.");
    }

    void OnApplicationQuit()
    {
        if (PhotonNetwork.InRoom)
        {
            // Call LeaveRoom(false) explicitly, so intentionally
            // leaving doesn't make your "inactive" slot linger:
            PhotonNetwork.LeaveRoom(becomeInactive: false);
        }

        PhotonNetwork.Disconnect();
    }

    #endregion [Monobehaviour Methods]


    #region [Photon Callbacks]

    public override void OnConnectedToMaster()
    {
        Sampleton.Log($"Photon::OnConnectedToMaster, CloudRegion='{PhotonNetwork.CloudRegion}'");

        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Sampleton.Log($"Photon::OnJoinedLobby");

        if (Sampleton.BaseUI is LocalSpaceSharingUI ui)
        {
            ui.NotifyLobbyAvailable(true);
            ui.DisplayLobbyPanel();
        }
    }

    public override void OnLeftLobby()
    {
        Sampleton.Log($"Photon::OnLeftLobby");

        if (Sampleton.BaseUI is LocalSpaceSharingUI ui)
            ui.NotifyLobbyAvailable(false);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (Sampleton.BaseUI is LocalSpaceSharingUI ui)
        {
            ui.NotifyRoomListUpdate(null);
        }

        switch (cause)
        {
            case DisconnectCause.DisconnectByServerLogic:
            case DisconnectCause.DisconnectByDisconnectMessage:
            case DisconnectCause.DnsExceptionOnConnect:
            case DisconnectCause.ServerAddressInvalid:
            case DisconnectCause.InvalidRegion:
            case DisconnectCause.InvalidAuthentication:
            case DisconnectCause.AuthenticationTicketExpired:
            case DisconnectCause.CustomAuthenticationFailed:
            case DisconnectCause.OperationNotAllowedInCurrentState:
            case DisconnectCause.DisconnectByOperationLimit:
            case DisconnectCause.MaxCcuReached:
                Sampleton.Error($"Photon:OnDisconnected: {cause}\n- will NOT attempt to automatically ReconnectAndRejoin().");
                return;

            case DisconnectCause.Exception:
            case DisconnectCause.ExceptionOnConnect:
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.ServerTimeout:
            case DisconnectCause.DisconnectByServerReasonUnknown:
                Sampleton.Warn($"Photon::OnDisconnected: {cause}\n+ attempting auto ReconnectAndRejoin() in 2 seconds...");
                Sampleton.DelayCall(2f, () => _ = PhotonNetwork.ReconnectAndRejoin() || PhotonNetwork.ConnectUsingSettings());
                return;

            default:
            case DisconnectCause.None:
            case DisconnectCause.DisconnectByClientLogic:
                Sampleton.Log($"Photon::OnDisconnected: {cause}");
                return;
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (returnCode == ErrorCode.GameDoesNotExist && !string.IsNullOrEmpty(m_LastRoomName))
        {
            string room = m_LastRoomName;
            m_LastRoomName = null;
            if (PhotonNetwork.JoinOrCreateRoom(room, RoomOptions, TypedLobby.Default))
            {
                Sampleton.Warn(
                    $"Photon::OnJoinRoomFailed: \"{message}\" ({returnCode})\n" +
                    $"+ Creating a new \"{room}\"..."
                );
                return;
            }
        }

        Sampleton.Error($"Photon::OnJoinRoomFailed: \"{message}\" ({returnCode})");

        if (PhotonNetwork.InLobby)
        {
            if (Sampleton.GetActiveUI(out var ui))
                ui.DisplayLobbyPanel();
            return;
        }

        _ = PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedRoom()
    {
        var room = PhotonNetwork.CurrentRoom;

        Sampleton.Log($"Photon::OnJoinedRoom: {room.Name}");

        m_LastRoomName = room.Name;

        if (room.CustomProperties.TryGetValue(k_PubRoomsKey, out var box) && box is byte[] bytes)
        {
            ReceiveSharedData(bytes);
        }

        if (Sampleton.GetActiveUI(out var ui))
            ui.DisplayRoomPanel();
    }

    public override void OnLeftRoom()
    {
        Sampleton.Log("Photon::OnLeftRoom"); // mainly for log consistency

        if (Sampleton.GetActiveUI(out var ui))
            ui.DisplayLobbyPanel();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Sampleton.Log($"Photon::OnPlayerEnteredRoom: {newPlayer}");

        if (Sampleton.BaseUI is LocalSpaceSharingUI ui)
            ui.NotifyRoomUsersUpdated();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer.IsInactive)
        {
            Sampleton.Log($"Photon::OnPlayerLeftRoom: {otherPlayer} has gone inactive.");
            return;
        }

        Sampleton.Log($"Photon::OnPlayerLeftRoom: {otherPlayer} has left.");

        if (Sampleton.BaseUI is LocalSpaceSharingUI ui)
            ui.NotifyRoomUsersUpdated();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (Sampleton.BaseUI is LocalSpaceSharingUI ui)
            ui.NotifyRoomListUpdate(roomList);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        Sampleton.Log($"Photon::OnRoomPropertiesUpdate(n={changedProps.Count})");
        foreach (var entry in changedProps)
        {
            Sampleton.Log($"+ {entry.Key}: {entry.Value}");
        }

        if (!changedProps.TryGetValue(k_PubRoomsKey, out var box))
            return;

        var bytes = box as byte[];

        Assert.IsNotNull(bytes, $"changedProps[{k_PubRoomsKey}] is byte[]");

        var currProps = PhotonNetwork.CurrentRoom.CustomProperties;

        if (!(changedProps.TryGetValue(k_LastPubberKey, out box) || currProps.TryGetValue(k_LastPubberKey, out box))
            || box is not int pubberNubber)
        {
            Sampleton.Error($"ERR: Non-empty room properties should have a value for [\"{k_LastPubberKey}\"]!");
            return;
        }

        if (pubberNubber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            Sampleton.Log($"* this data is yours.");
            return;
        }

        var pubber = PhotonNetwork.CurrentRoom.GetPlayer(pubberNubber);
        Sampleton.Log($"NEW shared data from Player {pubber}:");

        ReceiveSharedData(bytes);
    }

    #endregion [Photon Callbacks]


    #region [Send and read room data]

    public void PublishRoomData(Guid groupUuid, ICollection<Guid> roomUuids, Pose? floorPose = null)
    {
        Sampleton.Log($"{nameof(PublishRoomData)}: {roomUuids.Count} rooms");

        const int kNumGroupIds = 1;

        int size = k_PacketHeaderSz + k_UuidSize * (kNumGroupIds + roomUuids.Count);
        if (floorPose.HasValue)
            size += k_Pose3DSize;

        Array.Resize(ref m_PacketBuf, size);

        var rawBytes = m_PacketBuf;
        rawBytes[0] = floorPose.HasValue ? k_PacketFormatWithPose
                                         : k_PacketFormat;
        int offset = k_PacketHeaderSz;

        // Group UUID
        PackUuid(groupUuid, rawBytes, ref offset);

        // Room UUIDs
        foreach (var uuid in roomUuids)
        {
            PackUuid(uuid, rawBytes, ref offset);
        }

        // Floor Pose
        if (floorPose.HasValue)
            PackPose(floorPose.Value, rawBytes, ref offset);

        Assert.AreEqual(rawBytes.Length, offset, $"{nameof(rawBytes)}.Length");

        var pubProps = new Hashtable
        {
            [k_PubRoomsKey] = rawBytes,
            [k_LastPubberKey] = PhotonNetwork.LocalPlayer.ActorNumber,
        };

        var room = PhotonNetwork.CurrentRoom;
        Assert.IsNotNull(room, "PhotonNetwork.CurrentRoom");

        var pubBouncer = default(Hashtable);
        if (room.CustomProperties.TryGetValue(k_LastPubberKey, out var pubber))
        {
            pubBouncer = new Hashtable
            {
                [k_LastPubberKey] = pubber,
            };
        }

        // prefer room properties so that the data can be queried anytime after sync
        room.SetCustomProperties(pubProps, expectedProperties: pubBouncer);
    }

    static void PackUuid(Guid uuid, byte[] packet, ref int offset)
    {
        var toSpan = new Span<byte>(packet, offset, k_UuidSize);
        bool ok = uuid.TryWriteBytes(toSpan);
        Assert.IsTrue(ok, "uuid.TryWriteBytes(toSpan)");
        offset += k_UuidSize;
    }

    static void PackPose(Pose pose, byte[] packet, ref int offset)
    {
        var toSpan = new Span<byte>(packet, offset, k_Pose3DSize);

        bool ok = MemoryMarshal.TryWrite(toSpan, ref pose);
        Assert.IsTrue(ok, "MemoryMarshal.TryWrite(toSpan, ref pose)");

        offset += toSpan.Length;
    }

    static bool UnpackPose(byte[] packet, ref int offset, out Pose pose)
    {
        pose = default;
        if (offset + k_Pose3DSize > packet.Length)
            return false;

        var fromSpan = new ReadOnlySpan<byte>(packet, offset, k_Pose3DSize);

        bool ok = MemoryMarshal.TryRead(fromSpan, out pose);
        Assert.IsTrue(ok, "MemoryMarshal.TryRead(fromSpan, out pose)");

        offset += k_Pose3DSize;
        return Quaternion.Dot(pose.rotation, pose.rotation) > 1f - Vector4.kEpsilon;
    }

    void ReceiveSharedData(byte[] rawPacket)
    {
        Sampleton.Log($"{nameof(ReceiveSharedData)}({nameof(rawPacket)}[{(rawPacket is null ? "null" : rawPacket.Length)}])");

        if (rawPacket is null)
        {
            Sampleton.Error($"  - ERR: {nameof(rawPacket)} was null!");
            return;
        }

        if (rawPacket.Length == 0)
        {
            Sampleton.Error($"  - ERR: {nameof(rawPacket)} was empty!");
            return;
        }

        int tailSize;
        switch (rawPacket[0])
        {
            case k_PacketFormat:
                tailSize = 0;
                break;
            case k_PacketFormatWithPose:
                tailSize = k_Pose3DSize;
                break;
            default:
                Sampleton.Error($"  - ERR: invalid packet format: {rawPacket[0]}");
                return;
        }

        Sampleton.Log($"  + packet format: {rawPacket[0]}");

        if ((rawPacket.Length - tailSize) % k_UuidSize != k_PacketHeaderSz)
        {
            Sampleton.Error($"  - ERR: invalid packet size: {rawPacket.Length}");
            return;
        }

        int nUuids = (rawPacket.Length - k_PacketHeaderSz - tailSize) / k_UuidSize;
        if (nUuids == 0)
        {
            Sampleton.Warn($"  - SKIP: uuid block is empty.");
            return;
        }

        Sampleton.Log($"  + valid packet received!");

        var roomUuids = new Guid[nUuids - 1];
        var groupUuid = Guid.Empty;
        var curRoomId = default(Guid?);
        int offset = k_PacketHeaderSz;

        for (var i = 0; i < nUuids; i++)
        {
            // using a ReadOnlySpan is efficient because the packet array never needs to be copied into buffers
            var uuid = new Guid(new ReadOnlySpan<byte>(rawPacket, start: offset, length: k_UuidSize));
            offset += k_UuidSize;

            Debug.LogFormat( // not logging w/ Sampleton because that would be too verbose
                LogType.Log,
                LogOption.NoStacktrace,
                context: this,
                $"{nameof(ReceiveSharedData)}: unpacked {(i == 0 ? "Group" : "Room")}: {uuid}"
            );

            Assert.AreNotEqual(Guid.Empty, uuid, "uuid != null");

            // First GUID is the group
            if (i == 0)
            {
                groupUuid = uuid;
                continue;
            }

            // et cetera = rooms
            roomUuids[i - 1] = uuid;
            curRoomId ??= uuid;
        }

        Sampleton.Log($"{nameof(ReceiveSharedData)} DONE. 1 group, {roomUuids.Length} room(s) received.\n+ group: {groupUuid}");
        if (curRoomId.HasValue)
            Sampleton.Log($"+ current room: {curRoomId.Value}");

        // Floor pose
        if (curRoomId.HasValue && rawPacket[0] == k_PacketFormatWithPose && UnpackPose(rawPacket, ref offset, out var floorPose))
        {
            MRSceneManager.SetHostAlignment((curRoomId.Value, floorPose));
            Sampleton.Log($"+ SetHostAlignment(room: {curRoomId.Value.Brief()}, floorPose: {floorPose.ToString("F2")})");
        }
        else
        {
            MRSceneManager.SetHostAlignment(null);
            Sampleton.Warn($"- SetHostAlignment(null) - no current room or host floor pose");
        }

        MRSceneManager.SetSharedSceneUuids(roomUuids, groupUuid);

        if (Sampleton.PlayerFace.IsInLoadedRoom())
        {
            Sampleton.Log(
                $"-> Because your face is already in a room, the shared scene won't auto-load." +
                " You can attempt to load it manually using the \"Load Shared\" button."
            );
        }
        else
        {
            MRSceneManager.LoadSharedScene();
        }
    }

    #endregion [Send and read room data]

}
