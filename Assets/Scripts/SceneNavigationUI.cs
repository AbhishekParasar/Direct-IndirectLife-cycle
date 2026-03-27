// SceneNavigationUI.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneNavigationUI : MonoBehaviour
{
    [Header("Optional UI Buttons")]
    public Button resetButton;
    public Button backButton;

    void Start()
    {
        if (resetButton) resetButton.onClick.AddListener(ResetCurrentScene);
        if (backButton) backButton.onClick.AddListener(GoToPreviousScene);
    }

    /// <summary>
    /// Reloads the current scene from scratch.
    /// </summary>
    public void ResetCurrentScene()
    {
        Time.timeScale = 1f; // unpause if frozen
        var activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
    }

    /// <summary>
    /// Goes back to the previous scene in the GameFlow sequence (if any).
    /// </summary>
    public void GoToPreviousScene()
    {
       SceneManager.LoadScene(0);

        
    }
}
