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
    [SerializeField] private TMP_Text pierceCountText;
    [SerializeField] private TMP_Text criticalChanceText;
    [SerializeField] private TMP_Text heavyHitChanceText;
    [SerializeField] private TMP_Text damageNumberText;
    [SerializeField] private TMP_Text rangeNumberText;
    [SerializeField] private TMP_Text fireRateNumberText;
    [SerializeField] private TMP_Text pierceCountNumberText;
    [SerializeField] private TMP_Text criticalChanceNumberText;
    [SerializeField] private TMP_Text heavyHitChanceNumberText;

    [Header("영상")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoImage;
    [SerializeField] private Image fallbackIconImage;
    [SerializeField] private GameObject missingVideoMessageRoot;
    [SerializeField] private bool useVideoUvRect;
    [SerializeField] private Rect videoUvRect = new Rect(0.34f, 0.0f, 0.32f, 1.0f);

    private string nameTextTemplate;
    private string damageTextTemplate;
    private string rangeTextTemplate;
    private string fireRateTextTemplate;
    private string pierceCountTextTemplate;
    private string criticalChanceTextTemplate;
    private string heavyHitChanceTextTemplate;
    private TurretTechTreeViewProfileSO activeProfile;
    private bool isVideoFadeActive;
    private bool hasRequiredReferences;

    // 컴포넌트 추가 시 기본 루트와 버튼 참조를 자동 연결한다
    private void Reset()
    {
        popupRoot = gameObject;
        closeButton = GetComponentInChildren<Button>(true);
        videoPlayer = GetComponentInChildren<VideoPlayer>(true);
        BindChildReferences();
    }

    // 시작 전에 닫기 버튼을 연결하고 팝업을 숨긴다
    private void Awake()
    {
        hasRequiredReferences = ValidateRequiredReferences();
        CacheTextTemplates();
        BindButton();
        Hide();
    }

    // 재생 중인 프리뷰 영상의 시작/끝 페이드 알파를 갱신한다
    private void Update()
    {
        UpdateVideoFade();
    }

    // 파괴 시 닫기 버튼 이벤트를 해제한다
    private void OnDestroy()
    {
        UnbindButton();
    }

    // 지정 터렛 노드의 상세 정보와 프리뷰 영상을 표시한다
    public void Show(TurretDefinitionSO definition, TurretTechTreeNodeViewData nodeData, TurretTechTreeNodeState state, TurretTechTreeViewProfileSO profile)
    {
        hasRequiredReferences = ValidateRequiredReferences();
        if (!hasRequiredReferences)
        {
            Debug.LogWarning("[터렛 트리 상세 UI] 필수 인스펙터 참조가 누락되어 상세 팝업을 표시할 수 없습니다.", this);
            return;
        }

        if (definition == null)
        {
            Hide();
            return;
        }

        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }

        CacheTextTemplates();
        RefreshTexts(definition, state, profile);
        RefreshVideo(definition, nodeData, profile);
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
    private void RefreshTexts(TurretDefinitionSO definition, TurretTechTreeNodeState state, TurretTechTreeViewProfileSO profile)
    {
        TurretRuntimeStat stat = TurretStatCalculator.Calculate(definition, PREVIEW_LEVEL);
        TurretDamagePolishProfileSO damagePolishProfile = definition.damagePolishProfile;
        SetText(nameText, ApplyNameTemplate(nameTextTemplate, GetDisplayName(definition)));
        SetText(stateText, profile == null ? string.Empty : profile.GetStateText(state));
        HideDescriptionText();
        SetStatText(damageText, damageNumberText, damageTextTemplate, FormatValue(stat.damage));
        SetStatText(rangeText, rangeNumberText, rangeTextTemplate, FormatValue(stat.range));
        SetStatText(fireRateText, fireRateNumberText, fireRateTextTemplate, FormatValue(stat.fireInterval));
        SetStatText(pierceCountText, pierceCountNumberText, pierceCountTextTemplate, stat.pierceCount.ToString());
        SetStatText(criticalChanceText, criticalChanceNumberText, criticalChanceTextTemplate, FormatChance(GetCriticalChance(damagePolishProfile)));
        SetStatText(heavyHitChanceText, heavyHitChanceNumberText, heavyHitChanceTextTemplate, FormatChance(GetHeavyHitChance(damagePolishProfile)));
    }

    // 노드 데이터의 영상 클립을 VideoPlayer에 연결해 루프 재생한다
    private void RefreshVideo(TurretDefinitionSO definition, TurretTechTreeNodeViewData nodeData, TurretTechTreeViewProfileSO profile)
    {
        VideoClip clip = nodeData == null ? null : nodeData.PreviewClip;
        bool hasClip = clip != null && videoPlayer != null;
        activeProfile = profile;
        isVideoFadeActive = false;

        if (videoImage != null)
        {
            videoImage.gameObject.SetActive(hasClip);
            SetVideoImageAlpha(ShouldUseVideoFade(profile) ? 0.0f : 1.0f);
            videoImage.uvRect = useVideoUvRect ? videoUvRect : new Rect(0.0f, 0.0f, 1.0f, 1.0f);
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

        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.isLooping = true;
        videoPlayer.playOnAwake = false;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.Prepare();
    }

    // 현재 VideoPlayer 재생을 멈추고 클립 참조를 비운다
    private void StopVideo()
    {
        activeProfile = null;
        isVideoFadeActive = false;
        SetVideoImageAlpha(1.0f);

        if (videoPlayer == null)
        {
            return;
        }

        videoPlayer.prepareCompleted -= OnVideoPrepared;
        if (videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        videoPlayer.clip = null;
    }

    // 영상 준비가 끝나면 현재 클립을 재생한다
    private void OnVideoPrepared(VideoPlayer preparedPlayer)
    {
        if (preparedPlayer == null || preparedPlayer.clip == null)
        {
            return;
        }

        isVideoFadeActive = ShouldUseVideoFade(activeProfile);
        SetVideoImageAlpha(isVideoFadeActive ? 0.0f : 1.0f);
        preparedPlayer.Play();
    }

    // 런타임에 필요한 인스펙터 참조가 모두 연결됐는지 확인한다
    private bool ValidateRequiredReferences()
    {
        bool isValid = true;
        isValid &= LogMissingReference(popupRoot, nameof(popupRoot));
        isValid &= LogMissingReference(closeButton, nameof(closeButton));
        isValid &= LogMissingReference(nameText, nameof(nameText));
        isValid &= LogMissingReference(stateText, nameof(stateText));
        isValid &= LogMissingReference(damageText, nameof(damageText));
        isValid &= LogMissingReference(rangeText, nameof(rangeText));
        isValid &= LogMissingReference(fireRateText, nameof(fireRateText));
        isValid &= LogMissingReference(pierceCountText, nameof(pierceCountText));
        isValid &= LogMissingReference(criticalChanceText, nameof(criticalChanceText));
        isValid &= LogMissingReference(heavyHitChanceText, nameof(heavyHitChanceText));
        isValid &= LogMissingReference(damageNumberText, nameof(damageNumberText));
        isValid &= LogMissingReference(rangeNumberText, nameof(rangeNumberText));
        isValid &= LogMissingReference(fireRateNumberText, nameof(fireRateNumberText));
        isValid &= LogMissingReference(pierceCountNumberText, nameof(pierceCountNumberText));
        isValid &= LogMissingReference(criticalChanceNumberText, nameof(criticalChanceNumberText));
        isValid &= LogMissingReference(heavyHitChanceNumberText, nameof(heavyHitChanceNumberText));
        isValid &= LogMissingReference(videoPlayer, nameof(videoPlayer));
        isValid &= LogMissingReference(videoImage, nameof(videoImage));
        isValid &= LogMissingReference(fallbackIconImage, nameof(fallbackIconImage));
        isValid &= LogMissingReference(missingVideoMessageRoot, nameof(missingVideoMessageRoot));
        return isValid;
    }

    // 단일 인스펙터 참조 누락 여부를 로그로 알린다
    private bool LogMissingReference(UnityEngine.Object reference, string fieldName)
    {
        if (reference != null)
        {
            return true;
        }

        Debug.LogWarning("[터렛 트리 상세 UI] " + fieldName + " 참조가 비어 있습니다. 인스펙터에서 직접 연결해야 합니다.", this);
        return false;
    }

    // 영상 시간 기준으로 시작 페이드인과 끝 페이드아웃을 계산한다
    private void UpdateVideoFade()
    {
        if (!isVideoFadeActive || videoPlayer == null || videoImage == null || videoPlayer.clip == null)
        {
            return;
        }

        double clipLength = videoPlayer.length > 0.0 ? videoPlayer.length : videoPlayer.clip.length;
        if (clipLength <= 0.0)
        {
            SetVideoImageAlpha(1.0f);
            return;
        }

        float alpha = CalculateVideoFadeAlpha(videoPlayer.time, clipLength, activeProfile);
        SetVideoImageAlpha(alpha);
    }

    // 프로필 설정에 따라 프리뷰 영상 페이드를 사용할지 반환한다
    private static bool ShouldUseVideoFade(TurretTechTreeViewProfileSO profile)
    {
        if (profile == null || !profile.UsePreviewVideoFade)
        {
            return false;
        }

        return profile.PreviewVideoFadeInDuration > 0.0f || profile.PreviewVideoFadeOutDuration > 0.0f;
    }

    // 현재 영상 시간에 맞는 페이드 알파를 계산한다
    private static float CalculateVideoFadeAlpha(double time, double clipLength, TurretTechTreeViewProfileSO profile)
    {
        if (profile == null)
        {
            return 1.0f;
        }

        float alpha = 1.0f;
        float fadeInDuration = profile.PreviewVideoFadeInDuration;
        float fadeOutDuration = profile.PreviewVideoFadeOutDuration;

        if (fadeInDuration > 0.0f)
        {
            alpha = Mathf.Min(alpha, Mathf.Clamp01((float)(time / fadeInDuration)));
        }

        if (fadeOutDuration > 0.0f)
        {
            alpha = Mathf.Min(alpha, Mathf.Clamp01((float)((clipLength - time) / fadeOutDuration)));
        }

        return alpha;
    }

    // 영상 RawImage의 알파만 변경한다
    private void SetVideoImageAlpha(float alpha)
    {
        if (videoImage == null)
        {
            return;
        }

        Color color = videoImage.color;
        color.a = Mathf.Clamp01(alpha);
        videoImage.color = color;
    }

    [ContextMenu("참조 다시 연결")]
    // 상세 팝업 하위 오브젝트 이름을 기준으로 인스펙터 참조를 다시 연결한다
    public void BindChildReferences()
    {
        closeButton = closeButton != null ? closeButton : FindComponentByName<Button>("ExitButton");
        nameText = nameText != null ? nameText : FindComponentByName<TMP_Text>("TurretNameText");
        stateText = stateText != null ? stateText : FindComponentByName<TMP_Text>("StateText");
        descriptionText = descriptionText != null ? descriptionText : FindComponentByName<TMP_Text>("DescriptionText");
        damageText = damageText != null ? damageText : FindComponentByName<TMP_Text>("DamageText");
        rangeText = rangeText != null ? rangeText : FindComponentByName<TMP_Text>("RangeText");
        fireRateText = fireRateText != null ? fireRateText : FindComponentByName<TMP_Text>("FireRateText");
        pierceCountText = pierceCountText != null ? pierceCountText : FindComponentByName<TMP_Text>("PierceCountText");
        criticalChanceText = criticalChanceText != null ? criticalChanceText : FindComponentByName<TMP_Text>("CriticalChance");
        heavyHitChanceText = heavyHitChanceText != null ? heavyHitChanceText : FindFirstComponentByName<TMP_Text>("HeavyHitChance", "HeavtHitChance");
        damageNumberText = damageNumberText != null ? damageNumberText : FindStatNumberText(damageText);
        rangeNumberText = rangeNumberText != null ? rangeNumberText : FindStatNumberText(rangeText);
        fireRateNumberText = fireRateNumberText != null ? fireRateNumberText : FindStatNumberText(fireRateText);
        pierceCountNumberText = pierceCountNumberText != null ? pierceCountNumberText : FindStatNumberText(pierceCountText);
        criticalChanceNumberText = criticalChanceNumberText != null ? criticalChanceNumberText : FindStatNumberText(criticalChanceText);
        heavyHitChanceNumberText = heavyHitChanceNumberText != null ? heavyHitChanceNumberText : FindStatNumberText(heavyHitChanceText);
        videoPlayer = videoPlayer != null ? videoPlayer : GetComponentInChildren<VideoPlayer>(true);
        videoImage = videoImage != null ? videoImage : FindComponentByName<RawImage>("PreviewRawImage");
        fallbackIconImage = fallbackIconImage != null ? fallbackIconImage : FindComponentByName<Image>("FallbackIconImage");

        if (missingVideoMessageRoot == null)
        {
            Transform missingVideoMessage = FindChildByName(transform, "MissingVideoMessage");
            missingVideoMessageRoot = missingVideoMessage == null ? null : missingVideoMessage.gameObject;
        }
    }

    // TMP 원문 템플릿을 보관해 중괄호와 고정 문구를 유지한다
    private void CacheTextTemplates()
    {
        CacheTemplate(nameText, ref nameTextTemplate);
        CacheTemplate(damageText, ref damageTextTemplate);
        CacheTemplate(rangeText, ref rangeTextTemplate);
        CacheTemplate(fireRateText, ref fireRateTextTemplate);
        CacheTemplate(pierceCountText, ref pierceCountTextTemplate);
        CacheTemplate(criticalChanceText, ref criticalChanceTextTemplate);
        CacheTemplate(heavyHitChanceText, ref heavyHitChanceTextTemplate);
    }

    // 비어 있는 템플릿 저장소에 현재 TMP 원문을 저장한다
    private static void CacheTemplate(TMP_Text text, ref string template)
    {
        if (text != null && string.IsNullOrEmpty(template))
        {
            template = text.text;
        }
    }

    // 상세 설명 텍스트는 현재 팝업에서 사용하지 않으므로 숨긴다
    private void HideDescriptionText()
    {
        if (descriptionText == null)
        {
            return;
        }

        descriptionText.text = string.Empty;
        descriptionText.gameObject.SetActive(false);
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

    // 터렛 정의의 표시 이름을 반환한다
    private static string GetDisplayName(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
    }

    // 단일 수치 값을 소수점 둘째 자리까지 표시한다
    private static string FormatValue(float value)
    {
        return value.ToString("0.##");
    }

    // 확률 값을 백분율 문자열로 변환한다
    private static string FormatChance(float chance)
    {
        return Mathf.Clamp01(chance).ToString("0.#%");
    }

    // 데미지 폴리싱 프로필에서 치명타 확률을 반환한다
    private static float GetCriticalChance(TurretDamagePolishProfileSO damagePolishProfile)
    {
        return damagePolishProfile == null ? 0.0f : damagePolishProfile.CriticalChance;
    }

    // 데미지 폴리싱 프로필에서 강타 확률을 반환한다
    private static float GetHeavyHitChance(TurretDamagePolishProfileSO damagePolishProfile)
    {
        return damagePolishProfile == null ? 0.0f : damagePolishProfile.HeavyHitChance;
    }

    // 템플릿의 중괄호 구간을 값으로 교체한다
    private static string ApplyTemplate(string template, string value)
    {
        if (string.IsNullOrEmpty(template))
        {
            return value;
        }

        int openIndex = template.IndexOf('{');
        int closeIndex = template.IndexOf('}', openIndex + 1);
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return value;
        }

        return template.Substring(0, openIndex) + value + template.Substring(closeIndex + 1);
    }

    // 이름 템플릿이 없으면 대괄호 안에 터렛 이름을 표시한다
    private static string ApplyNameTemplate(string template, string value)
    {
        if (string.IsNullOrEmpty(template))
        {
            return "[" + value + "]";
        }

        int openIndex = template.IndexOf('{');
        int closeIndex = template.IndexOf('}', openIndex + 1);
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return "[" + value + "]";
        }

        return ApplyTemplate(template, value);
    }

    // 텍스트 참조가 있을 때만 문자열을 적용한다
    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    // 스탯 숫자 전용 TMP가 있으면 숫자만 적용하고 없으면 기존 템플릿 치환을 사용한다
    private static void SetStatText(TMP_Text labelText, TMP_Text numberText, string labelTemplate, string value)
    {
        if (numberText != null)
        {
            numberText.text = value;
            SetText(labelText, RemoveTemplatePlaceholder(labelTemplate));
            return;
        }

        SetText(labelText, ApplyTemplate(labelTemplate, value));
    }

    // 라벨 템플릿에 남아 있는 중괄호 자리표시자를 제거한다
    private static string RemoveTemplatePlaceholder(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        int openIndex = template.IndexOf('{');
        int closeIndex = template.IndexOf('}', openIndex + 1);
        if (openIndex < 0 || closeIndex < 0 || closeIndex <= openIndex)
        {
            return template;
        }

        return (template.Substring(0, openIndex) + template.Substring(closeIndex + 1)).TrimEnd();
    }

    // 라벨 TMP 하위의 숫자 표시 TMP를 반환한다
    private static TMP_Text FindStatNumberText(TMP_Text labelText)
    {
        if (labelText == null)
        {
            return null;
        }

        Transform numberTransform = FindChildByName(labelText.transform, "StatNumber");
        return numberTransform == null ? null : numberTransform.GetComponent<TMP_Text>();
    }

    // 여러 이름 후보 중 처음 찾은 자식 컴포넌트를 반환한다
    private T FindFirstComponentByName<T>(params string[] objectNames) where T : Component
    {
        if (objectNames == null)
        {
            return null;
        }

        for (int i = 0; i < objectNames.Length; i++)
        {
            T component = FindComponentByName<T>(objectNames[i]);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    // 지정 이름의 자식 컴포넌트를 반환한다
    private T FindComponentByName<T>(string objectName) where T : Component
    {
        Transform target = FindChildByName(transform, objectName);
        return target == null ? null : target.GetComponent<T>();
    }

    // 이름으로 자식 Transform을 재귀 검색한다
    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
