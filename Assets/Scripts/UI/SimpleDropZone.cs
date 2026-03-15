using UnityEngine;
using UnityEngine.UI;

namespace DonGame2D.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SimpleDropZone : MonoBehaviour
    {
        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public bool IsPositionInside(Vector2 anchoredPos)
        {
            // 矩形範囲内にあるか判定
            return rectTransform.rect.Contains(anchoredPos);
        }
    }
}
