using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 딜 미터기에서 터렛 하나의 순위, 데미지, 점유율, 바 길이를 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TurretDamageMeterRowUI : MonoBehaviour
{
    [Header("텍스트")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text percentText;

    [Header("이미지")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image barFillImage;

    [Header("이동 연출")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;

    private RectTransform rectTransform;
    private float targetY;
    private float yVelocity;

    // 시작 시 RectTransform을 캐시하고 현재 위치를 목표 위치로 맞춘다
    private void Awake()
    {
        rectTransform = transform as RectTransform;
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

    // Row의 표시 값을 갱신한다
    public void Refresh(int rank, string displayName, float damage, float totalPercent, float barRatio, Sprite icon, Color barColor)
    {
        if (rankText != null)
        {
            rankText.text = rank.ToString();
        }

        if (nameText != null)
        {
            nameText.text = displayName;
        }

        if (damageText != null)
        {
            damageText.text = damage.ToString("N0");
        }

        if (percentText != null)
        {
            percentText.text = (Mathf.Clamp01(totalPercent) * 100.0f).ToString("0.0") + "%";
        }

        if (barFillImage != null)
        {
            barFillImage.fillAmount = Mathf.Clamp01(barRatio);
            barFillImage.color = barColor;
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }
}
