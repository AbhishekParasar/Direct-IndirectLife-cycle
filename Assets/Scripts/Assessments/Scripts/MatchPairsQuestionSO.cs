using UnityEngine;

[System.Serializable] public struct MatchPair { public string left; public string right; }

[CreateAssetMenu(menuName = "Assessment/Match Pairs Question")]
public class MatchPairsQuestionSO : QuestionSO
{
    [Header("Audio")]
    

    public MatchPair[] pairs;
    public bool shuffleRight = true;
    public override QuestionType Type => QuestionType.MatchPairs;
    // add at top: using UnityEngine;
public AudioClip questionVO;   // plays when MatchPairs appears
public AudioClip[] leftVO;     // align with pairs[i].left
public AudioClip[] rightVO;    // align with pairs[i].right (original order)

}
