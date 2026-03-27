using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections; // Needed for Coroutines
using UnityEngine.UI; // Needed for Button

public class ComparisonGameManager : MonoBehaviour
{
    // --- Game Setup ---
    [Header("Game Setup")]
    public int totalItemsToPlace = 5;
    private int itemsCorrectlyPlaced = 0;

    // --- Canvas Control ---
    [Header("Canvas Control")]
    [Tooltip("The canvas containing the draggable items and drop zones.")]
    public GameObject gameCanvas;
    [Tooltip("The canvas to show when the comparison game is finished.")]
    public GameObject assessmentCanvas;

    // Optional: Keep for a final text feedback if needed
    public TextMeshProUGUI finishedText;

    // --- Feedback Audio Fields ---
    [Header("Feedback Audio")]
    // The AudioSource component used for all audio (feedback and prompts)
    public AudioSource feedbackAudioSource;
    public AudioClip correctClip;
    public AudioClip incorrectClip;

    // --- Audio Prompt Panel Elements (MERGED) ---
    [Header("Pre-Game Audio Prompt")]
    [Tooltip("The main panel/container for the introductory prompt.")]
    public GameObject promptPanel;
    [Tooltip("The Text component to display the introductory message.")]
    public TextMeshProUGUI promptText;
    [Tooltip("The button the user must click to start the game.")]
    public Button promptButton;
    [Tooltip("The audio clip containing the introductory instructions.")]
    public AudioClip startClip;

    // Fields to hold audio durations for timing transitions
    private float finalAudioDuration = 0f;
    private bool gameStarted = false;

    void Awake()
    {
        // 1. Initialize UI state. This runs before any Start() methods.
        if (gameCanvas != null)
        {
            // Initially, the game area should be hidden, as the prompt is up.
            gameCanvas.SetActive(true); // Game canvas should be OFF initially
        }
        if (assessmentCanvas != null)
        {
            assessmentCanvas.SetActive(false);
        }
    }

    void Start()
    {
        // Set up the button listener for the prompt
        if (promptButton != null)
        {
            // The button is wired directly to StartGame()
            promptButton.onClick.AddListener(StartGame);
        }

        // Start the game by showing the initial prompt.
        ShowPrompt();
    }

    // --- Pre-Game Prompt Logic (MERGED) ---

    private void ShowPrompt()
    {
        // Safety checks
        if (promptPanel == null || promptText == null || feedbackAudioSource == null || promptButton == null || startClip == null)
        {
            Debug.LogError("Pre-Game Prompt setup is incomplete. Check all Inspector references. Starting game immediately.");
            StartGame(); // Fallback to starting the game immediately
            return;
        }

        // 1. Lock input globally (it will be unlocked after audio finishes, then locked again by StartGame)
        InputManager.LockInputForDuration(9999f);

        // 2. Show the prompt panel
        promptPanel.SetActive(true);
        promptText.text = "Drag each organism into the correct box: Direct or Indirect life cycle.";

        // 3. Hide the button initially
        promptButton.gameObject.SetActive(false);

        // 4. Start the narration coroutine
        StartCoroutine(NarrateAndRevealButton(startClip));
    }

    private IEnumerator NarrateAndRevealButton(AudioClip clip)
    {
        feedbackAudioSource.clip = clip;
        feedbackAudioSource.Play();

        // Wait until the audio is finished playing
        yield return new WaitWhile(() => feedbackAudioSource.isPlaying);

        // 5. Audio finished, now reveal the button and unlock input for the button click
        if (!gameStarted)
        {
            promptButton.gameObject.SetActive(true);
            InputManager.UnlockInput(); // Unlock input only for the button click
            Debug.Log("Prompt audio finished. Button revealed.");
        }
    }

    /// <summary>
    /// Starts the actual comparison game flow. Called by the prompt button.
    /// This method HIDES the prompt panel and shows the game canvas.
    /// </summary>
    public void StartGame()
    {
        if (gameStarted) return;
        gameStarted = true;

        // Ensure audio stops if the user clicks the button before the audio finished
        if (feedbackAudioSource.isPlaying)
        {
            feedbackAudioSource.Stop();
        }

        // 1. HIDE the prompt panel
        if (promptPanel != null)
        {
            // ⭐ CRITICAL FIX: Set to FALSE to hide the panel
            promptPanel.SetActive(false);
        }

        // 2. SHOW the game canvas
        if (gameCanvas != null)
        {
            gameCanvas.SetActive(true);
        }

        // 3. Ensure input is unlocked for the drag-and-drop game
        InputManager.UnlockInput();
        Debug.Log("Game Started. Input unlocked for drag-and-drop.");
    }

