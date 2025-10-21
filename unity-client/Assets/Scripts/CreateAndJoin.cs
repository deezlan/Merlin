using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class CreateAndJoin : MonoBehaviourPunCallbacks
{
    public TMP_InputField input_Create;
    public TMP_InputField input_Join;

    public void CreateRoom()
    {
        // Set nickname before creating room
        PhotonNetwork.NickName = "PlayerA";
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true
        };
        PhotonNetwork.CreateRoom(input_Create.text, roomOptions, TypedLobby.Default);
    }

    public void JoinRoom()
    {
        // Set nickname before joining room
        PhotonNetwork.NickName = "PlayerB";
        PhotonNetwork.JoinRoom(input_Join.text);
    }

    public override void OnJoinedRoom()
    {
        // PhotonNetwork.LoadLevel("GamePlay");
        Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        Debug.Log($"Players in Room: {PhotonNetwork.CurrentRoom.PlayerCount}");

        // Optional: display player role
        Debug.Log($"You are {PhotonNetwork.NickName}");

        // When both players are present, load the Lobby scene
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("2nd Cond");
            PhotonNetwork.LoadLevel("Lobby");
        }
    }
}
