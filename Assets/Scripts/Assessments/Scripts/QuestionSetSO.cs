using UnityEngine;

[CreateAssetMenu(menuName = "Assessment/Question Set")]
public class QuestionSetSO : ScriptableObject
{
    public QuestionSO[] questions;
    public bool shuffleQuestions = true;
}
