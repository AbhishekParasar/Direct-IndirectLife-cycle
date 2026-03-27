using UnityEngine;
using System.Collections; // Required for IEnumerator

public class InputManager : MonoBehaviour
{
    // Allows any script to check the lock status easily.
    public static bool InputLocked { get; private set; } = false;

    // Singleton pattern
    public static InputManager Instance { get; private set; }

    // Reference to the currently running lock coroutine to allow stopping it.
    private Coroutine lockCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Optionally: DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Locks input for a specified duration, stopping any current lock timer.
    /// </summary>
    public static void LockInputForDuration(float duration)
    {
        // Stop any existing lock coroutine if one is running
        if (Instance.lockCoroutine != null)
        {
            Instance.StopCoroutine(Instance.lockCoroutine);
            Instance.lockCoroutine = null;
        }

        InputLocked = true;

        // Start the coroutine on the InputManager instance and store the reference
        Instance.lockCoroutine = Instance.StartCoroutine(Instance.UnlockAfterDelay(duration));
    }

    /// <summary>
    /// Immediately unlocks the input, overriding any ongoing lock timer.
    /// </summary>
    public static void UnlockInput()
    {
        if (InputLocked == false) return; // Already unlocked

        InputLocked = false;

        // Stop the coroutine if it is running
        if (Instance.lockCoroutine != null)
        {
            Instance.StopCoroutine(Instance.lockCoroutine);
            Instance.lockCoroutine = null;
        }

        Debug.Log("Input Unlocked.");
    }


    // Coroutine to handle the timed unlock
    private IEnumerator UnlockAfterDelay(float duration)
    {
        Debug.Log("Input Locked for " + duration + " seconds.");
        yield return new WaitForSeconds(duration);

        // Only set InputLocked to false if we weren't locked again during the wait.
        InputLocked = false;
        lockCoroutine = null;
        Debug.Log("Input Unlocked (Timer Finished).");
    }
}