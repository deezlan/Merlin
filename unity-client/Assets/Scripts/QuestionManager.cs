using System.Collections.Generic;
using UnityEngine;

public class QuestionManager : MonoBehaviour
{
    public static QuestionManager Instance { get; private set; }

    public List<string> Questions { get; private set; } = new List<string>();
    public string Summary { get; private set; } = "";
    public int CurrentIndex { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetQuiz(string summary, List<string> questions)
    {
        Summary = summary;
        Questions = questions;
        CurrentIndex = 0;
    }

    public string GetCurrentQuestion()
    {
        if (CurrentIndex >= 0 && CurrentIndex < Questions.Count)
            return Questions[CurrentIndex];
        return null;
    }

    public void MoveToNextQuestion()
    {
        if (CurrentIndex < Questions.Count - 1)
            CurrentIndex++;
        else
            Debug.Log("All questions completed!");
    }

    public bool HasMoreQuestions()
    {
        return CurrentIndex < Questions.Count - 1;
    }
}
