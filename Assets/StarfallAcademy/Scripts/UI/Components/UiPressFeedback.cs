using UnityEngine;
using UnityEngine.EventSystems;

namespace StarfallAcademy.Lobby
{
    public sealed class UiPressFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        Vector3 targetScale = Vector3.one;

        void OnEnable()
        {
            transform.localScale = Vector3.one;
            targetScale = Vector3.one;
        }

        void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * 18f);
        }

        public void OnPointerEnter(PointerEventData eventData) => targetScale = Vector3.one * 1.025f;
        public void OnPointerExit(PointerEventData eventData) => targetScale = Vector3.one;
        public void OnPointerDown(PointerEventData eventData) => targetScale = Vector3.one * .96f;
        public void OnPointerUp(PointerEventData eventData) => targetScale = eventData.pointerEnter == gameObject
            ? Vector3.one * 1.025f : Vector3.one;
    }
}
