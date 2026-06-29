using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 터렛 업그레이드 또는 진화 실행 버튼 하나를 표시하고 클릭 이벤트를 전달한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretUpgradeActionButtonView : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text stateText;

    private TurretUpgradeActionType actionType;
    private int actionIndex = -1;
    private Action<TurretUpgradeActionType, int> clickHandler;
    private bool isBound;

    // 컴포넌트 추가 시 버튼과 하위 텍스트 참조를 자동으로 찾는다
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

    // 버튼 표시 데이터와 클릭 콜백을 갱신한다
    public void Render(TurretUpgradeActionViewModel model, Action<TurretUpgradeActionType, int> onClicked)
    {
        bool isVisible = model != null && model.IsVisible;
        gameObject.SetActive(isVisible);
        if (!isVisible)
        {
            clickHandler = null;
            actionIndex = -1;
            return;
        }

        actionType = model.ActionType;
        actionIndex = model.ActionIndex;
        clickHandler = onClicked;

        SetText(titleText, model.Title);
        SetText(costText, model.CostText);
        SetText(stateText, model.StateText);
        ApplyIcon(model.Icon);

        if (button != null)
        {
            button.interactable = model.IsInteractable;
        }
    }

    // 빈 버튼 슬롯으로 초기화한다
    public void Clear()
    {
        clickHandler = null;
        actionIndex = -1;
        gameObject.SetActive(false);
    }

    // 버튼 클릭 시 현재 동작 타입과 인덱스를 상위 View에 전달한다
    private void OnButtonClicked()
    {
        if (clickHandler == null || actionIndex < 0)
        {
            return;
        }

        clickHandler.Invoke(actionType, actionIndex);
    }

    // 필요한 버튼과 텍스트 참조를 자동으로 찾는다
    private void AutoBindReferences()
    {
        button = button != null ? button : GetComponent<Button>();
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        titleText = titleText != null ? titleText : GetTextByName(texts, "Title", 0);
        costText = costText != null ? costText : GetTextByName(texts, "Cost", 1);
        stateText = stateText != null ? stateText : GetTextByName(texts, "State", 2);

        if (iconImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject != gameObject)
                {
                    iconImage = images[i];
                    break;
                }
            }
        }
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

    // 아이콘 스프라이트와 표시 여부를 적용한다
    private void ApplyIcon(Sprite icon)
    {
        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;
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
