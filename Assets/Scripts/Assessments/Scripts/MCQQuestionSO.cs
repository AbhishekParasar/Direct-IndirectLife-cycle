using UnityEngine;

[CreateAssetMenu(menuName = "Assessment/MCQ Question")]
public class MCQQuestionSO : QuestionSO
{


    public string[] options;
    public int correctIndex = 0;
    public bool shuffleOptions = true;
    public override QuestionType Type => QuestionType.MCQ;
    // add at top: using UnityEngine;
public AudioClip questionVO;      // plays when MCQ appears
public AudioClip[] optionVO;      // align with 'options' (by original index)

}
