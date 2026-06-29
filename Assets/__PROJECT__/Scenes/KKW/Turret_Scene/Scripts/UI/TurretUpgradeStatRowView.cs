using TMPro;
using UnityEngine;

/// <summary>
/// 터렛 업그레이드 팝업의 스탯 한 줄을 표시한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretUpgradeStatRowView : MonoBehaviour
{
    [Header("텍스트 참조")]
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text currentValueText;
    [SerializeField] private TMP_Text nextValueText;
    [SerializeField] private TMP_Text deltaValueText;

    [Header("색상")]
    [SerializeField] private Color normalDeltaColor = Color.white;
    [SerializeField] private Color positiveDeltaColor = new Color(0.25f, 0.9f, 0.55f, 1.0f);

    // 컴포넌트 추가 시 자식 텍스트 참조를 자동으로 보완한다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 활성화 전에 자식 텍스트 참조를 보완한다
    private void Awake()
    {
        AutoBindReferences();
    }

    // 스탯 행 표시값을 갱신한다
    public void Render(TurretUpgradeStatViewModel model)
    {
        bool hasModel = model != null;
        gameObject.SetActive(hasModel);
        if (!hasModel)
        {
            return;
        }

        SetText(labelText, model.Label);
        SetText(currentValueText, model.CurrentValue);
        SetText(nextValueText, model.NextValue);
        SetText(deltaValueText, model.DeltaValue);

        if (deltaValueText != null)
        {
            deltaValueText.color = model.HasPositiveDelta ? positiveDeltaColor : normalDeltaColor;
        }
    }

    // 하위 TMP_Text를 이름 순서 기준으로 자동 연결한다
    private void AutoBindReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        labelText = labelText != null ? labelText : GetTextByName(texts, "Label", 0);
        currentValueText = currentValueText != null ? currentValueText : GetTextByName(texts, "Current", 1);
        nextValueText = nextValueText != null ? nextValueText : GetTextByName(texts, "Next", 2);
        deltaValueText = deltaValueText != null ? deltaValueText : GetTextByName(texts, "Delta", 3);
    }

    // 이름이 일치하는 텍스트를 우선 찾고 없으면 인덱스 fallback을 반환한다
    private static TMP_Text GetTextByName(TMP_Text[] texts, string namePart, int fallbackIndex)
    {
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text != null && text.name.Contains(namePart))
            {
                return text;
            }
        }

        return fallbackIndex >= 0 && fallbackIndex < texts.Length ? texts[fallbackIndex] : null;
    }

    // TMP_Text가 있을 때만 문자열을 대입한다
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }
}
