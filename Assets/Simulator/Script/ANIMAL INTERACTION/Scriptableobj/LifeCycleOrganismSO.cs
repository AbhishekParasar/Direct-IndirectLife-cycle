using UnityEngine;

[CreateAssetMenu(fileName = "NewOrganismData", menuName = "LifeCycle/Organism Data")]
public class LifeCycleOrganismSO : ScriptableObject
{
    [Header("Organism Identification")]
    public string organismName = "New Organism";
    public AudioClip nameAudioClip; // Audio clip for the organism's name (played on click)
    public AudioClip exploreClickAudioClip; // Audio clip played when clicking the label to explore more

    [Header("Life Cycle Stages")]
    public LifeCycleStage[] stages;

    public string progressionObjectName;
    // Total stages count derived from the array
    public int TotalStages => stages.Length;
}