using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System;

namespace DonGame2D.UI
{
    /// <summary>
    /// 人数選択・メニュー選択用のカードコンポーネント。
    /// ドラッグ＆ドロップによる中心エリア投下での選択に対応します。
    /// </summary>
    public class SelectionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Settings")]
        public int playerCount;
        public string selectionId;
        public float hoverYOffset = 30f;
        public float hoverScale = 1.05f;
        public float animDuration = 0.2f;
        public bool isInteractable = true;
        public Vector3 baseScale = Vector3.one; // 基準となるスケール（Vector3で非一様スケーリングに対応）
        private int baseSortingOrder = 500; // 基準となる重なり順

        [SerializeField] private Vector2 baseAnchoredPos; // 扇状配置時の本来の座標
        [SerializeField] private Quaternion baseRotation; // 扇状配置時の本来の回転
        public RectTransform rectTransform;
        private Transform originalParent;
        private Coroutine activeCoroutine;
        private Vector2 dragOffset;
        private bool isEnteringCenter = false;
        private bool isFlyingBack = false;

        public bool IsFlyingBack => isFlyingBack;


        public Action<int> OnSelected;
        public Action<string> OnIdSelected;
        
        // Callback when the card is successfully dropped and slid into the center
        public Action<SelectionCard> OnCardDropped;
        
        // Callback as soon as the slide to center BEGINS (for early stack registration)
        public Action<SelectionCard> OnSelectionStarted;
        
