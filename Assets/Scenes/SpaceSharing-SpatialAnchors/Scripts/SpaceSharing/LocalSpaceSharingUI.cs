// Copyright (c) Meta Platforms, Inc. and affiliates.

using Photon.Pun;
using Photon.Realtime;

using System.Collections;
using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

using TMPro;


public class LocalSpaceSharingUI : BaseUI
{
    [Header(nameof(LocalSpaceSharingUI))]
    [SerializeField]
    [Tooltip(
        "If disabled, \"(inactive)\" Photon Players will not " +
        "appear in the current Room's \"Users:\" list.\n\n" +
        "If enabled, the per-Room \"(inactive)\" status and CustomProperties " +
        "for each Photon Player will be printed in the list.\n\n" +
        "Note: \"(inactive)\" Players are not considered in the Room anymore; " +
        "their slot remains reserved for up to a minute in case they return.")]
    bool m_VerboseUserList;

    [FormerlySerializedAs("createRoomButton")]
    [SerializeField]
    Button m_CreateRoomBtn;

    [FormerlySerializedAs("joinRoomButton")]
    [SerializeField]
    Button m_FindRoomsBtn;

    [FormerlySerializedAs("roomLayoutPanel")]
    [SerializeField]
    GameObject m_RoomListPanel;

    [FormerlySerializedAs("roomLayoutPanelRowPrefab")]
    [SerializeField]
    GameObject m_RoomListItemTemplate;

    [FormerlySerializedAs("statusText")]
    [SerializeField]
    TMP_Text m_StatusText;

    [FormerlySerializedAs("roomText")]
    [SerializeField]
    TMP_Text m_RoomText;

    [FormerlySerializedAs("userText")]
    [SerializeField]
    TMP_Text m_UserText;


    public void OnCreateRoomBtn()
    {
        if (Sampleton.ConnectMethod != ConnectMethod.Photon)
        {
            Sampleton.Error($"{nameof(OnCreateRoomBtn)}: Not supported in this scene!");
            NotifyLobbyAvailable(false);
            return;
        }

        if (!PhotonNetwork.IsConnected)
        {
            Sampleton.Warn($"{nameof(OnCreateRoomBtn)}: No Photon connection!\n- Attempting to reconnect... (retry this button later)");
            PhotonNetwork.ConnectUsingSettings();
            return;
        }

        Sampleton.Log($"{nameof(OnCreateRoomBtn)}:");

        string username = Sampleton.GetNickname();
        string newRoomName = $"{username}'s room";

        Sampleton.Log($"+ Attempting to host a new room named \"{newRoomName}\"...");

        if (PhotonNetwork.JoinOrCreateRoom(newRoomName, PhotonRoomManager.RoomOptions, TypedLobby.Default))
            return;

        Sampleton.Error($"ERR: Room creation request not sent to server!");
    }

    public void OnFindRoomsBtn()
    {
        if (Sampleton.ConnectMethod != ConnectMethod.Photon)
        {
            Sampleton.Error($"{nameof(OnFindRoomsBtn)}: Not supported in this scene!");
            NotifyLobbyAvailable(false);
            return;
        }

        Sampleton.Log(nameof(OnFindRoomsBtn));

        if (!PhotonNetwork.IsConnected)
        {
            if (PhotonNetwork.ConnectUsingSettings())
                Sampleton.Warn("PhotonNetwork was disconnected. Wait a few moments before trying this again...");
            else
                Sampleton.Error("Cannot connect to PhotonNetwork with the app's configured settings.");
            return;
        }

        m_RoomListPanel.SetActive(true);
    }

    public void OnJoinRoomBtn(TMP_Text roomName)
    {
        if (!roomName)
        {
            Sampleton.Error($"{nameof(OnJoinRoomBtn)}: Missing reference to room name component!");
            return;
        }

        if (string.IsNullOrEmpty(roomName.text))
        {
            Sampleton.Error($"{nameof(OnJoinRoomBtn)}: given room name is empty!");
            return;
        }

        Sampleton.Log($"{nameof(OnJoinRoomBtn)}: \"{roomName.text}\"");

        _ = Sampleton.GetNickname();

        PhotonNetwork.JoinRoom(roomName.text);
    }


    public void OnLoadLocalSceneButtonPressed()
    {
        Sampleton.Log(nameof(OnLoadLocalSceneButtonPressed));
        MRSceneManager.LoadOrScanLocalScene();
    }