    // --- Game Drop Logic ---

    // Handles the drop event, checking correctness and managing feedback.
    public void RegisterDrop(GameObject droppedObject, RectTransform droppedObjectRect, DropZone imageCorrectZone, Transform originalParent, PointerEventData eventData, int originalSiblingIndex)
    {
        // Prevent drop processing if input is locked (e.g., during audio feedback)
        if (InputManager.InputLocked) return;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        DropZone droppedZone = null;

        // 2. Iterate through all hit UI elements to find the DropZone component
        foreach (RaycastResult result in results)
        {
            droppedZone = result.gameObject.GetComponent<DropZone>();
            if (droppedZone != null)
            {
                break;
            }
        }

        // 3. Check for correctness and apply feedback/lock
        if (droppedZone != null && droppedZone == imageCorrectZone)
        {
            // CORRECT: Lock input, play audio, and then lock image.

            // Check if this is the FINAL drop to save the audio duration for the transition wait.
            if (itemsCorrectlyPlaced + 1 >= totalItemsToPlace && correctClip != null)
            {
                finalAudioDuration = correctClip.length;
            }

            LockInputAndPlayFeedback(correctClip, "Yes! That’s right.");
            LockImage(droppedObject, imageCorrectZone);
        }
        else
        {
            // INCORRECT: Lock input, play audio, and snap back.
            LockInputAndPlayFeedback(incorrectClip, "Try again. Think about whether the organism changes its body form.");

            // FAILED: Snap back logic.
            droppedObject.transform.SetParent(originalParent, false);
            droppedObjectRect.anchoredPosition = Vector2.zero;

            // Restore the image's position within the Layout Group
            droppedObject.transform.SetSiblingIndex(originalSiblingIndex);
        }
    }

    // Method to handle audio playback and input locking
    private void LockInputAndPlayFeedback(AudioClip clip, string textFeedback)
    {
        if (feedbackAudioSource != null && clip != null)
        {
            feedbackAudioSource.clip = clip;
            feedbackAudioSource.Play();

            // Lock all interaction for the duration of the audio clip
            InputManager.LockInputForDuration(clip.length);
        }

        Debug.Log("FEEDBACK: " + textFeedback);
    }

    private void LockImage(GameObject droppedObject, DropZone zone)
    {
        // Parent to the Drop Zone container.
        droppedObject.transform.SetParent(zone.transform, false);

        // Center the image within the zone.
        droppedObject.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        // Disable dragging so it stays put (assuming DragAndDrop is the script).
        var dragScript = droppedObject.GetComponent<DragAndDrop>();
        if (dragScript != null)
        {
            dragScript.enabled = false;
        }

        itemsCorrectlyPlaced++;
        CheckGameFinished();
    }

    private void CheckGameFinished()
    {
        if (itemsCorrectlyPlaced >= totalItemsToPlace)
        {
            // Start the Coroutine to wait for the final feedback audio before transitioning.
            StartCoroutine(TransitionToAssessment(finalAudioDuration));
        }
    }

    // COROUTINE: Waits for the final audio to end before transitioning
    IEnumerator TransitionToAssessment(float delay)
    {
        Debug.Log($"Last item placed. Waiting for {delay} seconds (audio duration) before transitioning.");

        // Wait for the duration of the final feedback audio clip
        yield return new WaitForSeconds(delay);

        // --- Transition Logic ---

        // 1. Hide the current game canvas
        if (gameCanvas != null)
        {
            gameCanvas.SetActive(false);
        }

        // 2. Hide the prompt panel (if it was somehow visible)
        if (promptPanel != null)
        {
            promptPanel.SetActive(false);
        }

        // 3. Show the assessment canvas
        if (assessmentCanvas != null)
        {
            assessmentCanvas.SetActive(true);
        }

        // 4. Optional: Update the finished text
        if (finishedText != null)
        {
            finishedText.text = "Comparison Complete! Starting Assessment...";
        }

        Debug.Log("Transition Complete: Assessment Canvas Active.");
    }
}