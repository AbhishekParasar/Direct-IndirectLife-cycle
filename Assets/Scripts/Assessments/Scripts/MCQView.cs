// MCQView.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MCQView : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text promptText;
    public RectTransform optionsParent;
    public Button optionButtonPrefab;
    public Button retryButton;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color correctColor = new Color(0.25f, 0.85f, 0.35f, 1f);
    public Color wrongColor = new Color(0.95f, 0.30f, 0.30f, 1f);
    public Color highlightColor = new Color(0f, 0.5f, 1f, 1f); // 🆕 Vibrant Blue
    
    [Header("Option Audio")]
    [Tooltip("Play option audios sequentially after question narration?")]
    public bool playOptionsSequentially = true;
    [Tooltip("Delay between each option audio (seconds)")]
    public float delayBetweenOptions = 0.4f;

    MCQQuestionSO _data;
    System.Action<QuestionAttemptData> _report;
    public int _attemptCount;
    int _maxAttempts = 3;
    bool _completed;
    bool _answeredCorrectly;
    Coroutine _sequentialAudioCoroutine;

    struct Opt
    {
        public Button btn;
        public int idx;
    }
    readonly List<Opt> _opts = new();

    void Awake()
    {
        // 🆕 UNIFICATION: Force use of the global shared color from AssessmentManager
        if (AssessmentManager.Instance != null)
        {
            highlightColor = AssessmentManager.Instance.sharedHighlightColor;
        }

        if (optionButtonPrefab)
        {
            var img = optionButtonPrefab.GetComponent<Image>();
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
            Debug.Log("MCQView received unlock event. Starting sequential audio.");
            
            if (playOptionsSequentially && _data != null && _data.optionVO != null)
            {
                if (_sequentialAudioCoroutine != null) StopCoroutine(_sequentialAudioCoroutine);
                _sequentialAudioCoroutine = StartCoroutine(PlayOptionsSequentially());
            }
            else
            {
                foreach (var o in _opts)
                {
                    o.btn.interactable = true;
                }
            }
        }
    }

    public void Bind(
        MCQQuestionSO data,
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

        foreach (Transform c in optionsParent) Destroy(c.gameObject);
        _opts.Clear();

        var items = new List<(string text, int idx)>();
        for (int i = 0; i < data.options.Length; i++) items.Add((data.options[i], i));
        if (data.shuffleOptions) Shuffle(items);

        bool isLocked = AssessmentManager.Instance != null && AssessmentManager.Instance.IsQuestionInputLocked();

        if (isLocked && AssessmentManager.Instance != null)
        {
            AssessmentManager.Instance.OnQuestionInputUnlocked += OnUnlocked;
        }

        foreach (var it in items)
        {
            var b = Instantiate(optionButtonPrefab, optionsParent);
            b.GetComponentInChildren<TMP_Text>().text = it.text;

            int captured = it.idx;

            AudioClip optionClip = (data.optionVO != null && captured >= 0 && captured < data.optionVO.Length)
                                 ? data.optionVO[captured] : null;

            b.onClick.AddListener(() => OnPick(b, captured, optionClip));

            SetButtonColor(b, normalColor);
            b.interactable = !_completed && !isLocked;

            _opts.Add(new Opt { btn = b, idx = captured });
        }

        if (_completed)
        {
            HighlightCorrectOnly();
            if (retryButton) retryButton.gameObject.SetActive(false);
        }
        else if (retryButton)
        {
            retryButton.gameObject.SetActive(false);
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(ResetForRetry);
        }
    }

   void OnPick(Button picked, int pickedIdx, AudioClip selectionClip)
{
    // ⭐ INPUT LOCK CHECK
    if (AssessmentManager.Instance != null &&
        AssessmentManager.Instance.IsQuestionInputLocked())
    {
        Debug.Log("Input is currently locked. Waiting for audio to finish.");
        return;
    }

    if (_completed) return;

    if (selectionClip != null)
    {
        VOBuss.PlayHover(selectionClip);
    }

    bool correct = (pickedIdx == _data.correctIndex);

    foreach (var o in _opts)
        o.btn.interactable = false;

    // Increase attempt count
    _attemptCount = Mathf.Min(_attemptCount + 1, _maxAttempts);

    if (correct)
    {
        SetButtonColor(picked, correctColor);

        _completed = true;
        _answeredCorrectly = true;

        // ✅ POINTS ONLY IF CORRECT ON FIRST ATTEMPT
        bool isFirstAttemptCorrect = (_attemptCount == 1);
        int earnedPoints = isFirstAttemptCorrect ? _data.points : 0;

        _report?.Invoke(new QuestionAttemptData
        {
            question = _data,
            attemptNumber = _attemptCount,
             correct = true,
           // correct = (earnedPoints > 0),
            response = GetOptionText(pickedIdx),
            earnedPoints = earnedPoints,
            isFinal = true
        });

        if (retryButton)
            retryButton.gameObject.SetActive(false);
    }
    else
    {
        // ❌ WRONG ANSWER
        SetButtonColor(picked, wrongColor);

        foreach (var o in _opts)
            if (o.idx == _data.correctIndex)
                SetButtonColor(o.btn, correctColor);

        bool isFinal = (_attemptCount >= _maxAttempts);

        _report?.Invoke(new QuestionAttemptData
        {
            question = _data,
            attemptNumber = _attemptCount,
            correct = false,
            response = GetOptionText(pickedIdx),
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
            Invoke("InvokeEnableRetryButton", 4.0f);
        }
    }
}

   

    void InvokeEnableRetryButton()
    {
        retryButton.gameObject.SetActive(true);
    }

    void ResetForRetry()
    {
        if (_completed) return;

        foreach (var o in _opts)
        {
            o.btn.interactable = true;
            SetButtonColor(o.btn, normalColor);
        }

        if (retryButton) retryButton.gameObject.SetActive(false);
    }

    static void SetButtonColor(Button b, Color c)
    {
        var g = b.targetGraphic;
        if (g) g.color = c;
        var img = b.GetComponent<Image>();
        if (img) img.color = c;
    }

    void HighlightCorrectOnly()
    {
        foreach (var o in _opts)
        {
            if (o.idx == _data.correctIndex)
                SetButtonColor(o.btn, correctColor);
            else
                SetButtonColor(o.btn, normalColor);
        }
    }

    string GetOptionText(int optionIndex)
    {
        if (_data.options == null || optionIndex < 0 || optionIndex >= _data.options.Length)
            return string.Empty;
        return _data.options[optionIndex];
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    System.Collections.IEnumerator PlayOptionsSequentially()
    {
        Debug.Log("[MCQView] Starting sequential option audio playback");

        foreach (var opt in _opts)
        {
            if (_completed) yield break;

            AudioClip clip = (_data.optionVO != null && opt.idx >= 0 && opt.idx < _data.optionVO.Length)
                           ? _data.optionVO[opt.idx] : null;

            if (clip != null)
            {
                Debug.Log($"[MCQView] Playing option {opt.idx}: {clip.name}");
                
                // 🆕 Highlight on start
                SetButtonColor(opt.btn, highlightColor);

                VOBuss.PlayHover(clip);
                
                yield return new WaitForSeconds(clip.length);

                // 🆕 Reset to normal
                if (!_completed) SetButtonColor(opt.btn, normalColor);

                yield return new WaitForSeconds(delayBetweenOptions);
            }
        }

        Debug.Log("[MCQView] Sequential audio playback complete. Enabling buttons.");
        
        if (!_completed)
        {
            foreach (var o in _opts)
            {
                o.btn.interactable = true;
            }
        }
    }
}