    public void OnLoadSharedSceneButtonPressed()
    {
        Sampleton.Log(nameof(OnLoadSharedSceneButtonPressed));
        MRSceneManager.LoadSharedScene();
    }

    public void OnShareLocalSceneButtonPressed()
    {
        Sampleton.Log(nameof(OnShareLocalSceneButtonPressed));
        MRSceneManager.ShareLocalScene();
    }


    public void NotifyLobbyAvailable(bool connected)
    {
        if (m_CreateRoomBtn)
            m_CreateRoomBtn.interactable = connected;

        if (m_FindRoomsBtn)
            m_FindRoomsBtn.interactable = connected;

        if (!connected)
            NotifyRoomListUpdate(null);
    }

    public void NotifyRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (Transform roomTransform in m_RoomListPanel.transform)
        {
            if (roomTransform.gameObject != m_RoomListItemTemplate)
                Destroy(roomTransform.gameObject);
        }

        if (roomList is null || roomList.Count == 0)
            return;

        foreach (var room in roomList)
        {
            var entry = Instantiate(m_RoomListItemTemplate, m_RoomListPanel.transform);
            entry.GetComponentInChildren<TMP_Text>().text = room.Name;
            entry.SetActive(true);
        }
    }

    public void NotifyRoomUsersUpdated()
    {
        UpdateUserList();
    }


    //
    // private impl.

    static readonly StringBuilder s_TextBuf = new();

    void UpdateLobbyInteractability()
        => NotifyLobbyAvailable(PhotonNetwork.InLobby);

    void OnEnable()
    {
        OnDisplayRoom += UpdateRoomName;

        UpdateLobbyInteractability();
        if (Sampleton.ConnectMethod == ConnectMethod.Photon)
            OnDisplayLobby += UpdateLobbyInteractability;
    }

    void OnDisable()
    {
        OnDisplayRoom -= UpdateRoomName;
        if (Sampleton.ConnectMethod == ConnectMethod.Photon)
            OnDisplayLobby -= UpdateLobbyInteractability;
    }

    protected override IEnumerator Start()
    {
        yield return base.Start();

        if (m_RoomListItemTemplate.scene == gameObject.scene) // (not a prefab ref)
        {
            m_RoomListItemTemplate.SetActive(false);
        }

        m_RoomListPanel.SetActive(false);

        yield return UpdateUI(new WaitForSecondsRealtime(1f));
    }

    IEnumerator UpdateUI(object interval)
    {
        bool logNetworkWarning = true;
        while (this)
        {
            if (ShowingLobbyPanel)
            {
                UpdateLobbyInteractability();
            }

            if (ShowingRoomPanel)
            {
                UpdateRoomName();
                UpdateStatus(ref logNetworkWarning);
                UpdateUserList();
            }

            yield return interval;
        }
    }

    void UpdateRoomName()
    {
        s_TextBuf.Clear();
        s_TextBuf.Append("Room: ");
        s_TextBuf.Append(PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name
                                              : "(none)");
        m_RoomText.SetText(s_TextBuf);
    }

    void UpdateStatus(ref bool logNetworkWarning)
    {
        s_TextBuf.Clear();
        s_TextBuf.Append("Status: ");
        s_TextBuf.Append(PhotonNetwork.NetworkClientState);
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            s_TextBuf.Append("\n<color=red>- NetworkReachability.NotReachable</color>");
            if (logNetworkWarning)
            {
                Sampleton.Warn($"WARNING: Application.internetReachability == {Application.internetReachability}");
                logNetworkWarning = false;
            }
        }
        else
        {
            if (!logNetworkWarning)
            {
                Sampleton.Log($"Internet reachability restored! ({Application.internetReachability})");
                logNetworkWarning = true;
            }
        }

        m_StatusText.SetText(s_TextBuf);
    }

    void UpdateUserList()
    {
        var players = new List<Player>(PhotonNetwork.PlayerList); // pre-sorted
        var deduper = new HashSet<string>();

        int i = players.Count;
        while (i-- > 0)
        {
            // exploit the fact that the bottom-most occurence will always be
            // the most up-to-date instance for the same username:
            if (!deduper.Add(players[i].NickName))
                players.RemoveAt(i);
        }

        s_TextBuf.Clear();
        s_TextBuf.Append("Users:");

        foreach (var player in players)
        {
            if (m_VerboseUserList)
                s_TextBuf.Append("\n- ").Append(player.ToStringFull());
            else if (!player.IsInactive)
                s_TextBuf.Append("\n- ").Append(player);
        }

        m_UserText.SetText(s_TextBuf);
    }

}
