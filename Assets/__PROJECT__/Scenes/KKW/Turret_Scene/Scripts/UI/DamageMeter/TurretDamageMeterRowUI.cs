using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 딜 미터기에서 터렛 하나의 순위, 데미지, 점유율, 바 길이를 표시한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class TurretDamageMeterRowUI : MonoBehaviour
{
    [Header("텍스트")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("데미지와 비중을 함께 표시합니다. 예: 10.50k (32.1%)")]
    [SerializeField] private TMP_Text damageText;

    [Header("이미지")]
    [SerializeField] private Image barFillImage;

    [Header("그래프 크기")]
    [SerializeField] private RectTransform barFillRect;
    [SerializeField] private bool resizeBarWidth = true;
    [SerializeField, Min(1.0f)] private float maxBarWidth = 1.0f;
    [SerializeField, Range(0.0f, 1.0f)] private float barAlphaMultiplier = 0.85f;

    [Header("이동 연출")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;

    [Header("투명도")]
    [SerializeField] private CanvasGroup canvasGroup;

    private RectTransform rectTransform;
    private float targetY;
    private float yVelocity;

    // 시작 시 RectTransform을 캐시하고 현재 위치를 목표 위치로 맞춘다
    private void Awake()
    {
        rectTransform = transform as RectTransform;
        ValidateRequiredReferences();
        CacheBarFillRect();
        CacheMaxBarWidth();

        if (rectTransform != null)
        {
            targetY = rectTransform.anchoredPosition.y;
        }
    }

    // 매 프레임 목표 Y 위치로 부드럽게 이동한다
    private void Update()
    {
        if (rectTransform == null)
        {
            return;
        }

        Vector2 position = rectTransform.anchoredPosition;
        position.y = Mathf.SmoothDamp(position.y, targetY, ref yVelocity, smoothTime);
        rectTransform.anchoredPosition = position;
    }

    // Row 활성 상태를 변경한다
    public void SetVisible(bool isVisible)
    {
        if (gameObject.activeSelf != isVisible)
        {
            gameObject.SetActive(isVisible);
        }
    }

    // Row가 이동할 목표 Y 위치를 설정한다
    public void SetTargetY(float targetY_)
    {
        targetY = targetY_;
    }

    // Row의 전체 투명도를 변경한다
    public void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    // Row의 현재 Y 위치와 목표 Y 위치를 즉시 맞춘다
    public void SetCurrentY(float currentY)
    {
        targetY = currentY;
        yVelocity = 0.0f;

        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (rectTransform == null)
        {
            return;
        }

        Vector2 position = rectTransform.anchoredPosition;
        position.y = currentY;
        rectTransform.anchoredPosition = position;
    }

    // Row의 표시 값을 갱신한다
    public void Refresh(int rank, string displayName, float damage, float totalPercent, float barRatio, Color barColor)
    {
        if (rankText != null)
        {
            rankText.text = "[" + rank + "]";
        }

        if (nameText != null)
        {
            nameText.text = displayName;
        }

        if (damageText != null)
        {
            damageText.text = FormatDamageText(damage, totalPercent);
        }

        if (barFillImage != null)
        {
            ApplyBarAmount(barRatio);
            ApplyBarColor(barColor);
        }
    }

    // 그래프 Fill 이미지의 RectTransform을 캐시한다
    private void CacheBarFillRect()
    {
        if (barFillRect == null && barFillImage != null)
        {
            barFillRect = barFillImage.rectTransform;
        }
    }

    // 인스펙터 값이 비어 있으면 현재 그래프 폭을 최대 폭으로 사용한다
    private void CacheMaxBarWidth()
    {
        if (barFillRect == null || maxBarWidth > 1.0f)
        {
            return;
        }

        maxBarWidth = Mathf.Max(1.0f, barFillRect.rect.width);
    }

    // 설정된 방식에 따라 그래프 길이를 적용한다
    private void ApplyBarAmount(float barRatio)
    {
        float safeRatio = Mathf.Clamp01(barRatio);
        if (resizeBarWidth && barFillRect != null)
        {
            if (barFillImage != null)
            {
                barFillImage.fillAmount = 1.0f;
            }

            Vector2 sizeDelta = barFillRect.sizeDelta;
            sizeDelta.x = maxBarWidth * safeRatio;
            barFillRect.sizeDelta = sizeDelta;
            return;
        }

        if (barFillImage != null)
        {
            barFillImage.fillAmount = safeRatio;
        }
    }

    // 색상 프로필의 RGB를 유지하고 표시용 알파 배율을 반영한다
    private void ApplyBarColor(Color barColor)
    {
        if (barFillImage == null)
        {
            return;
        }

        barColor.a *= barAlphaMultiplier;
        barFillImage.color = barColor;
    }

    // 데미지와 전체 비중을 한 문자열로 구성한다
    private static string FormatDamageText(float damage, float totalPercent)
    {
        return FormatCompactDamage(damage) + " (" + (Mathf.Clamp01(totalPercent) * 100.0f).ToString("0.0") + "%)";
    }

    // 큰 데미지 수치를 k, m, b 단위로 축약한다
    private static string FormatCompactDamage(float damage)
    {
        float safeDamage = Mathf.Max(0.0f, damage);
        if (safeDamage < 1000.0f)
        {
            return Mathf.FloorToInt(safeDamage).ToString();
        }

        if (safeDamage < 1000000.0f)
        {
            return (safeDamage / 1000.0f).ToString("0.00") + "k";
        }

        if (safeDamage < 1000000000.0f)
        {
            return (safeDamage / 1000000.0f).ToString("0.00") + "m";
        }

        return (safeDamage / 1000000000.0f).ToString("0.00") + "b";
    }

    // 런타임에 필요한 직접 연결 참조가 있는지 검증한다
    private void ValidateRequiredReferences()
    {
        if (rankText == null)
        {
            Debug.LogWarning("[딜 미터 행 UI] Rank Text 참조가 비어 있어 순위를 표시할 수 없습니다.", this);
        }

        if (nameText == null)
        {
            Debug.LogWarning("[딜 미터 행 UI] Name Text 참조가 비어 있어 터렛 이름을 표시할 수 없습니다.", this);
        }

        if (damageText == null)
        {
            Debug.LogWarning("[딜 미터 행 UI] Damage Text 참조가 비어 있어 데미지 값을 표시할 수 없습니다.", this);
        }

        if (barFillImage == null)
        {
            Debug.LogWarning("[딜 미터 행 UI] Bar Fill Image 참조가 비어 있어 그래프를 표시할 수 없습니다.", this);
        }

        if (canvasGroup == null)
        {
            Debug.LogWarning("[딜 미터 행 UI] CanvasGroup 참조가 비어 있어 접기 투명도 연출을 적용할 수 없습니다.", this);
        }
    }
}
