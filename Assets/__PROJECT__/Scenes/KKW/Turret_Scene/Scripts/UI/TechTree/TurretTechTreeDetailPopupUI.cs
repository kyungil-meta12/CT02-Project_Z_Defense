using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 터렛 트리 노드 클릭 시 터렛 1레벨 상세 스펙과 인게임 프리뷰 영상을 표시한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretTechTreeDetailPopupUI : MonoBehaviour
{
    private const int PREVIEW_LEVEL = 1;

    [Header("표시 루트")]
    [SerializeField] private GameObject popupRoot;

    [Header("버튼")]
    [SerializeField] private Button closeButton;

    [Header("텍스트")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private TMP_Text fireRateText;
    [SerializeField] private TMP_Text projectileSpeedText;
    [SerializeField] private TMP_Text projectileCountText;
    [SerializeField] private TMP_Text pierceCountText;

    [Header("영상")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoImage;
    [SerializeField] private Image fallbackIconImage;
    [SerializeField] private GameObject missingVideoMessageRoot;

    // 컴포넌트 추가 시 기본 루트와 버튼 참조를 자동 연결한다
    private void Reset()
    {
        popupRoot = gameObject;
        closeButton = GetComponentInChildren<Button>(true);
        videoPlayer = GetComponentInChildren<VideoPlayer>(true);
    }

    // 시작 전에 닫기 버튼을 연결하고 팝업을 숨긴다
    private void Awake()
    {
        if (popupRoot == null)
        {
            popupRoot = gameObject;
        }

        BindButton();
        Hide();
    }

    // 파괴 시 닫기 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButton();
    }

    // 지정 터렛 노드의 상세 정보와 프리뷰 영상을 표시한다
    public void Show(TurretDefinitionSO definition, TurretTechTreeNodeViewData nodeData, TurretTechTreeNodeState state, TurretTechTreeViewProfileSO profile)
    {
        if (definition == null)
        {
            Hide();
            return;
        }

        RefreshTexts(definition, nodeData, state, profile);
        RefreshVideo(definition, nodeData);

        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }
    }

    // 상세 팝업을 닫고 재생 중인 영상을 정지한다
    public void Hide()
    {
        StopVideo();

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    // 텍스트 참조에 터렛 기본 정보와 1레벨 스탯을 반영한다
    private void RefreshTexts(TurretDefinitionSO definition, TurretTechTreeNodeViewData nodeData, TurretTechTreeNodeState state, TurretTechTreeViewProfileSO profile)
    {
        TurretRuntimeStat stat = TurretStatCalculator.Calculate(definition, PREVIEW_LEVEL);
        SetText(nameText, GetDisplayName(definition));
        SetText(stateText, profile == null ? string.Empty : profile.GetStateText(state));
        SetText(descriptionText, GetDescription(definition, nodeData));
        SetText(damageText, FormatLabeledValue("공격력", stat.damage));
        SetText(rangeText, FormatLabeledValue("사거리", stat.range));
        SetText(fireRateText, FormatLabeledValue("발사간격", stat.fireInterval));
        SetText(projectileSpeedText, FormatLabeledValue("탄속", stat.projectileSpeed));
        SetText(projectileCountText, "투사체 수 " + stat.projectileCount);
        SetText(pierceCountText, "관통 " + stat.pierceCount);
    }

    // 노드 데이터의 영상 클립을 VideoPlayer에 연결해 루프 재생한다
    private void RefreshVideo(TurretDefinitionSO definition, TurretTechTreeNodeViewData nodeData)
    {
        VideoClip clip = nodeData == null ? null : nodeData.PreviewClip;
        bool hasClip = clip != null && videoPlayer != null;

        if (videoImage != null)
        {
            videoImage.gameObject.SetActive(hasClip);
        }

        if (fallbackIconImage != null)
        {
            fallbackIconImage.gameObject.SetActive(!hasClip);
            fallbackIconImage.sprite = definition.uiIcon;
            fallbackIconImage.enabled = definition.uiIcon != null;
            fallbackIconImage.preserveAspect = true;
        }

        if (missingVideoMessageRoot != null)
        {
            missingVideoMessageRoot.SetActive(!hasClip);
        }

        if (!hasClip)
        {
            StopVideo();
            return;
        }

        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.isLooping = true;
        videoPlayer.playOnAwake = false;
        videoPlayer.Prepare();
        videoPlayer.Play();
    }

    // 현재 VideoPlayer 재생을 멈추고 클립 참조를 비운다
    private void StopVideo()
    {
        if (videoPlayer == null)
        {
            return;
        }

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        videoPlayer.clip = null;
    }

    // 닫기 버튼 클릭 이벤트를 등록한다
    private void BindButton()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(Hide);
        closeButton.onClick.AddListener(Hide);
    }

    // 닫기 버튼 클릭 이벤트를 해제한다
    private void UnbindButton()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
        }
    }

    // 터렛 정의와 노드 데이터에서 설명 문구를 결정한다
    private static string GetDescription(TurretDefinitionSO definition, TurretTechTreeNodeViewData nodeData)
    {
        if (nodeData != null && !string.IsNullOrWhiteSpace(nodeData.PreviewDescription))
        {
            return nodeData.PreviewDescription;
        }

        return definition == null ? string.Empty : definition.shortDescription;
    }

    // 터렛 정의의 표시 이름을 반환한다
    private static string GetDisplayName(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
    }

    // 수치 라벨 문자열을 생성한다
    private static string FormatLabeledValue(string label, float value)
    {
        return label + " " + value.ToString("0.##");
    }

    // 텍스트 참조가 있을 때만 문자열을 적용한다
    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }
}
