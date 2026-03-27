using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class InputLockAudioManager : MonoBehaviour
{
    public static InputLockAudioManager Instance { get; private set; }

    [Header("Audio Sources to Monitor")]
    [Tooltip("Drag all AudioSource components that play VOs that should lock input.")]
    public List<AudioSource> voAudioSources = new List<AudioSource>();

    public bool IsInputLocked => IsAnyAudioPlaying();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            // Removed DontDestroyOnLoad for standard component behavior.
        }
    }

    public void PlayRandomVO(AudioClip[] clips)
    {
        if (voAudioSources.Count == 0 || clips == null || clips.Length == 0) return;

        // Check for null source before accessing isPlaying
        AudioSource availableSource = voAudioSources.FirstOrDefault(source => source != null && !source.isPlaying);

        if (availableSource != null)
        {
            AudioClip clipToPlay = clips[Random.Range(0, clips.Length)];
            availableSource.clip = clipToPlay;
            availableSource.Play();
            Debug.Log($"[InputLockManager] Playing clip '{clipToPlay.name}'. Input is now LOCKED.");
        }
        else
        {
            Debug.LogWarning("[InputLockManager] All monitored audio sources are currently busy.");
        }
    }

    private bool IsAnyAudioPlaying()
    {
        // Check for null source before accessing isPlaying
        return voAudioSources.Any(source => source != null && source.isPlaying);
    }
}