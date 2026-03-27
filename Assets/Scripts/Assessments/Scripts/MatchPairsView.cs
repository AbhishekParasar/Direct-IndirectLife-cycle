using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchPairsView : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text promptText;
    public RectTransform leftColumnParent;
    public RectTransform rightColumnParent;
    public Button itemButtonPrefab;
    public Button submitButton;

    // NEW: Reset button
    public Button resetButton;             // <--- assign in Inspector

    [Header("Connector (UI Image)")]
    public RectTransform linesRoot;
    public Image connectorPrefab;
    public float connectorThickness = 6f;
    public Color connectorPending = new Color(1f, 0.9f, 0.2f, 1f);
    public Color connectorCorrect = new Color(0.25f, 0.85f, 0.35f, 1f);
    public Color connectorWrong = new Color(0.95f, 0.30f, 0.30f, 1f);

    [Header("Button Colors")]
    public Color normalColor = Color.white;
    public Color correctColor = new Color(0.25f, 0.85f, 0.35f, 1f);
    public Color wrongColor = new Color(0.95f, 0.30f, 0.30f, 1f);
    public Color highlightColor = new Color(0f, 0.5f, 1f, 1f);

    [Header("Option Audio")]
    public bool playOptionsSequentially = true;
    public float delayBetweenOptions = 0.4f;

    MatchPairsQuestionSO _data;
    System.Action<QuestionAttemptData> _report;
    int _attemptCount;
    int _maxAttempts = 3;
    bool _completed;
    bool _answeredCorrectly;
    Coroutine _sequentialAudioCoroutine;

    void Awake()
    {
        if (AssessmentManager.Instance != null)
        {
            highlightColor = AssessmentManager.Instance.sharedHighlightColor;
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
            AssessmentManager.Instance.OnQuestionInputUnlocked -= OnUnlocked;

        if (!_completed)
        {
            if (playOptionsSequentially)
            {
                if (_sequentialAudioCoroutine != null) StopCoroutine(_sequentialAudioCoroutine);
                _sequentialAudioCoroutine = StartCoroutine(PlayOptionsSequentially());
            }
            else
            {
                EnableAllButtons();
            }
        }
    }

    int _selectedLeft = -1;
    readonly Dictionary<int, int> _chosen = new(); // leftIndex -> rightShownIndex
    List<string> _rightOrder = new();
    readonly Dictionary<int, Button> _leftBtns = new();
    readonly Dictionary<int, Button> _rightBtns = new();

    struct Connector { public RectTransform rt; public Image img; }
    readonly Dictionary<int, Connector> _connByLeft = new();
    readonly List<RectTransform> _allConns = new();

    Canvas _canvas;
    Camera _uiCam;
    public Button itemButtonPrefab_r;

    public void Bind(
        MatchPairsQuestionSO data,
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
        promptText.text = data.prompt;
        promptText.text = data.prompt;

        bool amHandlesAudio = AssessmentManager.Instance != null
                              && AssessmentManager.Instance.lockViewUntilAudioEnd
                              && AssessmentManager.Instance.questionAudioSource != null;

        if (!amHandlesAudio && data.questionVO)
            VOBuss.PlayQuestion(data.questionVO);

        // Stop any running sequence
        if (_sequentialAudioCoroutine != null) StopCoroutine(_sequentialAudioCoroutine);
        // Clear old UI
        _chosen.Clear();
        _leftBtns.Clear(); _rightBtns.Clear();
        foreach (Transform c in leftColumnParent) Destroy(c.gameObject);
        foreach (Transform c in rightColumnParent) Destroy(c.gameObject);

        foreach (var c in _allConns) if (c) Destroy(c.gameObject);
        _allConns.Clear(); _connByLeft.Clear();

        if (!linesRoot) linesRoot = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        _canvas = linesRoot ? linesRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
        _uiCam = (_canvas && _canvas.renderMode == RenderMode.ScreenSpaceCamera) ? _canvas.worldCamera : null;

        // Left (fixed order)
        for (int i = 0; i < data.pairs.Length; i++)
        {
            var b = Instantiate(itemButtonPrefab, leftColumnParent);
            b.GetComponentInChildren<TMP_Text>(true).text = data.pairs[i].left;
            var hvL = b.gameObject.AddComponent<UIHoverVO>();
            hvL.hoverClip = (_data.leftVO != null && i < _data.leftVO.Length) ? _data.leftVO[i] : null;
            hvL.playOnHover = hvL.hoverClip != null;

            SetGraphicColor(b, normalColor);
            int li = i;
            b.onClick.AddListener(() => SelectLeft(li));

            // Initially interactable? If locked or sequential, false.
            b.interactable = false;
            _leftBtns[li] = b;
        }

        // Right (maybe shuffled)
        _rightOrder = new List<string>();
        for (int i = 0; i < data.pairs.Length; i++) _rightOrder.Add(data.pairs[i].right);
        // if (data.shuffleRight) Shuffle(_rightOrder);
        if (data.shuffleRight)
            ShuffleAvoidCorrectMatches(_rightOrder, data.pairs);

        for (int i = 0; i < _rightOrder.Count; i++)
        {
            var b = Instantiate(itemButtonPrefab_r, rightColumnParent);
            b.GetComponentInChildren<TMP_Text>(true).text = _rightOrder[i];
            SetGraphicColor(b, normalColor);
            int orig = FindRightOriginalIndex(_rightOrder[i]);
            var hvR = b.gameObject.AddComponent<UIHoverVO>();
            hvR.hoverClip = (_data.rightVO != null && orig >= 0 && orig < _data.rightVO.Length) ? _data.rightVO[orig] : null;
            hvR.playOnHover = hvR.hoverClip != null;

            int ri = i;
            b.onClick.AddListener(() => PickRight(ri));

            b.interactable = false;
            _rightBtns[ri] = b;
        }

        bool isLocked = AssessmentManager.Instance != null && AssessmentManager.Instance.IsQuestionInputLocked();
        if (isLocked && AssessmentManager.Instance != null)
        {
            AssessmentManager.Instance.OnQuestionInputUnlocked += OnUnlocked;
        }
        else
        {
            // Not locked by AM.
            if (!_completed && playOptionsSequentially)
            {
                _sequentialAudioCoroutine = StartCoroutine(PlayOptionsSequentially());
            }
            else if (!_completed)
            {
                EnableAllButtons();
            }
        }

        // Submit wiring
        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(Submit);
        submitButton.interactable = false;
        submitButton.gameObject.SetActive(!_completed);

        // NEW: Reset wiring
        if (resetButton)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(ResetForRetry);
            resetButton.gameObject.SetActive(false);
        }

        if (_completed)
        {
            FinalizeView();
        }
    }
    static void ShuffleAvoidCorrectMatches(List<string> rightOrder, MatchPair[] pairs)
    {
        // Try multiple times to avoid same-index matches
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Shuffle(rightOrder);

            bool hasMatch = false;
            for (int i = 0; i < rightOrder.Count; i++)
            {
                if (rightOrder[i] == pairs[i].right)
                {
                    hasMatch = true;
                    break;
                }
            }

            if (!hasMatch)
                return; // ✅ valid shuffle found
        }

        // Fallback (rare case): rotate by 1
        string first = rightOrder[0];
        rightOrder.RemoveAt(0);
        rightOrder.Add(first);
    }
    int FindRightOriginalIndex(string rightText)
    {
        for (int i = 0; i < _data.pairs.Length; i++)
            if (_data.pairs[i].right == rightText) return i;
        return -1;
    }

    void SelectLeft(int li)
    {
        // ⭐ ADDED: INPUT LOCK CHECK
        if (AssessmentManager.Instance != null && AssessmentManager.Instance.IsQuestionInputLocked())
        {
            Debug.Log("Input is locked. Waiting for audio to finish.");
            return;
        }
        // ⭐ END LOCK CHECK

        _selectedLeft = li;
        foreach (var kv in _leftBtns) kv.Value.interactable = (kv.Key != li) && !_chosen.ContainsKey(kv.Key);
    }

    void PickRight(int ri)
    {
        // ⭐ ADDED: INPUT LOCK CHECK
        if (AssessmentManager.Instance != null && AssessmentManager.Instance.IsQuestionInputLocked())
        {
            Debug.Log("Input is locked. Waiting for audio to finish.");
            return;
        }
        // ⭐ END LOCK CHECK

        if (_selectedLeft < 0) return;
        foreach (var kv in _chosen) if (kv.Value == ri) return; // right already used

        int li = _selectedLeft;
        _chosen[li] = ri;
        _selectedLeft = -1;

        foreach (var kv in _leftBtns) kv.Value.interactable = !_chosen.ContainsKey(kv.Key);
        _rightBtns[ri].interactable = false;

        var conn = CreateOrGetConnector(li);
        conn.img.color = connectorPending;
        UpdateConnector(li);

        submitButton.interactable = (_chosen.Count == _data.pairs.Length);
    }

    void Submit()
    {
        // ⭐ ADDED: INPUT LOCK CHECK
        if (AssessmentManager.Instance != null && AssessmentManager.Instance.IsQuestionInputLocked())
        {
            Debug.Log("Input is locked. Waiting for audio to finish.");
            return;
        }
        // ⭐ END LOCK CHECK

        if (_completed) return;

        int correctPairs = 0;

        foreach (var (leftIdx, rightShownIdx) in _chosen)
        {
            string pickedRightText = _rightOrder[rightShownIdx];
            bool isCorrect = (_data.pairs[leftIdx].right == pickedRightText);
            if (isCorrect) correctPairs++;

            SetGraphicColor(_leftBtns[leftIdx], isCorrect ? correctColor : wrongColor);
            SetGraphicColor(_rightBtns[rightShownIdx], isCorrect ? correctColor : wrongColor);

            if (_connByLeft.TryGetValue(leftIdx, out var conn))
                conn.img.color = isCorrect ? connectorCorrect : connectorWrong;

            if (!isCorrect)
            {
                for (int i = 0; i < _rightOrder.Count; i++)
                    if (_rightOrder[i] == _data.pairs[leftIdx].right)
                        SetGraphicColor(_rightBtns[i], correctColor);
            }
        }

        foreach (var b in _leftBtns.Values) b.interactable = false;
        foreach (var b in _rightBtns.Values) b.interactable = false;
        submitButton.interactable = false;

        float frac = (float)correctPairs / Mathf.Max(1, _data.pairs.Length);
        int earned = Mathf.RoundToInt(frac * _data.points);
        bool allCorrect = (correctPairs == _data.pairs.Length);

        _attemptCount = Mathf.Min(_attemptCount + 1, _maxAttempts);

        bool isFinal = allCorrect || _attemptCount >= _maxAttempts;

        /* if (_attemptCount == 1)
          {
              _report?.Invoke(new QuestionAttemptData
              {
                  question = _data,
                  attemptNumber = _attemptCount,
                  correct = allCorrect,
                  response = BuildSelectionSummary(),
                  earnedPoints = earned,
                  isFinal = isFinal
              });
          }
          else
          {
              _report?.Invoke(new QuestionAttemptData
              {
                  question = _data,
                  attemptNumber = _attemptCount,
                  correct = allCorrect,
                  response = BuildSelectionSummary(),
                  earnedPoints = 0,
                  isFinal = isFinal
              });
          }*/

        #region CorrectBackend Logic



        bool isFirstAttemptCorrect = allCorrect && _attemptCount == 1;
        int earnedPoints = isFirstAttemptCorrect ? earned : 0;

        _report?.Invoke(new QuestionAttemptData
        {
            question = _data,
            attemptNumber = _attemptCount,
            correct = allCorrect,
          //  correct = (earnedPoints > 0),
            response = BuildSelectionSummary(),
            earnedPoints = earnedPoints,
            isFinal = isFinal

        });

        #endregion
        if (resetButton != null)
        {
            if (!allCorrect && _attemptCount < _maxAttempts)
                resetButton.gameObject.SetActive(true);
            else
                resetButton.gameObject.SetActive(false);
        }
    }

    // NEW: One-button full reset between attempts (keeps current order)
    public void ResetForRetry()
    {
        // ⭐ ADDED: INPUT LOCK CHECK
        if (AssessmentManager.Instance != null && AssessmentManager.Instance.IsQuestionInputLocked())
        {
            Debug.Log("Input is locked. Waiting for audio to finish.");
            return;
        }
        // ⭐ END LOCK CHECK

        if (_completed) return;

        _selectedLeft = -1;
        _chosen.Clear();

        foreach (var b in _leftBtns.Values)
        {
            b.interactable = true;
            SetGraphicColor(b, normalColor);
        }
        foreach (var b in _rightBtns.Values)
        {
            b.interactable = true;
            SetGraphicColor(b, normalColor);
        }

        foreach (var c in _allConns) if (c) Destroy(c.gameObject);
        _allConns.Clear();
        _connByLeft.Clear();

        submitButton.interactable = false;
        submitButton.gameObject.SetActive(true);
        if (resetButton) resetButton.gameObject.SetActive(false);
    }

    void FinalizeView()
    {
        foreach (var b in _leftBtns.Values)
        {
            b.interactable = false;
            SetGraphicColor(b, correctColor);
        }

        foreach (var b in _rightBtns.Values)
        {
            b.interactable = false;
            SetGraphicColor(b, normalColor);
        }

        _chosen.Clear();
        foreach (var c in _allConns) if (c) Destroy(c.gameObject);
        _allConns.Clear();
        _connByLeft.Clear();

        if (submitButton)
        {
            submitButton.interactable = false;
            submitButton.gameObject.SetActive(false);
        }
        if (resetButton) resetButton.gameObject.SetActive(false);

        for (int i = 0; i < _data.pairs.Length; i++)
        {
            if (_leftBtns.TryGetValue(i, out var leftBtn))
                SetGraphicColor(leftBtn, correctColor);

            int rightIndex = _rightOrder.IndexOf(_data.pairs[i].right);
            if (rightIndex >= 0 && _rightBtns.TryGetValue(rightIndex, out var rightBtn))
            {
                SetGraphicColor(rightBtn, correctColor);
                _chosen[i] = rightIndex;
                var conn = CreateOrGetConnector(i);
                conn.img.color = connectorCorrect;
                UpdateConnector(i);
            }
        }
    }

    string BuildSelectionSummary()
    {
        var parts = new List<string>();
        foreach (var kv in _chosen)
        {
            if (kv.Value < 0 || kv.Value >= _rightOrder.Count) continue;
            string left = _data.pairs[kv.Key].left;
            string right = _rightOrder[kv.Value];
            parts.Add($"{left}->{right}");
        }
        parts.Sort();
        return string.Join(", ", parts);
    }

    void LateUpdate()
    {
        foreach (var kv in _chosen) UpdateConnector(kv.Key);
    }

    // ---------- connector helpers ----------

    Connector CreateOrGetConnector(int leftIdx)
    {
        if (_connByLeft.TryGetValue(leftIdx, out var c) && c.rt) return c;

        var img = Instantiate(connectorPrefab, linesRoot ? linesRoot : _canvas.transform);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(0f, connectorThickness); // length set later
        rt.SetAsLastSibling();

        var conn = new Connector { rt = rt, img = img };
        _connByLeft[leftIdx] = conn;
        _allConns.Add(rt);
        return conn;
    }

    void UpdateConnector(int leftIdx)
    {
        if (!_chosen.TryGetValue(leftIdx, out var rightShownIdx)) return;
        if (!_connByLeft.TryGetValue(leftIdx, out var conn) || !conn.rt) return;

        var aBtn = _leftBtns[leftIdx];
        var bBtn = _rightBtns[rightShownIdx];

        // Use anchor (MatchAnchor) if present; else button center
        RectTransform aRT = GetAnchorOrSelf(aBtn);
        RectTransform bRT = GetAnchorOrSelf(bBtn);

        Vector2 aLocal = WorldToLocalIn(linesRoot, aRT);
        Vector2 bLocal = WorldToLocalIn(linesRoot, bRT);

        Vector2 delta = bLocal - aLocal;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Vector2 mid = (aLocal + bLocal) * 0.5f;

        conn.rt.anchoredPosition = mid;
        conn.rt.sizeDelta = new Vector2(length, connectorThickness);
        conn.rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    RectTransform GetAnchorOrSelf(Button b)
    {
        var anchor = b.GetComponentInChildren<MatchAnchor>(true);
        return anchor ? anchor.GetComponent<RectTransform>() : b.GetComponent<RectTransform>();
    }

    Vector2 WorldToLocalIn(RectTransform root, RectTransform target)
    {
        Vector3 world = target.TransformPoint(target.rect.center);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            root,
            RectTransformUtility.WorldToScreenPoint(_uiCam, world),
            _uiCam,
            out Vector2 local);
        return local;
    }

    // ---------- misc ----------

    static void SetGraphicColor(Button b, Color c)
    {
        var g = b.targetGraphic ? b.targetGraphic : b.GetComponent<Image>();
        if (g) g.color = c;
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
        yield return null;
        if (AssessmentManager.Instance != null)
            yield return new WaitWhile(() => AssessmentManager.Instance.IsQuestionInputLocked());

        // 1. Left items
        for (int i = 0; i < _data.pairs.Length; i++)
        {
            if (_completed) yield break;
            if (_leftBtns.TryGetValue(i, out var btn))
            {
                AudioClip clip = (_data.leftVO != null && i < _data.leftVO.Length) ? _data.leftVO[i] : null;
                if (clip)
                {
                    btn.interactable = true; // Enable during play
                    SetGraphicColor(btn, highlightColor);
                    VOBuss.PlayHover(clip);
                    yield return new WaitForSeconds(clip.length);
                    if (!_completed)
                    {
                        SetGraphicColor(btn, normalColor);
                        btn.interactable = false; // Disable after play
                    }
                    yield return new WaitForSeconds(delayBetweenOptions);
                }
            }
        }

        // 2. Right items (in display order)
        for (int i = 0; i < _rightOrder.Count; i++)
        {
            if (_completed) yield break;
            if (_rightBtns.TryGetValue(i, out var btn))
            {
                // To find VO, we need original index
                int orig = FindRightOriginalIndex(_rightOrder[i]);
                AudioClip clip = (_data.rightVO != null && orig >= 0 && orig < _data.rightVO.Length) ? _data.rightVO[orig] : null;
                if (clip)
                {
                    btn.interactable = true; // Enable during play
                    SetGraphicColor(btn, highlightColor);
                    VOBuss.PlayHover(clip);
                    yield return new WaitForSeconds(clip.length);
                    if (!_completed)
                    {
                        SetGraphicColor(btn, normalColor);
                        btn.interactable = false; // Disable after play
                    }
                    yield return new WaitForSeconds(delayBetweenOptions);
                }
            }
        }

        if (!_completed) EnableAllButtons();
    }

    void EnableAllButtons()
    {
        foreach (var b in _leftBtns.Values) b.interactable = true;
        foreach (var b in _rightBtns.Values) b.interactable = true;

        // Restore interactable logic for chosen items?
        // Actually SelectLeft/PickRight handle logic. 
        // We just mean "Assessment phase is interactive now".
        // But wait: SelectLeft sets interactable based on state.
        // Let's just reset based on current selection state?
        // Simplify: just verify locked/completed state.

        // A simple reset to "interactive" state:
        foreach (var kv in _leftBtns)
        {
            // If already chosen, it might be disabled? 
            // In MatchPairs, left side remains interactable to re-select? 
            // Logic in SelectLeft: "kv.Value.interactable = (kv.Key != li) && !_chosen.ContainsKey(kv.Key);"
            // So if we just set all to true, we might break that.
            // But initially _selectedLeft is -1 and _chosen is empty.
            // If we are mid-game? 
            // The sequential play usually happens at start.
            // If it happens at start, chosen is empty.
            kv.Value.interactable = !_chosen.ContainsKey(kv.Key);
        }
        foreach (var kv in _rightBtns)
        {
            // Right buttons are interactable unless used?
            // PickRight logic: "_rightBtns[ri].interactable = false;" if chosen.
            // _chosen maps Left -> RightShownIndex
            // So we can check if this right index is a value in _chosen.
            bool isChosen = false;
            foreach (var v in _chosen.Values) if (v == kv.Key) { isChosen = true; break; }
            kv.Value.interactable = !isChosen;
        }
    }
}