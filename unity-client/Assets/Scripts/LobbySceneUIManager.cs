using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using SFB; // StandaloneFileBrowser
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

public class LobbySceneUIManager : MonoBehaviourPunCallbacks
{
    [Header("UI Elements")]
    [SerializeField] private Button uploadSlideButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button disconnectButton;

    [Header("Backend Settings")]
    [SerializeField] private string pipelineUrl = "http://127.0.0.1:8000/pipeline";
    [SerializeField] private bool returnCleaned = false;
    [SerializeField] private int nHint = 0;

    private bool slidesUploaded = false;
    private bool bothPlayersReady = false;

    private void Awake()
    {
        uploadSlideButton.onClick.AddListener(OnUploadButtonClick);
        startButton.onClick.AddListener(OnStartButtonClick);
        disconnectButton.onClick.AddListener(OnDisconnectButtonClick);

        // Disable until both conditions are met
        startButton.interactable = false;
    }

    private void UpdateStartButtonState()
    {
        // Only enable start if both slides are uploaded and 2 players are in room
        startButton.interactable = slidesUploaded && bothPlayersReady;
    }

    private void OnUploadButtonClick()
    {
        var extensions = new[] { new ExtensionFilter("Slides", "pdf", "pptx") };
        var paths = StandaloneFileBrowser.OpenFilePanel("Select Lecture Slides", "", extensions, false);

        if (paths.Length > 0 && File.Exists(paths[0]))
        {
            Debug.Log("Selected file: " + paths[0]);
            StartCoroutine(UploadPipeline(paths[0]));
        }
        else
        {
            Debug.LogWarning("No file selected or invalid path.");
        }
    }

    private IEnumerator UploadPipeline(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        string fileName = Path.GetFileName(filePath);
        string mimeType = Path.GetExtension(fileName).ToLower() == ".pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.presentationml.presentation";

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", fileData, fileName, mimeType);
        form.AddField("return_cleaned", returnCleaned ? "true" : "false");
        if (nHint > 0) form.AddField("n_hint", nHint.ToString());

        using (UnityWebRequest www = UnityWebRequest.Post(pipelineUrl, form))
        {
            www.SetRequestHeader("accept", "application/json");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("❌ Pipeline upload failed: " + www.error);
                Debug.Log("Server response: " + www.downloadHandler.text);
                yield break;
            }

            Debug.Log("✅ Server response: " + www.downloadHandler.text);
            var pipelineResp = JsonUtility.FromJson<PipelineResponse>(www.downloadHandler.text);
            if (pipelineResp == null)
            {
                Debug.LogError("Failed to parse pipeline response!");
                yield break;
            }

            // Store questions and summary
            var questionsList = new List<string>(pipelineResp.questions.Split('\n'));
            QuestionManager.Instance.SetQuiz(pipelineResp.summary, questionsList);

            slidesUploaded = true;
            Debug.Log($"📘 Stored {questionsList.Count} questions and summary.");

            UpdateStartButtonState();
        }
    }

    private void OnStartButtonClick()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("GamePlay");
        }
    }

    private void OnDisconnectButtonClick()
    {
        PhotonNetwork.LeaveRoom();
    }

    // --- PHOTON CALLBACKS ---

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"👥 Player joined: {newPlayer.NickName}. Player count = {PhotonNetwork.CurrentRoom.PlayerCount}");
        CheckPlayerCount();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"🚪 Player left: {otherPlayer.NickName}. Player count = {PhotonNetwork.CurrentRoom.PlayerCount}");
        CheckPlayerCount();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"✅ Joined room: {PhotonNetwork.CurrentRoom.Name}, players = {PhotonNetwork.CurrentRoom.PlayerCount}");
        CheckPlayerCount();
    }

    private void CheckPlayerCount()
    {
        bothPlayersReady = PhotonNetwork.CurrentRoom.PlayerCount == 2;
        UpdateStartButtonState();
    }

    [System.Serializable]
    public class PipelineResponse
    {
        public string summary;
        public string questions;
        public int count;
        public string cleaned; // optional
    }
}
