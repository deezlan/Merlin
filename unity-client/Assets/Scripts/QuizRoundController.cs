using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Newtonsoft.Json.Linq; // For JSON parsing

public class QuizRoundController : MonoBehaviourPunCallbacks
{
    private GameSceneUIManager uiManager;
    private HealthBarController healthController;
    private Dictionary<string, string> currentAnswers = new Dictionary<string, string>();
    private bool hasAnswered = false;

    private void Awake()
    {
        PhotonNetwork.NetworkingClient.EventReceived += OnEventReceived;

        // Mark self as ready
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "Ready", true } });

        // Master client checks if all players ready
        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(CheckAllPlayersReady());
    }

    // private IEnumerator CheckAllPlayersReady()
    // {
    //     while (true)
    //     {
    //         bool allReady = true;
    //         foreach (var p in PhotonNetwork.PlayerList)
    //         {
    //             if (!p.CustomProperties.ContainsKey("Ready") || !(bool)p.CustomProperties["Ready"])
    //             {
    //                 allReady = false;
    //                 break;
    //             }
    //         }

    //         if (allReady)
    //         {
    //             // Fire the StartRound event for everyone
    //             RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
    //             SendOptions sendOptions = new SendOptions { Reliability = true };
    //             PhotonNetwork.RaiseEvent(99, null, options, sendOptions);
    //             yield break;
    //         }

    //         yield return null;
    //     }
    // }
    private IEnumerator CheckAllPlayersReady()
    {
        while (true)
        {
            bool allReady = true;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (!p.CustomProperties.ContainsKey("Ready") || !(bool)p.CustomProperties["Ready"])
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
            {
                Debug.Log("All players ready. Starting first round!");
                RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                SendOptions sendOptions = new SendOptions { Reliability = true };
                PhotonNetwork.RaiseEvent(99, null, options, sendOptions);
                yield break; // stop coroutine
            }

            yield return null;
        }
    }


    private void Start()
    {
        uiManager = FindFirstObjectByType<GameSceneUIManager>();
        healthController = FindFirstObjectByType<HealthBarController>();
    }

    private void OnDestroy()
    {
        PhotonNetwork.NetworkingClient.EventReceived -= OnEventReceived;
    }

    public void StartRound()
    {
        hasAnswered = false;
        currentAnswers.Clear();

        string question = QuestionManager.Instance.GetCurrentQuestion();
        Debug.Log($"🧠 Starting new round. Question: {question}");
        uiManager.SetQuestion(question);
    }

    public void OnSubmitAnswerFromUI(string answer)
    {
        if (hasAnswered || string.IsNullOrEmpty(answer)) return;

        hasAnswered = true;
        object[] content = new object[] { PhotonNetwork.NickName, answer };
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        SendOptions sendOptions = new SendOptions { Reliability = true };

        PhotonNetwork.RaiseEvent(1, content, options, sendOptions);
    }

    private void OnEventReceived(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case 1: // received an answer
                HandleAnswerEvent(photonEvent);
                break;

            case 99: // start round sync event
                StartRound();
                break;
        }
    }


    private void HandleAnswerEvent(EventData photonEvent)
    {
        object[] data = (object[])photonEvent.CustomData;
        string playerName = (string)data[0];
        string answer = (string)data[1];

        currentAnswers[playerName] = answer;

        Debug.Log($"🗣️ {playerName} answered: {answer}");

        if (currentAnswers.Count >= 2)
        {
            StartCoroutine(GradeCurrentQuestion());
        }
    }

    private IEnumerator GradeCurrentQuestion()
    {
        string question = QuestionManager.Instance.GetCurrentQuestion();
        string summary = QuestionManager.Instance.Summary;

        StringBuilder fullText = new StringBuilder();
        fullText.AppendLine($"Question: {question}");
        foreach (var kvp in currentAnswers)
            fullText.AppendLine($"{kvp.Key}: {kvp.Value}");

        string jsonPayload = JsonUtility.ToJson(new GradeRequest { summary = summary, full_text = fullText.ToString() });

        using (UnityWebRequest www = new UnityWebRequest("http://127.0.0.1:8000/grade", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Grading failed: " + www.error);
            }
            else
            {
                Debug.Log("✅ Grading result: " + www.downloadHandler.text);
                ApplyDamageOrRewards(www.downloadHandler.text);

                // 🛑 Check if any player died after damage
                bool aDead = healthController.PlayerAHealth <= 0f;
                bool bDead = healthController.PlayerBHealth <= 0f;

                if (aDead || bDead)
                {
                    string winner = aDead ? "PlayerB" : "PlayerA";
                    uiManager.SetQuestion($"{winner} wins!"); // Show winner
                    StartCoroutine(ReturnToLobbyAfterDelay(5f)); // Wait 5 seconds
                    yield break; // Stop further rounds
                }

                // Proceed to next question if both alive
                QuestionManager.Instance.MoveToNextQuestion();

                if (QuestionManager.Instance.HasMoreQuestions())
                {
                    // Master client triggers the next question for all players
                    if (PhotonNetwork.IsMasterClient)
                    {
                        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
                        SendOptions sendOptions = new SendOptions { Reliability = true };
                        PhotonNetwork.RaiseEvent(99, null, options, sendOptions);
                    }
                }
                else
                {
                    uiManager.SetQuestion("🎉 Quiz Complete!");
                }
            }
        }
    }

    private void ApplyDamageOrRewards(string jsonResult)
    {
        GradeResponse result;
        try
        {
            result = JsonUtility.FromJson<GradeResponse>(jsonResult);
        }
        catch
        {
            Debug.LogError("Failed to parse grading JSON!");
            return;
        }

        float aDamage = result.B.total;
        float bDamage = result.A.total;

        Debug.Log($"PlayerA takes {aDamage}% damage, PlayerB takes {bDamage}% damage");

        if (healthController != null)
        {
            healthController.ApplyDamage("PlayerA", aDamage, true);
            healthController.ApplyDamage("PlayerB", bDamage, true);

            // Check if anyone died
            bool aDead = healthController.PlayerAHealth <= 0f;
            bool bDead = healthController.PlayerBHealth <= 0f;

            if (aDead || bDead)
            {
                string winner = aDead ? "PlayerB" : "PlayerA";
                uiManager.SetQuestion($"{winner} wins!"); // Show winner
                StartCoroutine(ReturnToLobbyAfterDelay(5f)); // 5 seconds delay
                return; // stop further rounds
            }
        }
        else
        {
            Debug.LogError("HealthController not found in scene!");
        }
    }

    private IEnumerator ReturnToLobbyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Load lobby scene, keep Photon connection
        PhotonNetwork.LoadLevel("Lobby");
    }


    [System.Serializable]
    public class GradeRequest
    {
        public string summary;
        public string full_text;
    }

    [System.Serializable]
    public class PlayerGrade
    {
        public int accuracy;
        public int completeness;
        public int clarity;
        public int relevance;
        public int total;
        public string feedback;
    }

    [System.Serializable]
    public class GradeResponse
    {
        public PlayerGrade A;
        public PlayerGrade B;
    }
}
