using UnityEngine;
using UnityEngine.EventSystems;

public class DragAndDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private int originalSiblingIndex; // NEW FIELD

    [SerializeField] private Canvas canvas;
    [SerializeField] private ComparisonGameManager gameManager;
    [SerializeField] private DropZone correctZone;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex(); // STORE INITIAL INDEX
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 🛑 CHECK: Prevent drag if input is locked
        if (InputManager.InputLocked) return;

        // Existing drag start logic:
        transform.SetParent(canvas.transform);
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 🛑 CHECK: Prevent drag movement if input is locked
        if (InputManager.InputLocked) return;

        // Existing drag movement logic:
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    // MODIFIED: Passes the event data and the original index.
    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // Pass control to the Manager, including the EventData and the order index.
        gameManager.RegisterDrop(gameObject, rectTransform, correctZone, originalParent, eventData, originalSiblingIndex);
    }
}