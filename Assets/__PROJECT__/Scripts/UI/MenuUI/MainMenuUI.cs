using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴 패널과 오디오 설정 팝업의 표시, 닫기, 뒤로가기 입력을 제어한다.
/// </summary>
public class MainMenuUI : TouchBackHandler
{
    [Header("오디오 설정 팝업")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private Button audioSettingButton;
    [SerializeField] private Button audioCloseButton;
    [SerializeField] private bool hideAudioPanelOnEnable = true;

    private CanvasGroup group;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    // 메인 메뉴 뒤로가기 동작을 등록한다
    private void Start()
    {
        ValidateSerializedReferences();
        OnTouchBackAction += HandleTouchBack;
    }

    // 활성화 시 버튼 입력을 연결하고 팝업 초기 표시 상태를 정리한다
    private void OnEnable()
    {
        BindButtonListeners();

        if (hideAudioPanelOnEnable)
        {
            HideAudioPanel();
        }

        UIManager.Inst.HideAll();
    }

    // 비활성화 시 버튼 입력 연결을 해제한다
    private void OnDisable()
    {
        UnbindButtonListeners();
        if(UIManager.Inst)
        {
            UIManager.Inst.RevertAll();
        }
    }

    // 파괴 시 뒤로가기 동작 등록을 해제한다
    private void OnDestroy()
    {
        OnTouchBackAction -= HandleTouchBack;
    }

    // 매 프레임 뒤로가기 입력을 확인한다
    private void Update()
    {
        if(group.interactable)
        {
            UpdateTouchBackHandler();
        }
    }

    // 오디오 설정 팝업을 표시한다
    public void ShowAudioPanel()
    {
        if (audioPanel == null)
        {
            Debug.LogWarning("[MainMenuUI] 오디오 설정 패널 참조가 비어 있어 팝업을 열 수 없습니다.", this);
            return;
        }

        audioPanel.SetActive(true);
    }

    // 오디오 설정 팝업을 숨긴다
    public void HideAudioPanel()
    {
        if (audioPanel != null)
        {
            audioPanel.SetActive(false);
        }
    }

    // 오디오 팝업 상태에 따라 뒤로가기 입력을 처리한다
    private void HandleTouchBack()
    {
        if (audioPanel != null && audioPanel.activeSelf)
        {
            HideAudioPanel();
            return;
        }

        gameObject.SetActive(false);
        UIManager.Inst.RevertGameUI();
    }

    // 버튼 클릭 이벤트를 등록한다
    private void BindButtonListeners()
    {
        UnbindButtonListeners();

        if (audioSettingButton != null)
        {
            audioSettingButton.onClick.AddListener(ShowAudioPanel);
        }

        if (audioCloseButton != null)
        {
            audioCloseButton.onClick.AddListener(HideAudioPanel);
        }
    }

    // 버튼 클릭 이벤트를 해제한다
    private void UnbindButtonListeners()
    {
        if (audioSettingButton != null)
        {
            audioSettingButton.onClick.RemoveListener(ShowAudioPanel);
        }

        if (audioCloseButton != null)
        {
            audioCloseButton.onClick.RemoveListener(HideAudioPanel);
        }
    }

    // Inspector에서 연결해야 하는 오디오 설정 참조 누락을 알린다
    private void ValidateSerializedReferences()
    {
        if (audioPanel == null)
        {
            Debug.LogWarning("[MainMenuUI] Audio Panel 참조가 비어 있습니다. 오디오 설정 팝업을 열 수 없습니다.", this);
        }

        if (audioSettingButton == null)
        {
            Debug.LogWarning("[MainMenuUI] Audio Setting Button 참조가 비어 있습니다.", this);
        }

        if (audioCloseButton == null)
        {
            Debug.LogWarning("[MainMenuUI] Audio Close Button 참조가 비어 있습니다.", this);
        }
    }
}
