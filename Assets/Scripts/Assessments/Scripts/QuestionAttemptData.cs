using System;

public struct QuestionAttemptData
{
    public QuestionSO question;
    public int attemptNumber;
    public bool correct;
    public string response;
    public int earnedPoints;
    public bool isFinal;
}
