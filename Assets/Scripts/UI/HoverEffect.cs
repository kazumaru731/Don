using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class HoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scaling Settings")]
    public float hoverScale = 1.1f;
    public float animationDuration = 0.15f;

    [Header("Glow Settings")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(1.2f, 1.2f, 1.2f, 1f); // Brighten beyond white for "glow"

    private Text targetText;
    private Vector3 originalScale;
    private Coroutine currentCoroutine;

    void Awake()
    {
        targetText = GetComponentInChildren<Text>();
        originalScale = transform.localScale;
        if (targetText != null)
        {
            normalColor = targetText.color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        StopAnimation();
        currentCoroutine = StartCoroutine(AnimateHover(hoverScale, hoverColor));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopAnimation();
        currentCoroutine = StartCoroutine(AnimateHover(1.0f, normalColor));
    }

    private void StopAnimation()
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }
    }

    private IEnumerator AnimateHover(float targetScaleFactor, Color targetColor)
    {
        Vector3 startScale = transform.localScale;
        Vector3 endScale = originalScale * targetScaleFactor;
        
        Color startColor = Color.white;
        if (targetText != null) startColor = targetText.color;

        float elapsed = 0;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / animationDuration;
            
            // Smooth step for nicer feel
            float smoothPercent = Mathf.SmoothStep(0, 1, percent);

            transform.localScale = Vector3.Lerp(startScale, endScale, smoothPercent);
            if (targetText != null)
            {
                targetText.color = Color.Lerp(startColor, targetColor, smoothPercent);
            }
            yield return null;
        }

        transform.localScale = endScale;
        if (targetText != null) targetText.color = targetColor;
    }

    void OnDisable()
    {
        // Reset state when object is disabled to prevent sticking
        StopAnimation();
        transform.localScale = originalScale;
        if (targetText != null) targetText.color = normalColor;
    }
}
