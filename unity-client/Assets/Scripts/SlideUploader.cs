using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using SFB; // StandaloneFileBrowser

public class SlideUploader : MonoBehaviour
{
    [Header("Backend Settings")]
    [SerializeField] private string pipelineUrl = "http://127.0.0.1:8000/pipeline";

    [Header("Optional Pipeline Settings")]
    [SerializeField] private bool returnCleaned = false;  // include cleaned text
    [SerializeField] private int nHint = 0;               // optional quiz question hint count

    public void OnUploadButtonClick()
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

    IEnumerator UploadPipeline(string filePath)
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

            Debug.Log("Server response: " + www.downloadHandler.text);

            var pipelineResp = JsonUtility.FromJson<PipelineResponse>(www.downloadHandler.text);

            if (pipelineResp == null)
            {
                Debug.LogError("Failed to parse pipeline response as JSON!");
                yield break;
            }

            // Store summary + questions
            var questionsList = new List<string>(pipelineResp.questions.Split('\n'));
            QuestionManager.Instance.SetQuiz(pipelineResp.summary, questionsList);
            Debug.Log($"Stored {questionsList.Count} questions and summary for gameplay.");

            // Optionally load game scene if not already
            // Example using UnityEngine.SceneManagement
            // UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");

            // Start first round automatically (ensure QuizRoundController exists in the scene)
            var quizController = Object.FindFirstObjectByType<QuizRoundController>();

            if (quizController != null)
            {
                quizController.StartRound();
            }

            if (!string.IsNullOrEmpty(pipelineResp.summary))
                Debug.Log($"Summary: {pipelineResp.summary.Substring(0, Mathf.Min(100, pipelineResp.summary.Length))}...");

            if (!string.IsNullOrEmpty(pipelineResp.questions))
                Debug.Log($"Questions ({pipelineResp.count}):\n{pipelineResp.questions}");

            if (returnCleaned && !string.IsNullOrEmpty(pipelineResp.cleaned))
                Debug.Log($"Cleaned text preview: {pipelineResp.cleaned.Substring(0, Mathf.Min(100, pipelineResp.cleaned.Length))}...");
        }
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