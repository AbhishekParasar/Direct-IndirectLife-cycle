using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (mainCameraTransform != null)
        {
            // Makes the panel look directly at the camera
            transform.LookAt(mainCameraTransform);

            // Step 1: Lock the rotation to the Y-axis only
            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);

            // Step 2: ADD THIS LINE to flip the text 180 degrees horizontally.
            // This corrects the mirror effect, making the text face forward.
            transform.rotation *= Quaternion.Euler(0f, 180f, 0f);
        }
    }
}