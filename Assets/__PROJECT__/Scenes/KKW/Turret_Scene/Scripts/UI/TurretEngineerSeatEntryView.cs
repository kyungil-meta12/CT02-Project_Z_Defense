using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 터렛 업그레이드 팝업의 엔지니어 탑승 좌석 버튼 하나를 표시한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretEngineerSeatEntryView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text buffText;

    private int seatIndex = -1;
    private Action<int> clickHandler;
    private bool isBound;

    // 컴포넌트 추가 시 참조를 자동으로 찾는다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 활성화 전에 참조를 보완하고 버튼 이벤트를 연결한다
    private void Awake()
    {
        AutoBindReferences();
        BindButton();
    }

    // 파괴 시 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButton();
    }

    // 엔지니어 좌석 표시 데이터와 클릭 콜백을 갱신한다
    public void Render(TurretEngineerSeatViewModel model, Action<int> onClicked)
    {
        bool isVisible = model != null && model.IsVisible;
        gameObject.SetActive(isVisible);
        if (!isVisible)
        {
            clickHandler = null;
            seatIndex = -1;
            return;
        }

        seatIndex = model.SeatIndex;
        clickHandler = onClicked;
        SetText(labelText, model.Label);
        SetText(buffText, model.BuffText);

        if (button != null)
        {
            button.interactable = model.IsInteractable;
        }
    }

    // 빈 좌석 슬롯으로 초기화한다
    public void Clear()
    {
        clickHandler = null;
        seatIndex = -1;
        gameObject.SetActive(false);
    }

    // 버튼 클릭 시 좌석 인덱스를 상위 View에 전달한다
    private void OnButtonClicked()
    {
        if (clickHandler == null || seatIndex < 0)
        {
            return;
        }

        clickHandler.Invoke(seatIndex);
    }

    // 필요한 버튼과 텍스트 참조를 자동으로 찾는다
    private void AutoBindReferences()
    {
        button = button != null ? button : GetComponent<Button>();
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        labelText = labelText != null ? labelText : GetTextByName(texts, "Label", 0);
        buffText = buffText != null ? buffText : GetTextByName(texts, "Buff", 1);
    }

    // 버튼 클릭 이벤트를 중복 없이 연결한다
    private void BindButton()
    {
        if (isBound || button == null)
        {
            return;
        }

        button.onClick.AddListener(OnButtonClicked);
        isBound = true;
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButton()
    {
        if (!isBound || button == null)
        {
            return;
        }

        button.onClick.RemoveListener(OnButtonClicked);
        isBound = false;
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
