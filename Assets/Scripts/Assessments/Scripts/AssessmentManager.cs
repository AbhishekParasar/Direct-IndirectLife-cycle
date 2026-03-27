// AssessmentManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class AssessmentManager : MonoBehaviour
{
    // Exposed singleton-like accessor for other UI scripts
    public static AssessmentManager Instance { get; private set; }

    // ⭐ NEW/FIXED: Public event for views to subscribe to when the input lock is released
    public event Action OnQuestionInputUnlocked;
    [Header("Transition Type")]
    public bool isAutoMoveNextQuestion;

    [Header("Data")]
    public QuestionSetSO questionSet;

    [Header("Prefabs (Views)")]
    public MCQView mcqPrefab;
    public TrueFalseView trueFalsePrefab;
    public MatchPairsView matchPairsPrefab;

    [Header("Mount + HUD")]
    public Transform viewMount;
    public TMP_Text headerText;
    public TMP_Text scoreText;

    [Header("Global Styles")]
    [Tooltip("Highlight color used across all question types (MCQ, True/False)")]
    public Color sharedHighlightColor = new Color(0f, 0.5f, 1f, 1f);

    // ⭐ ADDED: Question Audio and Lock Control (for audio synchronization)
    public AudioSource questionAudioSource;
    [Tooltip("If checked, the question view input will be locked until the audio finishes.")]
    public bool lockViewUntilAudioEnd = true;

    [Header("Question Navigation Buttons")]
    public Button prevQuestionButton;
    public Button nextQuestionButton;
    public GameObject winscreen;

    [Header("Activity Navigation")]
    public Button nextActivityButton;
    [Tooltip("Auto-show the Next Activity button when the assessment is finished.")]
    public bool autoShowActivityNextOnFinish = true;
    [Tooltip("Minimum number of correct answers required to enable Next Activity.")]
    public int minCorrectToPass = 0;

    [Header("Attempts")]
    [Tooltip("Maximum number of attempts allowed per question.")]
    public int maxAttemptsPerQuestion = 3;
    [Tooltip("If ON, allow URL '?api_url=...' to override the Inspector backendEndpoint.")]
    public bool allowBackendFromUrl = true;
    public string urlParam_backendEndpoint = "api_url";

    [Header("Reporting (Legacy)")]
    [Tooltip("Optional identifier for this assessment when reporting results.")]
    public string assessmentId = "assessment";
    [Tooltip("HTTP endpoint that receives the assessment report as JSON.")]
    public string backendEndpoint;
    [Tooltip("Log the generated report JSON to the console before sending.")]
    public bool logReportToConsole = true;
    [Tooltip("Automatically send the assessment report when all questions are completed.")]
    public bool sendReportOnFinish = true;

    [Header("Prev Unlock Rule")]
    [Tooltip("If ON: Prev is available only after the learner has at least one correct answer (any question).")]
    public bool enablePrevOnlyAfterAnyCorrect = true;

    [Header("Keyboard Shortcuts")]
    public bool enableKeyboardShortcuts = true;
    public bool enterSpaceAdvance = true;
    [Tooltip("If ON, allow URL '?auth=...' to override the Inspector authToken.")]
    public bool allowAuthTokenFromUrl = false;

    // -------- QBS API context --------
    [Header("API Auth & Context")]
    [Tooltip("Header key for auth, e.g. X-Authorization")]
    public string authHeaderKey = "X-Authorization";
    [Tooltip("Token value for the auth header (⚠️ embedding tokens in client builds exposes them).")]
    public string authToken = "";
    [Tooltip("Simulation name to send, e.g. \"unity-simulation\"")]
    public string simulationName = "unity-simulation";

    [Header("WebGL Param Intake")]
    [Tooltip("If ON (WebGL), read userName/topic_id (and optionally token) from URL query.")]
    public bool readParamsFromUrl = true;
    public string urlParam_userName = "userName";
    public string urlParam_topicId = "topic_id";
    public string urlParam_authToken = "auth"; // optional
    [Tooltip("Fallbacks if not found from URL")]
    public string userName = "";
    public string topic_id = "";
    public string urlParam_backendEndpointFromUrl = "api_url";

    [Header("Optional: Send a 'start' ping")]
    [Tooltip("If ON, sends a minimal payload on Start so the server can log session start.")]
    public bool sendStartPing = false;

    // ------------ internal ------------
    readonly List<QuestionSO> _queue = new();
    GameObject _currentView;

    class AnswerState
    {
        public bool answered;
        public bool correct;
        public int earned;
        public QuestionSummary summary;
    }
    readonly List<AnswerState> _answers = new();

    int _index = 0;
    int _score = 0;
    bool _reportSent;

    // ⭐ ADDED: Internal state for question input lock
    private bool _isQuestionInputLocked = false;

    void Start()
    {
        // set Instance for other scripts to query
        Instance = this;

        // Preserve Inspector defaults
        string finalAuthToken = authToken;
        string finalBackend = backendEndpoint;

        if (readParamsFromUrl && Application.platform == RuntimePlatform.WebGLPlayer)
        {
            TryReadWebGLQueryParams(Application.absoluteURL,
                out string qUser, out string qTopic, out string qToken, out string qApiUrl);

            if (!string.IsNullOrWhiteSpace(qUser)) userName = qUser;
            if (!string.IsNullOrWhiteSpace(qTopic)) topic_id = qTopic;

            if (allowAuthTokenFromUrl && !string.IsNullOrWhiteSpace(qToken))
                finalAuthToken = qToken;

            if (allowBackendFromUrl && !string.IsNullOrWhiteSpace(qApiUrl) && IsValidHttpUrl(qApiUrl))
                finalBackend = qApiUrl;

            if (logReportToConsole)
                Debug.Log($"[QBS] URL params -> userName:{userName}, topic_id:{topic_id}, api_url:{finalBackend}");
        }

        // commit decisions
        authToken = finalAuthToken;
        backendEndpoint = finalBackend;

        BuildQueue();

        if (prevQuestionButton)
        {
            prevQuestionButton.onClick.RemoveAllListeners();
            prevQuestionButton.onClick.AddListener(ShowPrevQuestion);
        }
        if (nextQuestionButton)
        {
            nextQuestionButton.onClick.RemoveAllListeners();
            nextQuestionButton.onClick.AddListener(ShowNextQuestion);
        }
        if (nextActivityButton)
        {
            nextActivityButton.onClick.RemoveAllListeners();
            nextActivityButton.onClick.AddListener(GoToNextActivity);
        }

        // ⭐ Ensure AudioSource is present
        if (questionAudioSource == null)
        {
            Debug.LogWarning("Question Audio Source is not assigned. Audio sync features will be disabled.");
        }

        // Optional: Send a minimal "session started" ping
        if (sendStartPing)
            StartCoroutine(SendStartPingCoroutine());

        ShowQuestion(0);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ⭐ ADDED: Public accessor for the lock state
    public bool IsQuestionInputLocked()
    {
        return _isQuestionInputLocked;
    }

    // ⭐ ADDED: Internal method to set the lock state
    private void SetQuestionInputLock(bool isLocked)
    {
        _isQuestionInputLocked = isLocked;
    }

    // ⭐ CORRECTED: Coroutine to manage audio playback and unlock
    private IEnumerator PlayAudioAndUnlock(AudioClip clip)
    {
        SetQuestionInputLock(true);

        if (clip != null && questionAudioSource != null)
        {
            Debug.Log($"[Audio Sync] Playing clip '{clip.name}'. Duration: {clip.length} seconds.");
            questionAudioSource.clip = clip;
            questionAudioSource.Play();

            // Wait until audio finishes playing
            yield return new WaitForSeconds(clip.length);
        }
        else
        {
            // Fallback for immediate unlock if no audio clip is provided
            yield return null;
        }

        SetQuestionInputLock(false);
        Debug.Log("Question audio finished. Input unlocked.");

        // ⭐ THE CRITICAL FIX: Invoke the event! ⭐
        OnQuestionInputUnlocked?.Invoke();
    }

    // ⭐ ADDED: Placeholder function to retrieve the audio clip 
    private AudioClip GetAudioClipFromQuestion(QuestionSO question)
    {
        if (question == null) return null;

        // Note: You must ensure the actual field name (e.g., questionVO) exists 
        // in your specific QuestionSO classes (MCQQuestionSO, TrueFalseQuestionSO, etc.).
        if (question is MCQQuestionSO mcq) return mcq.questionVO;
        else if (question is TrueFalseQuestionSO tf) return tf.questionVO;
        else if (question is MatchPairsQuestionSO match) return match.questionVO;

        Debug.LogWarning($"[Audio Lock] No 'questionVO' field found for question type: {question.GetType().Name}.");
        return null;
    }

    void TryReadWebGLQueryParams(string absoluteUrl,
        out string qUser, out string qTopic, out string qToken, out string qApiUrl)
    {
        qUser = qTopic = qToken = qApiUrl = null;
        if (string.IsNullOrEmpty(absoluteUrl)) return;

        int idx = absoluteUrl.IndexOf('?');
        if (idx < 0 || idx >= absoluteUrl.Length - 1) return;

        var query = absoluteUrl.Substring(idx + 1);
        var pairs = query.Split('&');
        foreach (var p in pairs)
        {
            var kv = p.Split('=');
            if (kv.Length != 2) continue;
            string key = Uri.UnescapeDataString(kv[0]);
            string val = Uri.UnescapeDataString(kv[1]);

            if (string.Equals(key, urlParam_userName, StringComparison.OrdinalIgnoreCase)) qUser = val;
            else if (string.Equals(key, urlParam_topicId, StringComparison.OrdinalIgnoreCase)) qTopic = val;
            else if (string.Equals(key, urlParam_authToken, StringComparison.OrdinalIgnoreCase)) qToken = val;
            else if (string.Equals(key, urlParam_backendEndpoint, StringComparison.OrdinalIgnoreCase)) qApiUrl = val;
        }
    }
    static bool IsValidHttpUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    void Update()
    {
        if (!enableKeyboardShortcuts || _queue.Count == 0) return;

        // PREVIOUS (questions): Left / A
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            if (CanGoPrevQuestion()) ShowPrevQuestion();

        // NEXT: Right / D / Enter / Space
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)
            || (enterSpaceAdvance && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))))
        {
            if (IsFinished())
            {
                if (CanGoNextActivity()) GoToNextActivity();
            }
            else
            {
                if (CanGoNextQuestion()) ShowNextQuestion();
            }
        }

        if (winscreen)
        {
            if (AllAnswered())
                if (winscreen) Invoke("InvokeWinscreen", 3.0f);
        }
    }

    void BuildQueue()
    {
        _queue.Clear();
        _answers.Clear();

        if (!questionSet || questionSet.questions == null || questionSet.questions.Length == 0) return;

        _queue.AddRange(questionSet.questions);
        if (questionSet.shuffleQuestions) Shuffle(_queue);

        int cappedAttempts = Mathf.Max(1, maxAttemptsPerQuestion);
        for (int i = 0; i < _queue.Count; i++)
        {
            var q = _queue[i];
            var summary = new QuestionSummary
            {
                questionId = q ? q.name : $"Question_{i + 1}",
                prompt = q ? q.prompt : string.Empty,
                questionType = q ? q.Type.ToString() : string.Empty,
                maxAttempts = cappedAttempts
            };

            _answers.Add(new AnswerState { summary = summary });
        }
    }

    // Public helper used by UI (e.g. to check if current question is answered)
    public bool IsCurrentQuestionAnswered()
    {
        if (_answers == null || _answers.Count == 0) return false;
        if (_index < 0 || _index >= _answers.Count) return false;
        return _answers[_index].answered;
    }

    // ---------- question nav ----------
    void ShowNextQuestion() => ShowQuestion(_index + 1);
    void ShowPrevQuestion() => ShowQuestion(Mathf.Max(_index - 1, 0));

    void ShowQuestion(int newIndex)
    {
        if (_currentView) Destroy(_currentView);

        // Finished pseudo-screen (no more questions)
        if (_queue.Count == 0 || newIndex >= _queue.Count)
        {
            _index = Mathf.Clamp(newIndex, 0, Mathf.Max(0, _queue.Count - 1));
            if (headerText) headerText.text = "Finished!";
            if (scoreText) scoreText.text = $"Final: {RecomputeScore()}";

            UpdatePrevUI();
            UpdateNextQuestionUI();
            UpdateNextActivityUI();
            return;
        }

        _index = Mathf.Clamp(newIndex, 0, _queue.Count - 1);

        if (headerText) headerText.text = $"Q {_index + 1}/{_queue.Count}";
        if (scoreText) scoreText.text = $"Score: {RecomputeScore()}";

        var q = _queue[_index];
        var state = _answers[_index];

        // ⭐ IMPLEMENT AUDIO LOCKING LOGIC
        AudioClip questionClip = GetAudioClipFromQuestion(q);

        if (lockViewUntilAudioEnd && questionClip != null)
        {
            if (questionAudioSource != null) questionAudioSource.Stop();
            StartCoroutine(PlayAudioAndUnlock(questionClip));
        }
        else
        {
            // Ensure it's unlocked if we skip the audio/lock process
            SetQuestionInputLock(false);
        }
        // END AUDIO LOCKING LOGIC

        switch (q.Type)
        {
            case QuestionType.MCQ:
                {
                    var view = Instantiate(mcqPrefab, viewMount);
                    // NOTE: MCQView must be modified to respect IsQuestionInputLocked()
                    view.Bind((MCQQuestionSO)q, OnAttempt, state.summary.attemptsUsed, Mathf.Max(1, maxAttemptsPerQuestion), state.answered, state.correct);
                    _currentView = view.gameObject;
                    break;
                }
            case QuestionType.TrueFalse:
                {
                    var view = Instantiate(trueFalsePrefab, viewMount);
                    // NOTE: TrueFalseView must be modified to respect IsQuestionInputLocked()
                    view.Bind((TrueFalseQuestionSO)q, OnAttempt, state.summary.attemptsUsed, Mathf.Max(1, maxAttemptsPerQuestion), state.answered, state.correct);
                    _currentView = view.gameObject;
                    break;
                }
            case QuestionType.MatchPairs:
                {
                    var view = Instantiate(matchPairsPrefab, viewMount);
                    // NOTE: MatchPairsView must be modified to respect IsQuestionInputLocked()
                    view.Bind((MatchPairsQuestionSO)q, OnAttempt, state.summary.attemptsUsed, Mathf.Max(1, maxAttemptsPerQuestion), state.answered, state.correct);
                    _currentView = view.gameObject;
                    break;
                }
        }

        UpdatePrevUI();
        UpdateNextQuestionUI();
        UpdateNextActivityUI();
    }

    void OnAttempt(QuestionAttemptData attempt)
    {
        var state = _answers[_index];
        var summary = state.summary;

        if (summary == null)
        {
            summary = new QuestionSummary
            {
                questionId = attempt.question ? attempt.question.name : $"Question_{_index + 1}",
                prompt = attempt.question ? attempt.question.prompt : string.Empty,
                questionType = attempt.question ? attempt.question.Type.ToString() : string.Empty,
                maxAttempts = Mathf.Max(1, maxAttemptsPerQuestion)
            };
            state.summary = summary;
        }

        var entry = new QuestionAttemptEntry
        {
            attemptNumber = attempt.attemptNumber,
            correct = attempt.correct,
            response = attempt.response,
            earnedPoints = attempt.earnedPoints,
            wasFinal = attempt.isFinal
        };
        summary.attempts.Add(entry);
        summary.attemptsUsed = attempt.attemptNumber;
        if (!attempt.correct) summary.wrongAttempts++;

        if (attempt.isFinal)
        {
            state.answered = true;
            state.correct = attempt.correct;
            state.earned = Mathf.Max(0, attempt.earnedPoints);

            summary.answeredCorrectly = attempt.correct;
            summary.earnedPoints = Mathf.Max(0, attempt.earnedPoints);
        }

        if (attempt.isFinal)
        {
            if (scoreText) scoreText.text = $"Score: {RecomputeScore()}";

            UpdatePrevUI();
            UpdateNextQuestionUI();
            UpdateNextActivityUI();

            if (AllAnswered())
            {
                if (winscreen) Invoke("InvokeWinscreen", 3.0f);

                if (sendReportOnFinish && !_reportSent)
                    StartCoroutine(SendReportCoroutine());
            }
        }
    }

    void InvokeWinscreen()
    {
        winscreen.SetActive(true);
    }

    // ---------- gating ----------
    bool AnyCorrect()
    {
        for (int i = 0; i < _answers.Count; i++) if (_answers[i].correct) return true;
        return false;
    }

    bool AllAnswered()
    {
        for (int i = 0; i < _answers.Count; i++) if (!_answers[i].answered) return false;
        return true;
    }

    int CountCorrect()
    {
        int c = 0;
        for (int i = 0; i < _answers.Count; i++) if (_answers[i].correct) c++;
        return c;
    }

    bool IsOnLastQuestion() => _queue.Count > 0 && _index == _queue.Count - 1;

    bool IsFinished()
    {
        // “Finished” means last question has been reached AND it has been answered
        return _queue.Count > 0 && IsOnLastQuestion() && _answers[_index].answered;
    }

    bool CanGoPrevQuestion()
    {
        if (_index <= 0) return false;
        return !enablePrevOnlyAfterAnyCorrect || AnyCorrect();
    }

    bool CanGoNextQuestion()
    {
        if (_queue.Count == 0) return false;
        if (!IsOnLastQuestion()) return _answers[_index].answered;
        return _answers[_index].answered;
    }

    bool CanGoNextActivity()
    {
        if (!IsFinished()) return false;
        return CountCorrect() >= Mathf.Max(0, minCorrectToPass);
    }

    void UpdatePrevUI()
    {
        if (!prevQuestionButton) return;
        bool can = CanGoPrevQuestion();
        if (!isAutoMoveNextQuestion)
        {
            prevQuestionButton.gameObject.SetActive(can);
            prevQuestionButton.interactable = can;
        }
    }

    void UpdateNextQuestionUI()
    {
        if (!nextQuestionButton) return;
        bool can = CanGoNextQuestion();
        bool show = !IsFinished() && can;

        if (show && isAutoMoveNextQuestion)
        {
            Invoke("InvokeNextQuestion", 3f);
        }
        else
        {
            nextQuestionButton.gameObject.SetActive(show);
            nextQuestionButton.interactable = can && !IsFinished();
        }
    }

    void InvokeNextQuestion()
    {
        ShowNextQuestion();
    }

    void UpdateNextActivityUI()
    {
        if (!nextActivityButton) return;
        bool finished = IsFinished();
        bool passed = CountCorrect() >= Mathf.Max(0, minCorrectToPass);

        if (autoShowActivityNextOnFinish)
            nextActivityButton.gameObject.SetActive(finished);

        nextActivityButton.interactable = finished && passed;
    }

    // ---------- activity switch ----------
    void GoToNextActivity()
    {
        if (!CanGoNextActivity()) return;
        Time.timeScale = 1f;
        // GameFlowManager.MarkCurrentComplete();
    }

    // ---------- score ----------
    int RecomputeScore()
    {
        _score = 0;
        for (int i = 0; i < _answers.Count; i++) _score += _answers[i].earned;
        return _score;
    }

    // ---------- reporting ----------
    [Serializable]
    class ApiPayload
    {
        public string userName;
        public string topic_id;
        public string assessmentId;
        public string simulation;
        public string attemptedOn;
        public int totalQuestions;
        public int totalCorrect;
        public int totalWrong;
        public int totalWrongAttempts;
        public int totalScore;
        public List<QuestionSummary> questions = new();
    }

    AssessmentReport BuildReport()
    {
        var report = new AssessmentReport
        {
            assessmentId = string.IsNullOrEmpty(assessmentId)
                ? (questionSet ? questionSet.name : "assessment")
                : assessmentId,
            totalQuestions = _answers.Count
        };

        int correctCount = 0;
        int wrongCount = 0;
        int wrongAttempts = 0;
        int totalScore = 0;

        foreach (var state in _answers)
        {
            if (state.summary == null)
                continue;

            state.summary.maxAttempts = Mathf.Max(1, maxAttemptsPerQuestion);
            state.summary.attemptsUsed = Mathf.Max(state.summary.attemptsUsed, state.summary.attempts.Count);
            state.summary.answeredCorrectly = state.correct;
            state.summary.earnedPoints = state.earned;

            report.questions.Add(state.summary);

            totalScore += state.earned;
            if (state.correct) correctCount++; else wrongCount++;
            wrongAttempts += state.summary.wrongAttempts;
        }

        report.totalCorrect = correctCount;
        report.totalWrong = wrongCount;
        report.totalWrongAttempts = wrongAttempts;
        report.totalScore = totalScore;

        return report;
    }

    ApiPayload BuildApiPayload()
    {
        var report = BuildReport();
        var payload = new ApiPayload
        {
            userName = userName,
            topic_id = topic_id,
            assessmentId = report.assessmentId,
            simulation = simulationName,
            attemptedOn = DateTime.UtcNow.ToString("o"), // ISO8601 UTC
            totalQuestions = report.totalQuestions,
            totalCorrect = report.totalCorrect,
            totalWrong = report.totalWrong,
            totalWrongAttempts = report.totalWrongAttempts,
            totalScore = report.totalScore,
            questions = report.questions
        };
        return payload;
    }

    IEnumerator SendReportCoroutine()
    {
        string beforeJson = JsonUtility.ToJson(BuildApiPayload(), true);
        Debug.Log($"<color=#FF0000><b>[QBS] BEFORE SEND PAYLOAD:</b>\n{beforeJson}</color>");
        if (_reportSent) yield break;
        _reportSent = true;

        if (string.IsNullOrWhiteSpace(backendEndpoint))
        {
            Debug.LogWarning("Assessment report endpoint is empty; skipping send.");
            yield break;
        }

        var payload = BuildApiPayload();
        string json = JsonUtility.ToJson(payload, true);

        if (logReportToConsole)
            Debug.Log($"[QBS] Assessment payload:\n{json}");

        using var request = new UnityWebRequest(backendEndpoint, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(authHeaderKey) && !string.IsNullOrEmpty(authToken))
            request.SetRequestHeader(authHeaderKey, authToken);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"[QBS] Report sent OK. Response: {request.downloadHandler.text}");
        }
        else
        {
            Debug.LogError($"[QBS] Report send FAILED: {request.responseCode} {request.error}\nBody: {request.downloadHandler.text}");
        }
    }

    // Optional: start ping (minimal body)
    IEnumerator SendStartPingCoroutine()
    {
        if (string.IsNullOrWhiteSpace(backendEndpoint))
            yield break;

        var ping = new ApiPayload
        {
            userName = userName,
            topic_id = topic_id,
            assessmentId = assessmentId,
            simulation = simulationName,
            attemptedOn = DateTime.UtcNow.ToString("o"),
            totalQuestions = 0,
            totalCorrect = 0,
            totalWrong = 0,
            totalWrongAttempts = 0,
            totalScore = 0,
            questions = new List<QuestionSummary>()
        };

        string json = JsonUtility.ToJson(ping, true);
        if (logReportToConsole) Debug.Log($"[QBS] Start ping payload:\n{json}");

        using var request = new UnityWebRequest(backendEndpoint, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(authHeaderKey) && !string.IsNullOrEmpty(authToken))
            request.SetRequestHeader(authHeaderKey, authToken);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
            Debug.Log("[QBS] Start ping sent.");
        else
            Debug.LogWarning($"[QBS] Start ping failed: {request.responseCode} {request.error}");
    }

    // ---------- utils ----------
    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}