using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GameSceneUIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_InputField answerInputField;
    [SerializeField] private Button submitButton;

    private void Awake()
    {
        // Optionally assign button listener here
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(() =>
            {
                var quizController = FindObjectOfType<QuizRoundController>();
                if (quizController != null)
                    quizController.OnSubmitAnswerFromUI(answerInputField.text);
            });
        }
    }

    public void SetQuestion(string question)
    {
        if (questionText != null)
            questionText.text = question;

        if (answerInputField != null)
            answerInputField.text = "";
    }

    public void ClearInput()
    {
        if (answerInputField != null)
            answerInputField.text = "";
    }
}