        // ドロップ時に中央判定を行うための外部参照
        public Func<Vector2, bool> IsInDropZone;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            isEnteringCenter = false;
            // 再コンパイル時などはシリアライズされた値を保持し、初期値（全ゼロ）の場合のみ現在の状態を保存
            if (baseRotation.x == 0 && baseRotation.y == 0 && baseRotation.z == 0 && baseRotation.w == 0)
            {
                baseAnchoredPos = rectTransform.anchoredPosition;
                baseRotation = rectTransform.localRotation;
            }
            originalParent = transform.parent;
        }

        private void OnEnable()
        {
            isEnteringCenter = false;
        }



        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractable || isEnteringCenter) return;
            AnimateTo(baseAnchoredPos, Vector3.Scale(baseScale, Vector3.one * hoverScale), baseRotation);
        }



        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isInteractable || isEnteringCenter) return;
            AnimateTo(baseAnchoredPos, baseScale, baseRotation);
        }



        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable || isEnteringCenter || isFlyingBack) return;
            
            // Note: Click-to-select disabled as per request.
            // Clicks now only provide visual feedback (done via Enter/Exit).
        }

                public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isInteractable) return;
            isEnteringCenter = false; // Reset if we start dragging after a drop
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out dragOffset);
            
            // --- Drag-on-Top Implementation ---
            // Temporarily grant ultra-high priority during drag
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20000; // Above stack (10000)
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            
            transform.SetAsLastSibling();
            
            // ドラッグ開始時にまっすぐにする
            rectTransform.localRotation = Quaternion.identity;
        }


        public void OnDrag(PointerEventData eventData)
        {
            if (!isInteractable) return;
            Vector2 mousePos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform.parent as RectTransform, eventData.position, eventData.pressEventCamera, out mousePos))
            {
                rectTransform.anchoredPosition = mousePos - dragOffset;
            }
        }

                public void OnEndDrag(PointerEventData eventData)
        {
            if (!isInteractable) return;
            if (IsInDropZone != null && IsInDropZone(rectTransform.anchoredPosition))
            {
                // Notify early
                OnSelectionStarted?.Invoke(this);
                
                // 中央にスライドしてから選択確定
                if (gameObject.activeInHierarchy)
                    StartCoroutine(Co_SlideIntoCenter());
            }
            else
            {
                // Restore base sorting order when returned to hand
                var canvas = GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = baseSortingOrder;
                }

                AnimateTo(baseAnchoredPos, baseScale, baseRotation);
            }
        }

        private IEnumerator Co_SlideIntoCenter()
        {
            isEnteringCenter = true;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);

            Vector2 startPos = rectTransform.anchoredPosition;
            Quaternion startRot = rectTransform.localRotation;
            Vector2 targetPos = new Vector2(0, 50f); // DropZoneArea の中心付近
            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                rectTransform.localScale = Vector3.Lerp(baseScale, baseScale * 0.8f, t);
                // 確実にまっすぐ（Identity）へ向かわせる
                rectTransform.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, t);
                yield return null;
            }
            
            // Note: OnSelectionStarted was moved to OnPointerClick/OnEndDrag 
            // to ensure it happens BEFORE HideAllCards (which might be triggered by legacy buttons)

            rectTransform.anchoredPosition = targetPos;
            rectTransform.localRotation = Quaternion.identity;
            
            // Allow clicking again after animation finishes if we are still active
            isEnteringCenter = false; 

            OnCardDropped?.Invoke(this); // Add to stack FIRST
            OnSelected?.Invoke(playerCount);
            OnIdSelected?.Invoke(selectionId);
        }

        public void ResetCardState()
        {
            if (rectTransform == null) 
            {
                rectTransform = GetComponent<RectTransform>();
                if (baseRotation.x == 0 && baseRotation.y == 0 && baseRotation.z == 0 && baseRotation.w == 0)
                {
                    baseAnchoredPos = rectTransform.anchoredPosition;
                    baseRotation = rectTransform.localRotation;
                }
                originalParent = transform.parent;
            }
            StopAllCoroutines();
            activeCoroutine = null;
            isEnteringCenter = false;
            isFlyingBack = false;
            isInteractable = true; // Always enable when resetting
            
            if (originalParent != null && transform.parent != originalParent)
            {
                transform.SetParent(originalParent, false);
            }

            rectTransform.anchoredPosition = baseAnchoredPos;
            rectTransform.localRotation = baseRotation;
            rectTransform.localScale = baseScale;
            
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = baseSortingOrder;
            }

            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        public void FlyBack(bool hideAtEnd = true)
        {
            isEnteringCenter = false;
            isFlyingBack = true;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            
            if (originalParent != null && transform.parent != originalParent)
            {
                transform.SetParent(originalParent, false);
            }

            gameObject.SetActive(true);
            activeCoroutine = StartCoroutine(Co_FlyBack(hideAtEnd));
        }

        private IEnumerator Co_FlyBack(bool hideAtEnd)
        {
            // Reset scale/alpha immediately just in case
            var canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            Vector2 startPos = rectTransform.anchoredPosition;
            Quaternion startRot = rectTransform.localRotation;
            Vector3 startScale = rectTransform.localScale;
            
            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, baseAnchoredPos, t);
                rectTransform.localScale = Vector3.Lerp(startScale, baseScale, t);
                rectTransform.localRotation = Quaternion.Slerp(startRot, baseRotation, t);
                yield return null;
            }

            rectTransform.anchoredPosition = baseAnchoredPos;
            rectTransform.localRotation = baseRotation;
            rectTransform.localScale = baseScale;
            
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = baseSortingOrder;
            }
            
            isFlyingBack = false;
            if (hideAtEnd) gameObject.SetActive(false);
        }

        public void SetBaseSortingOrder(int order)
        {
            baseSortingOrder = order;
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = baseSortingOrder;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        }



        public void SetOriginalPosition(Vector2 pos)
        {
            baseAnchoredPos = pos;
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            rectTransform.anchoredPosition = pos;
        }

        public void PlayFlyIn(Vector2 targetPos, Quaternion rotation, float delay)
        {
            gameObject.SetActive(true); // Safety measure
            isEnteringCenter = false; // Reset state when showing the card again
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            
            // Immediately reset position/scale/alpha BEFORE the coroutine delay to prevent lingering graphics
            rectTransform.anchoredPosition = new Vector2(0, -500f);
            rectTransform.localScale = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            var canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            baseAnchoredPos = targetPos;
            baseRotation = rotation;
            StartCoroutine(Co_FlyIn(targetPos, rotation, delay));
        }

        private IEnumerator Co_FlyIn(Vector2 targetPos, Quaternion rotation, float delay)
        {
            // Initial state set in PlayFlyIn to prevent flickering/lingering
            yield return new WaitForSeconds(delay);

            var canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            float elapsed = 0f;
            float duration = 0.5f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(0, -500f), targetPos, t);
                rectTransform.localScale = Vector3.Lerp(Vector3.zero, baseScale, t);
                rectTransform.localRotation = Quaternion.Slerp(Quaternion.identity, rotation, t);
                canvasGroup.alpha = t;
                yield return null;
            }

            rectTransform.anchoredPosition = targetPos;
            rectTransform.localScale = baseScale;
            rectTransform.localRotation = rotation;
            canvasGroup.alpha = 1f;
        }

        private void AnimateTo(Vector2 targetPos, Vector3 targetScale, Quaternion? targetRot = null)
        {
            if (isEnteringCenter) return;
            
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(Co_Animate(targetPos, targetScale, targetRot ?? rectTransform.localRotation));
        }


        private IEnumerator Co_Animate(Vector2 targetPos, Vector3 targetScale, Quaternion targetRot)
        {
            Vector2 startPos = rectTransform.anchoredPosition;
            Vector3 startScale = rectTransform.localScale;
            Quaternion startRot = rectTransform.localRotation;
            float elapsed = 0f;

            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / animDuration);
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
                rectTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            rectTransform.anchoredPosition = targetPos;
            rectTransform.localScale = targetScale;
            rectTransform.localRotation = targetRot;
            activeCoroutine = null;
        }
    }
}
