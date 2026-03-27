using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    // --- ORIGINAL INTRO UI ELEMENTS ---
    [Header("--- 1. Introduction UI ---")]
    
    public GameObject introPanel;
    public TextMeshProUGUI introText;
    // This button is ONLY used to advance the intro (Message One -> Message Two -> Interaction)
    public Button introNextButton;

    // --- NEW FINAL COMPLETION UI ELEMENTS ---
    [Header("--- 2. Completion UI ---")]
    // The panel that pops up at the end (KEPT FOR REFERENCE, BUT NO LONGER SHOWN)
    public GameObject completionPanel;
    public TextMeshProUGUI completionText;
    // The button that loads the next scene (KEPT FOR REFERENCE, BUT NO LONGER USED)
    public Button finalActionButton;

    // Audio Setup
    [Header("Audio Clips")]
    public AudioSource audioSource;
    public AudioClip audioClipOne;
    public AudioClip audioClipTwo;

    // Messages
    private string messageOne = "All living things have life cycles. Some grow gradually (direct life cycle), while others go through dramatic changes (indirect life cycle). Click on Next button to explore!";
    private string messageTwo = "Click on an organism to view its life cycle.";

    // Safety buffer (no longer needed for typing, but kept for potential future use)
    private float audioBufferTime = 0.5f;

    // State Management
    private enum IntroState { MessageOne, MessageTwo, Interaction, Finished }
    private IntroState currentState = IntroState.MessageOne;

    // --- COMPLETION TRACKING FIELDS ---
    [Header("--- 3. Tracking & Flow ---")]
    public GameObject[] allCompletionMarks;
    public string nextSceneName = "AssessmentScene";

    private int totalOrganisms;
    private int organismsCompleted = 0;

    // --- SEQUENCE MANAGEMENT ---
    [Header("--- Sequence Control ---")]
    public OrganismInteractor[] orderedOrganisms; // Frog, Butterfly, Hen, PalmTree
    private int currentSequenceIndex = 0;


    // --- Lifecycle Methods ---

    void Start()
    {
        // 1. Setup Validation
        if (audioSource == null)
        {
            Debug.LogError("GameFlowManager requires an AudioSource component assigned to the 'Audio Source' field.");
            return;
        }

        // ⭐ IMPORTANT: InputManager initialization must happen before any component tries to lock input.
        // Assuming InputManager is a Singleton, ensure its Awake/Initialize runs first.
        // If not already done, you must initialize InputManager here if it's a static class:
        // InputManager.Initialize(this); 

        // 2. Initial UI State


        introPanel.SetActive(true);
        if (introNextButton != null) introNextButton.gameObject.SetActive(false);

        // Ensure the completion panel is hidden at start
        if (completionPanel != null) completionPanel.SetActive(false);

        // 3. Setup Listeners
        if (introNextButton != null)
        {
            introNextButton.onClick.AddListener(OnIntroNextButtonClicked);
        }
        // Final button listener setup (REMOVED as it's no longer used for scene transition)
        /*
        if (finalActionButton != null)
        {
            finalActionButton.onClick.AddListener(LoadNextScene);
        }
        */

        // 4. Start Flow
        totalOrganisms = allCompletionMarks.Length;
        StartCoroutine(ShowTextSequentially(messageOne, audioClipOne));
        CheckInitialCompletion();
    }

    void OnIntroNextButtonClicked()
    {
        StopAllCoroutines();
        audioSource.Stop();

        if (introNextButton != null) introNextButton.gameObject.SetActive(false);

        if (currentState == IntroState.MessageOne)
        {
            currentState = IntroState.MessageTwo;
            // Play Message Two (This will be the standard prompt after completion as well)
            StartCoroutine(ShowTextSequentially(messageTwo, audioClipTwo));
        }
        else if (currentState == IntroState.MessageTwo)
        {
            // Transition from Intro to Interaction Phase
            currentState = IntroState.Interaction;

            // Immediately hide the UI after the second message
            if (introPanel != null) introPanel.SetActive(false);

            Debug.Log("Introduction finished. Interaction phase started.");
            
            // Initialize and Start the Sequence
            InitializeSequence();
        }
    }

    private void InitializeSequence()
    {
        // Auto-discover if not assigned
        if (orderedOrganisms == null || orderedOrganisms.Length == 0)
        {
            Debug.Log("Auto-discovering organisms for sequence...");
            var allInteractors = FindObjectsOfType<OrganismInteractor>();
            orderedOrganisms = new OrganismInteractor[4];
            
            foreach (var org in allInteractors)
            {
                if (org.name.IndexOf("Frog", System.StringComparison.OrdinalIgnoreCase) >= 0) orderedOrganisms[0] = org;
                else if (org.name.IndexOf("Butterfly", System.StringComparison.OrdinalIgnoreCase) >= 0) orderedOrganisms[1] = org;
                else if (org.name.IndexOf("Hen", System.StringComparison.OrdinalIgnoreCase) >= 0) orderedOrganisms[2] = org;
                else if (org.name.IndexOf("Palm", System.StringComparison.OrdinalIgnoreCase) >= 0) orderedOrganisms[3] = org;
            }
        }

        // Activate the first one
        currentSequenceIndex = 0;
        UpdateSequenceState();
    }

    private void UpdateSequenceState()
    {
        if (orderedOrganisms == null) return;

        for (int i = 0; i < orderedOrganisms.Length; i++)
        {
            if (orderedOrganisms[i] != null)
            {
                // Only the organism at the current index is active/glowing
                orderedOrganisms[i].SetActiveTurn(i == currentSequenceIndex);
            }
        }
    }


    public void ShowSelectAnotherOrganismMessage()
    {
        // Only show the prompt if the game isn't finished
        if (currentState == IntroState.Interaction)
        {
            if (introPanel != null) introPanel.SetActive(true);

            // Stop current message/audio and play message two again.
            StopAllCoroutines();
            audioSource.Stop();

            // Replay the standard "Click on an organism..." prompt (messageTwo/audioClipTwo)
            StartCoroutine(ShowTextSequentially(messageTwo, audioClipTwo));

            // Ensure the Next button is hidden (ShowTextSequentially will manage this after audio)
            if (introNextButton != null) introNextButton.gameObject.SetActive(false);
        }
    }


    // --- COMPLETION TRACKING METHODS ---

    public void RegisterOrganismCompletion()
    {
        if (currentState == IntroState.Interaction)
        {
            organismsCompleted++;
            Debug.Log($"Organism completed. Total completed: {organismsCompleted} / {totalOrganisms}");
            
            // Advance sequence
            currentSequenceIndex++;
            UpdateSequenceState();

            CheckAllCompleted();
        }
    }


    private void CheckAllCompleted()
    {
        if (organismsCompleted >= totalOrganisms)
        {
            currentState = IntroState.Finished;
            HandleFinalState();
        }
    }

    private void HandleFinalState()
    {
        Debug.Log("All organisms completed! Immediately loading the next scene.");

        // Ensure the original intro UI is hidden
        if (introPanel != null)
        {
            introPanel.SetActive(false);
        }

        // Stop any lingering prompt audio
        StopAllCoroutines();
        audioSource.Stop();

        // 🛑 CRITICAL CHANGE: Instead of showing the panel, immediately load the next scene.
        LoadNextScene();
    }

    // Coroutine displays text instantly, then waits for audio before enabling the button.
    IEnumerator ShowTextSequentially(string message, AudioClip clip)
    {
        introText.text = message; // Display the full text instantly

        if (clip != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }

        // Wait for the audio to finish playing
        if (clip != null)
        {
            yield return new WaitWhile(() => audioSource.isPlaying);
        }
        // If there's no clip, wait a short moment for the user to read the text
        else
        {
            yield return new WaitForSeconds(1.0f);
        }

        // Activate the button ONLY if we are currently in the intro sequence (MessageOne or MessageTwo)
        if ((currentState == IntroState.MessageOne || currentState == IntroState.MessageTwo) && introNextButton != null)
        {
            introNextButton.gameObject.SetActive(true);
        }
        else if (currentState == IntroState.Interaction)
        {
            // If we are in the interaction phase, hide the button after playing the prompt.
            if (introNextButton != null)
            {
                introNextButton.gameObject.SetActive(false);
            }
        }
    }

    private void CheckInitialCompletion()
    {
        organismsCompleted = 0;
        foreach (GameObject mark in allCompletionMarks)
        {
            if (mark != null && mark.activeSelf)
            {
                organismsCompleted++;
            }
        }

        if (organismsCompleted >= totalOrganisms)
        {
            currentState = IntroState.Finished;
            HandleFinalState();
        }
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("Next Scene Name is not set in GameFlowManager! Cannot load scene.");
        }
    }
}