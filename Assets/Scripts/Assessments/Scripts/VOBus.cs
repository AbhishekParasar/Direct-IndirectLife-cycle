using System.Collections;
using UnityEngine;

// Shared policy for hover handling
public enum HoverPolicy { Replace, IgnoreIfPlaying }

public class VOBuss : MonoBehaviour
{
    static VOBuss I;

    // Events (optional): fire when a question VO starts/ends
    public static event System.Action OnQuestionStarted;
    public static event System.Action OnQuestionEnded;

    AudioSource mainVO;                 // question/prompt narration, and feedback
    AudioSource uiVO;                   // UI hover/click sounds
    float mainDefaultVol = 1f;

    int hoverEpoch = 0;                 // latest hover token
    Coroutine hoverCo;
    float nextHoverAllowedAt = 0f;      // global hover throttle

    int questionEpoch = 0;              // tracks the current question session
    Coroutine questionWatchCo;

    public static bool QuestionPlaying => I && I.mainVO && I.mainVO.isPlaying;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        mainVO = gameObject.AddComponent<AudioSource>();
        mainVO.playOnAwake = false; mainVO.loop = false; mainVO.spatialBlend = 0f;

        uiVO = gameObject.AddComponent<AudioSource>();
        uiVO.playOnAwake = false; uiVO.loop = false; uiVO.spatialBlend = 0f;
    }

    static void Ensure()
    {
        if (I) return;
        var go = new GameObject("VOBus");
        I = go.AddComponent<VOBuss>();
        DontDestroyOnLoad(go);
    }

    // — Question VO (replaces any ongoing narration)
    public static void PlayQuestion(AudioClip clip, float volume = 1f)
    {
        if (!clip) return;
        Ensure();

        if (I.questionWatchCo != null) I.StopCoroutine(I.questionWatchCo);

        I.mainVO.Stop();
        I.mainDefaultVol = Mathf.Clamp01(volume);
        I.mainVO.volume = I.mainDefaultVol;
        I.mainVO.clip = clip;

        I.questionEpoch++;
        OnQuestionStarted?.Invoke();

        I.mainVO.Play();
        I.questionWatchCo = I.StartCoroutine(I.QuestionWatchCo(I.questionEpoch));
    }

    // — Feedback VO (used for correct/incorrect responses or button names)
    public static void PlayFeedback(AudioClip clip, float volume = 1f)
    {
        if (!clip) return;
        Ensure();

        // Stop any current question/feedback narration before playing the new feedback
        if (I.questionWatchCo != null) I.StopCoroutine(I.questionWatchCo);
        I.mainVO.Stop();

        I.mainVO.volume = Mathf.Clamp01(volume);
        I.mainVO.clip = clip;
        I.mainVO.Play();
    }

    IEnumerator QuestionWatchCo(int myEpoch)
    {
        // wait until the question VO (for this epoch) stops
        while (mainVO.isPlaying && myEpoch == questionEpoch)
            yield return null;

        if (myEpoch == questionEpoch)
            OnQuestionEnded?.Invoke();
    }

    // — Hover/Click VO: gated while question VO is playing
    public static void PlayHover(
        AudioClip clip,
        float uiVolume = 1f,
        float duckTo = 0.4f,
        HoverPolicy policy = HoverPolicy.Replace,
        float throttleSeconds = 0.10f,
        bool respectQuestionGate = true)
    {
        if (!clip) return;
        Ensure();

        // *** Gate: ignore option UI audio while narration is playing ***
        if (respectQuestionGate && I.mainVO.isPlaying)
            return;

        // Global throttle across all hover sources
        if (Time.unscaledTime < I.nextHoverAllowedAt) return;
        I.nextHoverAllowedAt = Time.unscaledTime + Mathf.Max(0f, throttleSeconds);

        if (policy == HoverPolicy.IgnoreIfPlaying && I.uiVO.isPlaying)
            return;

        I.hoverEpoch++;
        if (policy == HoverPolicy.Replace) I.uiVO.Stop();
        if (I.hoverCo != null) I.StopCoroutine(I.hoverCo);
        I.hoverCo = I.StartCoroutine(I.PlayHoverCo(clip, uiVolume, duckTo, I.hoverEpoch));
    }

    IEnumerator PlayHoverCo(AudioClip clip, float uiVolume, float duckTo, int myEpoch)
    {
        float prev = mainVO.volume;
        mainVO.volume = Mathf.Clamp01(duckTo);

        uiVO.volume = Mathf.Clamp01(uiVolume);
        uiVO.clip = clip;
        uiVO.Play();

        while (uiVO.isPlaying && myEpoch == hoverEpoch)
            yield return null;

        if (myEpoch == hoverEpoch)
            mainVO.volume = mainDefaultVol;
    }
}