using TMPro;
using UnityEngine;
using UnityEngine.UI;

// NOTE: This script assumes the existence of:
// 1. A QuestionAttemptData struct/class.
// 2. An AssessmentManager static instance with IsQuestionInputLocked() method.
// 3. A UIHoverVO component script that listens to mouse events and calls VOBuss.PlayHover.

public class TrueFalseView : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text promptText;
    public Button trueButton;
    public Button falseButton;
    public Button retryButton;      // NEW

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color correctColor = new Color(0.25f, 0.85f, 0.35f, 1f);
    public Color wrongColor = new Color(0.95f, 0.30f, 0.30f, 1f);

    [Header("Option Audio")]
    public bool playOptionsSequentially = true;
    public float delayBetweenOptions = 0.4f;
    // 🆕 Updated to a lighter/vibrant blue to match the screenshot (approx #0080FF)
    public Color highlightColor = new Color(0f, 0.5f, 1f, 1f); 

    TrueFalseQuestionSO _data;
    System.Action<QuestionAttemptData> _report;
    int _attemptCount;
    int _maxAttempts = 3;
    bool _completed;
    bool _answeredCorrectly;
    Coroutine _sequentialAudioCoroutine;

    void Awake()
    {
        // 🆕 UNIFICATION: Force use of the global shared color from AssessmentManager
        if (AssessmentManager.Instance != null)
        {
            highlightColor = AssessmentManager.Instance.sharedHighlightColor;
        }

        if (trueButton)
        {
            var img = trueButton.GetComponent<Image>();
            if (img) normalColor = img.color;
        }
    }



    void OnDestroy()
    {
        if (AssessmentManager.Instance != null)
        {
            AssessmentManager.Instance.OnQuestionInputUnlocked -= OnUnlocked;
        }
    }

    void OnUnlocked()
    {
        if (AssessmentManager.Instance != null)
        {
            AssessmentManager.Instance.OnQuestionInputUnlocked -= OnUnlocked;
        }

        if (!_completed)
        {
            Debug.Log("TrueFalseView received unlock event. Starting sequential audio.");
            
            if (playOptionsSequentially)
            {
                if (_sequentialAudioCoroutine != null) StopCoroutine(_sequentialAudioCoroutine);
                _sequentialAudioCoroutine = StartCoroutine(PlayOptionsSequentially());
            }
            else
            {
                trueButton.interactable = falseButton.interactable = true;
            }
        }
    }

    public void Bind(
        TrueFalseQuestionSO data,
        System.Action<QuestionAttemptData> onAttempt,
        int attemptsUsed = 0,
        int maxAttempts = 3,
        bool completed = false,
        bool answeredCorrectly = false)
    {
        _data = data;
        _report = onAttempt;
        _maxAttempts = Mathf.Max(1, maxAttempts);
        _attemptCount = Mathf.Clamp(attemptsUsed, 0, _maxAttempts);
        _completed = completed || (_attemptCount >= _maxAttempts && !answeredCorrectly);
        _answeredCorrectly = answeredCorrectly;

        if (promptText) promptText.text = data.prompt;

        // ---- Play question VO ----
        // ---- Play question VO ----
        // FIX: Avoid double-playback. If AssessmentManager handles audio/locking, 
        // playing it again here on VOBuss creates a race condition that blocks valid option audio.
        bool amHandlesAudio = AssessmentManager.Instance != null 
                              && AssessmentManager.Instance.lockViewUntilAudioEnd 
                              && AssessmentManager.Instance.questionAudioSource != null;

        if (!amHandlesAudio && _data.questionVO) 
            VOBuss.PlayQuestion(_data.questionVO);

        // listeners
        trueButton.onClick.RemoveAllListeners();
        falseButton.onClick.RemoveAllListeners();
        trueButton.onClick.AddListener(() => Pick(trueButton, true));
        falseButton.onClick.AddListener(() => Pick(falseButton, false));

        // ⭐ MODIFIED: Use the hover clip as the click clip to say "True" or "False" on click.
        // We pass the same clip for both hover and click actions.
        SetupHoverVO(trueButton, _data.trueHoverVO, _data.trueHoverVO);
        SetupHoverVO(falseButton, _data.falseHoverVO, _data.falseHoverVO);

        // reset visuals
        SetColor(trueButton, normalColor);
        SetColor(falseButton, normalColor);
        
        bool isLocked = AssessmentManager.Instance != null && AssessmentManager.Instance.IsQuestionInputLocked();
        if (isLocked && AssessmentManager.Instance != null)
        {
            AssessmentManager.Instance.OnQuestionInputUnlocked += OnUnlocked;
            trueButton.interactable = falseButton.interactable = false;
        }
        else
        {
             trueButton.interactable = falseButton.interactable = !_completed;
             // If not locked and not completed, play immediately? 
             // Logic in MCQView suggests if not locked we might need to manually trigger if playOptionsSequentially is true.
             // However, Bind is usually called at start. If not locked, we can start.
             if (!_completed && playOptionsSequentially && !isLocked)
             {
                 if (_sequentialAudioCoroutine != null) StopCoroutine(_sequentialAudioCoroutine);
                 _sequentialAudioCoroutine = StartCoroutine(PlayOptionsSequentially());
             }
        }

        if (retryButton)
        {
            retryButton.gameObject.SetActive(false);
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(ResetForRetry);
            if (_completed) retryButton.gameObject.SetActive(false);
        }

        if (_completed) HighlightCorrect();
    }

    System.Collections.IEnumerator PlayOptionsSequentially()
    {
        // 🆕 Safety wait for lock state to update
        yield return null;
        
        // 🆕 Wait until question VO finishes
        if (AssessmentManager.Instance != null)
        {
            yield return new WaitWhile(() => AssessmentManager.Instance.IsQuestionInputLocked());
        }

        // 1. Play True
        if (_data.trueHoverVO)
        {
            SetColor(trueButton, highlightColor);
            VOBuss.PlayHover(_data.trueHoverVO);
            yield return new WaitForSeconds(_data.trueHoverVO.length);
            if (!_completed) SetColor(trueButton, normalColor);
            yield return new WaitForSeconds(delayBetweenOptions);
        }

        if (_completed) yield break;

        // 2. Play False
        if (_data.falseHoverVO)
        {
             SetColor(falseButton, highlightColor);
             VOBuss.PlayHover(_data.falseHoverVO);
             yield return new WaitForSeconds(_data.falseHoverVO.length);
             if (!_completed) SetColor(falseButton, normalColor);
             yield return new WaitForSeconds(delayBetweenOptions);
        }

        // Enable
        if (!_completed)
        {
            trueButton.interactable = falseButton.interactable = true;
        }
    }

    void SetupHoverVO(Button b, AudioClip hoverClip, AudioClip clickClip)
    {
        var hv = b.GetComponent<UIHoverVO>();
        if (!hv) hv = b.gameObject.AddComponent<UIHoverVO>();

        hv.hoverClip = hoverClip;
        hv.playOnHover = hoverClip != null;

        // The clickClip is now the clip that says "True" or "False"
        hv.clickClip = clickClip;
        hv.playOnClick = clickClip != null;
    }

    void Pick(Button picked, bool pickedTrue)
    {
        // ⭐ INPUT LOCK CHECK (Blocks user selection until question audio finishes)
        if (AssessmentManager.Instance != null &&
            AssessmentManager.Instance.IsQuestionInputLocked())
        {
            Debug.Log("Input is locked. Please wait for the question audio to finish.");
            return;
        }
        // ⭐ END LOCK CHECK

        if (_completed) return;

        bool correct = (pickedTrue == _data.answerTrue);

        trueButton.interactable = false;
        falseButton.interactable = false;

        // Increase attempt count
        _attemptCount = Mathf.Min(_attemptCount + 1, _maxAttempts);

        if (correct)
        {
            SetColor(picked, correctColor);
            _completed = true;
            _answeredCorrectly = true;

            // ✅ POINTS ONLY IF CORRECT ON FIRST ATTEMPT
            bool isFirstAttemptCorrect = (_attemptCount == 1);
            int earned = isFirstAttemptCorrect ? _data.points : 0;

            _report?.Invoke(new QuestionAttemptData
            {
                question = _data,
                attemptNumber = _attemptCount,
                correct = true,
               // correct = (earned > 0),
                response = pickedTrue ? "True" : "False",
                earnedPoints = earned,
                isFinal = true
            });

            if (retryButton)
                retryButton.gameObject.SetActive(false);
        }
        else
        {
            // ❌ WRONG ANSWER
            SetColor(picked, wrongColor);
            SetColor(_data.answerTrue ? trueButton : falseButton, correctColor);

            bool isFinal = (_attemptCount >= _maxAttempts);

            _report?.Invoke(new QuestionAttemptData
            {
                question = _data,
                attemptNumber = _attemptCount,
                correct = false,
                response = pickedTrue ? "True" : "False",
                earnedPoints = 0,
                isFinal = isFinal
            });

            if (isFinal)
            {
                _completed = true;
                if (retryButton)
                    retryButton.gameObject.SetActive(false);
            }
            else if (retryButton)
            {
                Invoke("InvokeEnableRetryButton", 4f);
            }
        }
    }


    void InvokeEnableRetryButton()
    {
        retryButton.gameObject.SetActive(true);
    }

    void ResetForRetry()
    {
        // ⭐ INPUT LOCK CHECK
        if (AssessmentManager.Instance != null && AssessmentManager.Instance.IsQuestionInputLocked())
        {
            Debug.Log("Input is locked. Cannot reset until audio finishes.");
            return;
        }
        // ⭐ END LOCK CHECK

        if (_completed) return;
        SetColor(trueButton, normalColor);
        SetColor(falseButton, normalColor);
        trueButton.interactable = falseButton.interactable = true;
        if (retryButton) retryButton.gameObject.SetActive(false);
    }

    static void SetColor(Button b, Color c)
    {
        var g = b.targetGraphic;
        if (g) g.color = c;
        var img = b.GetComponent<Image>();
        if (img) img.color = c;
    }

    void HighlightCorrect()
    {
        SetColor(_data.answerTrue ? trueButton : falseButton, correctColor);
        SetColor(_data.answerTrue ? falseButton : trueButton, normalColor);
        trueButton.interactable = falseButton.interactable = false;
    }
}