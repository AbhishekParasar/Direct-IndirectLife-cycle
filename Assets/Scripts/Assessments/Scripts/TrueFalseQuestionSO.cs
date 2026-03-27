using UnityEngine;

[CreateAssetMenu(menuName = "Assessment/TrueFalse Question")]
public class TrueFalseQuestionSO : QuestionSO
{
    // Assuming QuestionSO provides the base structure (e.g., 'prompt', 'points')

    [Header("Answer")]
    public bool answerTrue = true;

    [Header("VO")]
    [Tooltip("Narration that plays when the question appears.")]
    public AudioClip questionVO;

    [Tooltip("Hover VO when user hovers the TRUE button. Used as click VO to say 'True'.")]
    public AudioClip trueHoverVO;

    [Tooltip("Hover VO when user hovers the FALSE button. Used as click VO to say 'False'.")]
    public AudioClip falseHoverVO;

    [Tooltip("Optional: click VO (If this is set, it might override or combine with the hover clips being used for clicks).")]
    public AudioClip clickVO;

    public override QuestionType Type => QuestionType.TrueFalse;
}