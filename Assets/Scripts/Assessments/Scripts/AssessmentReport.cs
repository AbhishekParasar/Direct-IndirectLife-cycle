using System;
using System.Collections.Generic;

[Serializable]
public class QuestionAttemptEntry
{
    public int attemptNumber;
    public bool correct;
    public string response;
    public int earnedPoints;
    public bool wasFinal;
}

[Serializable]
public class QuestionSummary
{
    public string questionId;
    public string prompt;
    public string questionType;
    public int maxAttempts;
    public int attemptsUsed;
    public int wrongAttempts;
    public bool answeredCorrectly;
    public int earnedPoints;
    public List<QuestionAttemptEntry> attempts = new();
}

[Serializable]
public class AssessmentReport
{
    public string assessmentId;
    public int totalQuestions;
    public int totalCorrect;
    public int totalWrong;
    public int totalWrongAttempts;
    public int totalScore;
    public List<QuestionSummary> questions = new();
}
