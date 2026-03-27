using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ActiveInactiveManager : MonoBehaviour
{
    [Header("Object Toggles")]
    [Tooltip("List of GameObjects to set active when the button is clicked.")]
    public List<GameObject> toEnable = new List<GameObject>();
    [Tooltip("List of GameObjects to set inactive when the button is clicked.")]
    public List<GameObject> toDisable = new List<GameObject>();

    private Button myButton;
    [Header("Visual Feedback")]
    [Tooltip("The GameObject (e.g., a checkmark image) to show when the button is locked or action completes.")]
    public GameObject tickImage;

    [Header("Buttons to Change")]
    [SerializeField]
    private Button[] toInteractable;

    [SerializeField]
    private Button[] toNonInteractable;

    [Header("Animation Settings")]
    [Tooltip("The GameObject with the Legacy Animation component.")]
    public GameObject animationTarget;

    [Tooltip("The exact name of the Animation Clip to play (e.g., 'ButtonPressAnim').")]
    public string animationClipName;

    // Flag for a permanent lock (independent of audio)
    private bool isPermanentlyLocked = false;

    void Start()
    {
        myButton = GetComponent<Button>();
        if (myButton != null)
        {
            // Attach the click handler to the button's onClick event
            myButton.onClick.AddListener(HandleButtonClick);
        }
        else
        {
            Debug.LogError($"[ActiveInactiveManager] Button component not found on {gameObject.name}.");
        }
    }

    // Called every frame to monitor the audio state and update the button
    void Update()
    {
        UpdateButtonInteractableState();
    }

    /// <summary>
    /// Checks the global audio lock and updates the button's interactable state.
    /// </summary>
    private void UpdateButtonInteractableState()
    {
        // 1. Check for permanent lock (priority check)
        if (isPermanentlyLocked)
        {
            // Ensure the button is disabled and then stop checking other states
            if (myButton != null && myButton.interactable == true)
            {
                myButton.interactable = false;
            }
            return;
        }

        // 2. Safely check the global audio manager 
        bool isAudioPlaying = false;
        // Check for null to prevent NullReferenceException during startup order
        if (InputLockAudioManager.Instance != null)
        {
            isAudioPlaying = InputLockAudioManager.Instance.IsInputLocked;
        }

        // 3. Determine the required state
        // The button should be interactable ONLY if the audio is NOT playing.
        bool shouldBeInteractable = !isAudioPlaying;

        // 4. Apply the state (only if the button exists and the state needs changing)
        if (myButton != null && myButton.interactable != shouldBeInteractable)
        {
            myButton.interactable = shouldBeInteractable;
            // You can remove this Debug.Log in the final build
            Debug.Log($"[ActiveInactiveManager: {gameObject.name}] Button interactable state changed: {shouldBeInteractable}");
        }
    }

    /// <summary>
    /// Executes when the button is clicked (if interactable).
    /// </summary>
    private void HandleButtonClick()
    {
        // This lock check is a final safety net, even though Update() visually locks the button.
        if (InputLockAudioManager.Instance != null && InputLockAudioManager.Instance.IsInputLocked)
        {
            Debug.Log($"[ActiveInactiveManager: {gameObject.name}] Click ignored: Input is globally locked by audio.");
            return; // EXIT and ignore the click
        }

        // If not locked, proceed with the action
        EnableDisableObjects();
    }

    /// <summary>
    /// Toggles the active/inactive state of the listed GameObjects.
    /// </summary>
    public void EnableDisableObjects()
    {
        foreach (GameObject obj in toEnable)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        foreach (GameObject obj in toDisable)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        Debug.Log($"[ActiveInactiveManager: {gameObject.name}] Click accepted. Toggling objects.");
    }

    public void PlayAnimation()
    {
        if (animationTarget != null && !string.IsNullOrEmpty(animationClipName))
        {
            Animation targetAnimation = animationTarget.GetComponent<Animation>();

            if (targetAnimation != null)
            {
                if (targetAnimation.GetClip(animationClipName) != null)
                {
                    targetAnimation.Play(animationClipName);
                    Debug.Log($"Legacy Animation '{animationClipName}' started on {animationTarget.name}.");
                }
                else
                {
                    Debug.LogError($"Animation Clip '{animationClipName}' not found on the Animation component of {animationTarget.name}. Check the spelling.");
                }
            }
            else
            {
                Debug.LogError($"GameObject '{animationTarget.name}' is missing the Legacy Animation component.");
            }
        }
        else
        {
            Debug.LogWarning("Animation target or clip name is not set in the Inspector.");
        }
    }

    /// <summary>
    /// Permanently locks the button, overriding the audio lock status.
    /// </summary>
    public void LockButton()
    {
        if (myButton != null)
        {
            myButton.interactable = false;
        }
        isPermanentlyLocked = true; // Set the permanent lock flag
        Debug.Log($"[ActiveInactiveManager: {gameObject.name}] Button is now permanently locked.");
    }

    public void SetButtonInteractivity()
    {
        // 1. Set Buttons to INTERACTABLE (enabled)
        foreach (Button btn in toInteractable)
        {
            if (btn != null)
            {
                btn.interactable = true;
            }
        }

        // 2. Set Buttons to NON-INTERACTABLE (disabled)
        foreach (Button btn in toNonInteractable)
        {
            if (btn != null)
            {
                btn.interactable = false;
            }
        }

        Debug.Log($"[ButtonInteractivityManager: {gameObject.name}] Button interactivity toggled.");
    }

    /// <summary>
    /// Activates the designated tick image (for visual feedback).
    /// </summary>
    public void ShowTickImage()
    {
        if (tickImage != null)
        {
            tickImage.SetActive(true);
            Debug.Log($"[ActiveInactiveManager: {gameObject.name}] Tick image activated.");
        }
    }
}