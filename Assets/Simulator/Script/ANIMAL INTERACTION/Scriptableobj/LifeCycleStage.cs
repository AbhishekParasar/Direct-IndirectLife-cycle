using UnityEngine;

[System.Serializable]
public class LifeCycleStage
{
    [TextArea(3, 5)]
    public string stageText; // The description for this stage

    public AudioClip stageAudioClip; // The voiceover audio for this stage

    public string progressionObjectName;
}