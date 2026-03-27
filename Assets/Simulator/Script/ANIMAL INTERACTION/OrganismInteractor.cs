using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OrganismInteractor : MonoBehaviour
{
    [Header("--- Main obj ---")]
    public GameObject Frog;
    public GameObject Hen;
    public GameObject Butterfly;
    public GameObject PalmTree;
    // This field holds the name used in the prompt for the first stage click.
    public string stageName;

    [Header("--- Sequence & Glow ---")]
    public GameObject labelGlow; // Assign the label/glow object here
    private bool canInteract = false;    


    [Header("--- External Manager ---")]
    public GameFlowManager gameFlowManager;

    [Header("--- Data Source ---")]
    public LifeCycleOrganismSO organismData;

    [Header("--- STAGE ASSETS ---")]
    // The objects the player must CLICK to advance to the next stage. (Always visible after start)
    public GameObject[] progressionObjects;
    // ⭐ NEW: Buttons for organisms that use UI instead of 3D clicks (e.g. Palm Tree)
    public Button[] progressionButtons;

    // The objects that are VISIBLE while the current stage's audio plays. (Toggled sequentially)
    public GameObject[] stageVisuals;

    [Header("--- UI Elements (Global) ---")]
    public GameObject background;
    public GameObject centralUIPanel;
    public TextMeshProUGUI centralDescriptionText;
    // ⭐ NEW: Reference to the Next Button and its Text
    public Button nextStageButton;
    public TextMeshProUGUI nextButtonText;

    [Header("--- Scene Components ---")]
    public GameObject lifeCycleSetContainer;
    public AudioSource organismAudioSource;
    public GameObject completionMark;

    [Header("--- Camera Settings ---")]
    public Transform zoomedInView;
    public Transform wideView;
    public float cameraSpeed = 1.5f;
    // ⭐ NEW: Field for the specific FOV when zoomed in (set in Inspector)
    [Tooltip("The Field of View the camera will transition to when zooming onto this organism.")]
    public float zoomedFOV = 40f;
    // Recommended wide FOV default: 60f
    public float wideFOV = 60f;

    [Header("--- Post Processing ---")]
    public Volume postProcessVolume;

    private int currentStageIndex = -1;
    private Coroutine stageControlCoroutine;
    private Coroutine cameraMoveCoroutine;
    private Camera mainCamera;

    private DepthOfField depthOfField;
    private MinFloatParameter focusDistanceParameter;

    private GameObject[] objectsToToggle; // Other organisms to hide

    // ⭐ NEW: Flag to track if the current stage's audio has finished
    private bool stageAudioFinished = false;

    // --- Lifecycle Methods ---

    void Start()
    {
        if (centralUIPanel != null) centralUIPanel.SetActive(false);
        if (completionMark != null) completionMark.SetActive(false);
        // ⭐ NEW: Set the Next button to be inactive initially
        if (nextStageButton != null) nextStageButton.gameObject.SetActive(false);

        // Check array lengths
        // Relaxed validation: It is now allowed for TotalStages to be greater than progressionObjects/stageVisuals/buttons.
        if (organismData == null || organismData.TotalStages == 0)
        {
            Debug.LogError("Organism Data SO is invalid or missing stages!");
            return;
        }

        if (lifeCycleSetContainer != null) lifeCycleSetContainer.SetActive(false);

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found! Ensure your scene has a camera tagged 'MainCamera'.");
        }

        // Initialize Post Processing for URP
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out depthOfField);

            if (depthOfField != null)
            {
                focusDistanceParameter = depthOfField.focusDistance;
                depthOfField.active = false;
            }
        }

        // ⭐ NEW: Add listener to the Next button
        if (nextStageButton != null)
        {
            nextStageButton.onClick.AddListener(OnNextStageButtonClicked);
        }
        else
        {
            Debug.LogError("Next Stage Button reference is missing! Assign it in the Inspector.");
        }

        // ⭐ NEW: Register listeners for Progression Buttons
        if (progressionButtons != null)
        {
            for (int i = 0; i < progressionButtons.Length; i++)
            {
                int index = i; // Capture index for closure
                if (progressionButtons[i] != null)
                {
                    progressionButtons[i].onClick.AddListener(() => OnProgressionButtonClicked(index));
                }
            }
        }

        // INITIALIZE: All click targets and all visuals should be OFF before interaction starts.
        SetProgressionObjectsVisibility(false);
        SetVisualsActive(-1);

        // Default to not interactable and no glow until Manager says so
        if (labelGlow != null) labelGlow.SetActive(false);
        canInteract = false;

        GameObject thisOrganism = this.gameObject;

        // Determine the two organisms to hide
        if (Frog != thisOrganism && Frog != null) objectsToToggle = AddToArray(objectsToToggle, Frog);
        if (Hen != thisOrganism && Hen != null) objectsToToggle = AddToArray(objectsToToggle, Hen);
        if (Butterfly != thisOrganism && Butterfly != null) objectsToToggle = AddToArray(objectsToToggle, Butterfly);
        if (PalmTree != thisOrganism && PalmTree != null) objectsToToggle = AddToArray(objectsToToggle, PalmTree);
    }

    // --- Stage Toggling Helpers ---

    private void SetProgressionObjectsVisibility(bool state)
    {
        // 1. Handle 3D Progression Objects
        if (progressionObjects != null)
        {
            foreach (GameObject obj in progressionObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(state);
                }
            }
        }

        // 2. Handle UI Progression Buttons
        if (progressionButtons != null)
        {
            foreach (Button btn in progressionButtons)
            {
                if (btn != null)
                {
                    btn.gameObject.SetActive(state);
                }
            }
        }
    }

    private void SetVisualsActive(int newIndex)
    {
        for (int i = 0; i < stageVisuals.Length; i++)
        {
            if (stageVisuals[i] != null)
            {
                stageVisuals[i].SetActive(i == newIndex);
            }
        }
    }

    void Update()
    {
        if (InputManager.InputLocked) return;

        // Strict Sequence Check
        if (!canInteract && currentStageIndex == -1) return;

        // ⭐ NEW: Input is now only handled for the initial click or for the click targets
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                GameObject clickedObject = hit.collider.gameObject;

                if (currentStageIndex == -1)
                {
                    // Case 1: Initial click on the main organism to START the sequence.
                    if (clickedObject == gameObject)
                    {
                        HandleInteractionStart();
                    }
                }
                else if (currentStageIndex < organismData.TotalStages)
                {
                    // Case 2: Click to ADVANCE the stage (handles Stage 0 click and subsequent clicks).
                    // This is now only for the progression objects, not the "Next" button.
                    HandleProgressionClick(clickedObject);
                }
            }
        }
    }

    // ⭐ NEW: Handler for Progression Buttons
    void OnProgressionButtonClicked(int buttonIndex)
    {
        Debug.Log($"Progression Button Clicked: {buttonIndex}");

        // Case 1: First Stage (Index 0)
        if (currentStageIndex == 0 && stageControlCoroutine == null)
        {
            if (buttonIndex == 0)
            {
                ShowStage(currentStageIndex);
                return;
            }
        }

        // Case 2: Finish
        if (currentStageIndex >= organismData.TotalStages - 1) return;

        // Case 3: Advance
        if (centralUIPanel.activeSelf) return; // Prevent advancing if current text is showing

        int nextStageIndex = currentStageIndex + 1;
        if (buttonIndex == nextStageIndex)
        {
            AdvanceToNextStage();
        }
    }

    void HandleProgressionClick(GameObject clickedObject)
    {
        // ⭐ MODIFIED: Check if we are ready to start the *first* stage audio (currentStageIndex is 0).
        if (currentStageIndex == 0 && stageControlCoroutine == null)
        {
            // The required click object for Stage 0 description is progressionObjects[0].
            if (progressionObjects != null && progressionObjects.Length > 0 && clickedObject == progressionObjects[0])
            {
                // Start the audio process for the current index (0).
                ShowStage(currentStageIndex);
                return;
            }
        }

        // If we are on the last stage, clicks on progression objects are irrelevant as it auto-completes 
        // after the last audio, or requires a final button click.
        if (currentStageIndex >= organismData.TotalStages - 1) return;

        // ⭐ MODIFIED: Check for click to ADVANCE to the next stage (index > 0).
        // This click is only allowed if the description panel for the PREVIOUS stage is NOT active.
        if (centralUIPanel.activeSelf) return;

        // The click required is for the object at index (currentStageIndex + 1).
        int nextStageIndex = currentStageIndex + 1;

        if (progressionObjects != null && nextStageIndex < progressionObjects.Length && clickedObject == progressionObjects[nextStageIndex])
        {
            // The logic here is now handled by AdvanceToNextStage only.
            AdvanceToNextStage();
        }
    }

    // --- Interaction Logic (Initial Click) ---

    void HandleInteractionStart()
    {
        if (currentStageIndex != -1) return;

        if (gameFlowManager != null && gameFlowManager.introPanel != null)
        {
            gameFlowManager.introPanel.SetActive(false);
        }

        // Turn off the glow/label once interaction starts
        if (labelGlow != null) labelGlow.SetActive(false);

        SetObjectsActive(objectsToToggle, false);

        if (zoomedInView != null)
        {
            // Pass the Inspector-defined zoomedFOV to the smooth move coroutine
            StartSmoothCameraMove(zoomedInView, zoomedFOV);
        }

        PlayOrganismNameAudio();
        if (background != null) background.SetActive(true);

        StartCoroutine(StartInteractionAfterAudio());
    }

    IEnumerator StartInteractionAfterAudio()
    {
        if (organismAudioSource != null && organismAudioSource.isPlaying)
        {
            yield return new WaitWhile(() => organismAudioSource.isPlaying);
        }

        // Set camera effects and container active
        if (depthOfField != null && focusDistanceParameter != null && zoomedInView != null && lifeCycleSetContainer != null)
        {
            float focusDistance = Vector3.Distance(zoomedInView.position, lifeCycleSetContainer.transform.position);
            focusDistanceParameter.value = focusDistance;
            depthOfField.active = true;
        }

        if (lifeCycleSetContainer != null)
        {
            lifeCycleSetContainer.SetActive(true);
        }

        SetProgressionObjectsVisibility(true);

        // Stage 0 setup
        currentStageIndex = 0;
        centralUIPanel.SetActive(true); // Show UI panel with the prompt text
        // Prompt to click the progression object (now stageName is the prompt text)
        centralDescriptionText.text = "Click the labels to learn more about it.";
        SetVisualsActive(0); // Show the visual for Stage 0 (the click target)

        // Hide the "Next" button for this initial prompt
        if (nextStageButton != null) nextStageButton.gameObject.SetActive(false);

        // Play the "explore more" audio when showing the cycle prompt
        if (organismData.exploreClickAudioClip != null && organismAudioSource != null)
        {
            organismAudioSource.PlayOneShot(organismData.exploreClickAudioClip);
        }

        // Stage 0 audio is now triggered by the user's next click on progressionObjects[0].
    }

    void PlayOrganismNameAudio()
    {
        if (organismAudioSource != null && organismData.nameAudioClip != null)
        {
            organismAudioSource.clip = organismData.nameAudioClip;
            organismAudioSource.Play();
            InputManager.LockInputForDuration(organismData.nameAudioClip.length);
        }
        else
        {
            Debug.LogWarning("Organism Name AudioClip not assigned or AudioSource is missing!");
        }
    }

    // --- Camera Movement ---
    // Modified to accept a target FOV
    void StartSmoothCameraMove(Transform targetView, float targetFOV)
    {
        if (mainCamera == null || targetView == null) return;
        if (cameraMoveCoroutine != null) StopCoroutine(cameraMoveCoroutine);
        cameraMoveCoroutine = StartCoroutine(MoveCameraSmoothly(targetView, targetFOV)); // Pass FOV
    }

    // Coroutine modified to change FOV
    IEnumerator MoveCameraSmoothly(Transform targetView, float targetFOV)
    {
        float t = 0f;
        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;
        float startFOV = mainCamera.fieldOfView; // Get current FOV

        while (t < 1f)
        {
            t += Time.deltaTime * cameraSpeed;
            mainCamera.transform.position = Vector3.Lerp(startPosition, targetView.position, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRotation, targetView.rotation, t);

            // Lerp the Field of View
            mainCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t);

            yield return null;
        }
        mainCamera.transform.position = targetView.position;
        mainCamera.transform.rotation = targetView.rotation;
        mainCamera.fieldOfView = targetFOV; // Ensure final FOV is set
        cameraMoveCoroutine = null;
    }

    // --- Stage Progression Methods ---

    void AdvanceToNextStage()
    {
        if (stageControlCoroutine != null) StopCoroutine(stageControlCoroutine);

        InputManager.LockInputForDuration(0.2f);

        // currentStageIndex is incremented here, preparing to show the NEXT stage's description.
        currentStageIndex++;

        ShowStage(currentStageIndex);
    }

    // ⭐ NEW: Method called by the OK button
    public void OnNextStageButtonClicked()
    {
        // Only allow clicking the button if the audio has finished playing
        if (!stageAudioFinished) return;

        // Immediately stop the coroutine to prevent any double-progression issues
        if (stageControlCoroutine != null) StopCoroutine(stageControlCoroutine);

        // Lock input momentarily for a smooth transition
        InputManager.LockInputForDuration(0.2f);

        // Calculate the NEXT stage index
        int nextStageIndex = currentStageIndex + 1;

        // CHECK: If we still have a corresponding visible progression object OR BUTTON for the NEXT stage
        bool hasNextObject = (progressionObjects != null && nextStageIndex < progressionObjects.Length && progressionObjects[nextStageIndex] != null);
        bool hasNextButton = (progressionButtons != null && nextStageIndex < progressionButtons.Length && progressionButtons[nextStageIndex] != null);

        if (hasNextObject || hasNextButton)
        {
            // 1. Hide the UI panel and the button
            if (centralUIPanel != null) centralUIPanel.SetActive(false);
            if (nextStageButton != null) nextStageButton.gameObject.SetActive(false);

            // 2. Disable the CURRENT visual.
            SetVisualsActive(-1);

            // 3. Show the visual for the NEXT stage (to prompt the click).
            SetVisualsActive(nextStageIndex);

            // Reset the flag and wait for user to click the object
            stageAudioFinished = false;
            stageControlCoroutine = null;
        }
        else
        {
            // NO progression object (e.g., extra text/audio stages at the end or without 3D interaction).
            // AUTOMATIC PROGRESSION: Do NOT hide the UI. Just play the next stage immediately.
            
            AdvanceToNextStage();
        }
    }

    private void ShowStage(int index)
    {
        if (centralUIPanel == null || centralDescriptionText == null)
        {
            Debug.LogError("UI elements (Panel/Text) are NULL. Check Inspector assignments.");
            return;
        }

        if (index >= organismData.TotalStages)
        {
            SequenceFinished();
            return;
        }

        LifeCycleStage stage = organismData.stages[index];

        centralUIPanel.SetActive(true);
        if (nextStageButton != null) nextStageButton.gameObject.SetActive(false); // Hide button initially

        // ⭐ MODIFIED: Always set the button text to "OK" as requested
        if (nextButtonText != null)
        {
            nextButtonText.text = "Next";
        }

        // VISUAL LOGIC: Show the visual for the CURRENT stage being described.
        SetVisualsActive(index);

        // This is the call that starts the audio for the current stage (index 0, then 1, 2, etc.)
        stageControlCoroutine = StartCoroutine(DisplayAndSynchronize(stage));
    }

    // --- Synchronization Coroutine ---

    IEnumerator DisplayAndSynchronize(LifeCycleStage stage)
    {
        // 1. Display text and play audio
        centralDescriptionText.text = stage.stageText;

        AudioClip clip = stage.stageAudioClip;

        if (clip != null && organismAudioSource != null)
        {
            organismAudioSource.clip = clip;
            organismAudioSource.Play();
        }

        // 2. Wait for audio to finish playing
        if (organismAudioSource != null)
        {
            yield return new WaitWhile(() => organismAudioSource.isPlaying);
        }

        // 3. LOGIC AFTER AUDIO: Audio is finished, show the button.
        stageAudioFinished = true; // Set flag to allow button click

        if (nextStageButton != null)
        {
            nextStageButton.gameObject.SetActive(true); // Show the button
        }

        // The Coroutine now waits here for the button click, which calls OnNextStageButtonClicked().
    }

    // --- Finish Sequence ---

    void SequenceFinished()
    {
        if (stageControlCoroutine != null) StopCoroutine(stageControlCoroutine);
        if (organismAudioSource != null) organismAudioSource.Stop();

        SetProgressionObjectsVisibility(false);
        SetVisualsActive(-1);

        if (depthOfField != null)
        {
            depthOfField.active = false;
        }
        SetObjectsActive(objectsToToggle, true);
        if (lifeCycleSetContainer != null) lifeCycleSetContainer.SetActive(false);
        if (background != null) background.SetActive(false);
        if (centralUIPanel != null) centralUIPanel.SetActive(false); // Ensure it is hidden upon finish
        if (nextStageButton != null) nextStageButton.gameObject.SetActive(false); // Hide the button
        if (completionMark != null) completionMark.SetActive(true);

        if (wideView != null)
        {
            // Use the Inspector-defined wideFOV when returning to the starting view
            StartSmoothCameraMove(wideView, wideFOV);
        }

        if (gameFlowManager != null)
        {
            gameFlowManager.RegisterOrganismCompletion();
            gameFlowManager.ShowSelectAnotherOrganismMessage();
        }

        currentStageIndex = organismData.TotalStages;
        stageAudioFinished = false; // Reset flag
    }

    // --- Utility Methods ---
    private void SetObjectsActive(GameObject[] objects, bool state)
    {
        if (objects == null) return;
        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(state);
            }
        }
    }

    private GameObject[] AddToArray(GameObject[] original, GameObject item)
    {
        if (original == null)
        {
            return new GameObject[] { item };
        }
        GameObject[] newArray = new GameObject[original.Length + 1];
        original.CopyTo(newArray, 0);
        newArray[original.Length] = item;
        return newArray;
    }

    // --- Sequence Control ---
    public void SetActiveTurn(bool isActive)
    {
        canInteract = isActive;
        if (labelGlow != null)
        {
            labelGlow.SetActive(isActive);
        }
    }
}