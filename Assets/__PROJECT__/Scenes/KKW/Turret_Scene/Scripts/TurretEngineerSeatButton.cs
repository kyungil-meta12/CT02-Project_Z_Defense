using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 터렛 업그레이드 UI 상단의 엔지니어 탑승 해제 트리거 버튼을 관리한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretEngineerSeatButton : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;

    private TurretTemporaryUpgradePopupUI owner;
    private int seatIndex = -1;
    private bool isBound;

    // 컴포넌트 추가 시 하위 UI 참조를 자동으로 연결한다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 활성화 전에 참조를 보완하고 클릭 이벤트를 연결한다
    private void Awake()
    {
        AutoBindReferences();
        BindButton();
    }

    // 파괴될 때 클릭 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButton();
    }

    // 버튼이 가리킬 탑승 슬롯 인덱스와 표시 라벨을 갱신한다
    public void Configure(TurretTemporaryUpgradePopupUI owner_, int seatIndex_, string label)
    {
        owner = owner_;
        seatIndex = seatIndex_;
        AutoBindReferences();
        BindButton();

        if (labelText != null)
        {
            labelText.text = label;
        }

        if (button != null)
        {
            button.interactable = owner != null && seatIndex >= 0;
        }
    }

    // 버튼을 빈 상태로 초기화하고 숨긴다
    public void Clear()
    {
        owner = null;
        seatIndex = -1;

        if (labelText != null)
        {
            labelText.text = string.Empty;
        }

        if (button != null)
        {
            button.interactable = false;
        }
    }

    // 클릭 시 소유 UI에 하차 요청을 전달한다
    private void OnButtonClicked()
    {
        if (owner == null || seatIndex < 0)
        {
            return;
        }

        owner.OnEngineerSeatButtonClicked(seatIndex);
    }

    // 필요한 Button과 TMP_Text 참조를 자동으로 찾는다
    private void AutoBindReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TMP_Text>(true);
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
}
