using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UIHoverVO : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler
{
    [Header("Clips")]
    public AudioClip hoverClip;
    public AudioClip clickClip;

    [Header("Behavior")]
    public bool playOnHover = true;
    public bool playOnClick = false;

    [Tooltip("Replace = interrupt current hover VO; IgnoreIfPlaying = skip while one is playing")]
    public HoverPolicy policy = HoverPolicy.Replace;

    [Tooltip("Respect narration gate: block option UI sounds while question VO is playing")]
    public bool respectQuestionGate = true;

    [Tooltip("Global throttle across ALL buttons (seconds)")]
    public float throttleSeconds = 0.10f;

    [Range(0f, 1f)] public float hoverVolume = 1f;
    [Range(0f, 1f)] public float clickVolume = 1f;
    [Range(0f, 1f)] public float duckTo = 0.4f;

    // Optional: if true, and user is hovering during narration, auto-play once when narration ends
    public bool queueAfterQuestionIfHovering = false;

    bool isHovering = false;
    System.Action queuedHandler;

    void OnEnable()
    {
        // clean (re)subscribe only if queue is desired
        if (queueAfterQuestionIfHovering)
            VOBuss.OnQuestionEnded += HandleQuestionEnded;
    }

    void OnDisable()
    {
        if (queueAfterQuestionIfHovering)
            VOBuss.OnQuestionEnded -= HandleQuestionEnded;
        queuedHandler = null;
        isHovering = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        // NEW: if current question already answered, do not play hover audio
        if (AssessmentManager.Instance != null && AssessmentManager.Instance.IsCurrentQuestionAnswered())
        {
            // ensure queued handler doesn't stay around
            queuedHandler = null;
            return;
        }

        // ⭐ MODIFIED: Check if playOnHover is true, but DO NOT play audio here.
        if (!playOnHover || !hoverClip) return;

        // Gate: if narration is playing…
        if (respectQuestionGate && VOBuss.QuestionPlaying)
        {
            // …queue the audio to play when the question narration ends, if enabled.
            if (queueAfterQuestionIfHovering)
                queuedHandler = () => TryPlayHover();
            return;
        }

        // ⭐ AUDIO REMOVED FROM HOVER: We do not call TryPlayHover() here.
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        queuedHandler = null; // cancel any queued play
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[UIHoverVO] Click detected! playOnClick={playOnClick}, hoverClip={(hoverClip != null ? hoverClip.name : "NULL")}");

        // ⭐ REMOVED the "question already answered" check - let audio play!

        // Check if we have a clip to play
        if (!hoverClip)
        {
            Debug.LogWarning("[UIHoverVO] No hoverClip assigned to button!");
            return;
        }

        // Check if click audio is enabled
        if (!playOnClick)
        {
            Debug.Log("[UIHoverVO] playOnClick is FALSE - skipping audio");
            return;
        }

        // ⭐ Play hoverClip immediately - IGNORE question gate for button clicks
        Debug.Log($"[UIHoverVO] ✓ PLAYING AUDIO: {hoverClip.name} at volume {clickVolume}");
        VOBuss.PlayHover(hoverClip, clickVolume, duckTo, policy, throttleSeconds, false);  // false = don't wait for question
    }

    void HandleQuestionEnded()
    {
        if (!queueAfterQuestionIfHovering) return;
        if (!isHovering) { queuedHandler = null; return; }

        // If the question was answered while queued, don't play
        if (AssessmentManager.Instance != null && AssessmentManager.Instance.IsCurrentQuestionAnswered())
        {
            queuedHandler = null;
            return;
        }

        // Play once after narration ends if still hovering
        queuedHandler?.Invoke();
        queuedHandler = null;
    }

    void TryPlayHover()
    {
        // This method plays the hoverClip using all its configured parameters.
        VOBuss.PlayHover(hoverClip, hoverVolume, duckTo, policy, throttleSeconds, respectQuestionGate);
    }
}