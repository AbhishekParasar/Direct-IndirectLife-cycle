using UnityEngine;

public enum QuestionType { MCQ, TrueFalse, MatchPairs }

public abstract class QuestionSO : ScriptableObject
{
    [TextArea] public string prompt;
    public int points = 1;
    public abstract QuestionType Type { get; }
}
