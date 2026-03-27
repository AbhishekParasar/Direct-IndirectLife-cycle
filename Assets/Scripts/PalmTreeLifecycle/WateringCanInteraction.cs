using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace PalmTreeLifecycle
{
    [RequireComponent(typeof(Image))]
    public class WateringCanInteraction : MonoBehaviour, IPointerClickHandler
    {
        [Header("Settings")]
        [SerializeField] private Sprite wateredSprite;
        [SerializeField] private bool interactable = true;

        [Header("Events")]
        public UnityEvent onWateringComplete;

        private Image _image;
        private Sprite _originalSprite;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _originalSprite = _image.sprite;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable) return;

            Water();
        }

        public void Water()
        {
            interactable = false;
            
            if (wateredSprite != null)
            {
                _image.sprite = wateredSprite;
            }

            Debug.Log("Watering Can Used!");
            onWateringComplete?.Invoke();
        }

        public void ResetCan()
        {
            _image.sprite = _originalSprite;
            interactable = true;
        }

        public void SetInteractable(bool state)
        {
            interactable = state;
        }
    }
}
