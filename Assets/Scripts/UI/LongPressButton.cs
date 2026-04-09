using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections;

namespace DonGame2D.UI
{
    public class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public UnityEvent onLongPress = new UnityEvent();
        public float initialDelay = 0.5f;
        public float repeatInterval = 0.15f;

        private bool isPressed = false;
        private Coroutine repeatCoroutine;

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            if (repeatCoroutine != null) StopCoroutine(repeatCoroutine);
            repeatCoroutine = StartCoroutine(RepeatAction());
        }

        public void OnPointerUp(PointerEventData eventData) => StopPress();
        public void OnPointerExit(PointerEventData eventData) => StopPress();

        private void OnDisable() => StopPress();

        private void StopPress()
        {
            isPressed = false;
            if (repeatCoroutine != null)
            {
                StopCoroutine(repeatCoroutine);
                repeatCoroutine = null;
            }
        }

        private IEnumerator RepeatAction()
        {
            yield return new WaitForSeconds(initialDelay);
            while (isPressed)
            {
                onLongPress.Invoke();
                yield return new WaitForSeconds(repeatInterval);
            }
        }
    }
}