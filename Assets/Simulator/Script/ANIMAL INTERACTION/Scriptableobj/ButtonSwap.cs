using UnityEngine;
using UnityEngine.UI;

public class ButtonSwap : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject panelToShow;
    [SerializeField] private GameObject buttonToHide;

    // This function will be called when the button is clicked
    public void ToggleUI()
    {
        // Check to make sure objects are assigned to avoid errors
        if (panelToShow != null && buttonToHide != null)
        {
            panelToShow.SetActive(true);    // Show the panel
            buttonToHide.SetActive(false); // Hide the button
        }
    }
}