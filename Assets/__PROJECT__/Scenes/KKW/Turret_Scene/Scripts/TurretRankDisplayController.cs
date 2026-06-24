using TinyGiantStudio.Ranks;
using TMPro;
using UnityEngine;

/// <summary>
/// 터렛의 현재 Definition과 티어 레벨을 3D Rank 배지와 레벨 텍스트에 반영한다.
/// </summary>
[DisallowMultipleComponent]
public class TurretRankDisplayController : MonoBehaviour
{
    [Header("터렛 참조")]
    [SerializeField] private TurretDefinitionRuntimeController turretRuntime;

    [Header("랭크 표시")]
    [SerializeField] private TurretRankDisplayProfileSO rankDisplayProfile;
    [SerializeField] private RanksAnimationController rankAnimationController;
    [SerializeField] private bool hideRankWhenRuleMissing;

    [Header("레벨 표시")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private bool showTotalLevel;
    [SerializeField] private string tierLevelPrefix = "Lv.";
    [SerializeField] private string totalLevelPrefix = "Total Lv.";

    [Header("디버그")]
    [SerializeField] private bool logMissingReference = true;

    private int lastRankIndex = -1;
    private int lastTierLevel = -1;
    private int lastTotalLevel = -1;
    private bool isSubscribed;

    // 컴포넌트 추가 시 필요한 참조를 자동으로 수집한다
    private void Reset()
    {
        RefreshReferences();
    }

    // 시작 전 필요한 참조를 수집한다
    private void Awake()
    {
        RefreshReferences();
    }

    // 활성화 시 터렛 변경 이벤트를 구독하고 표시를 즉시 갱신한다
    private void OnEnable()
    {
        SubscribeRuntimeEvent();
        RefreshDisplay();
    }

    // 비활성화 시 터렛 변경 이벤트 구독을 해제한다
    private void OnDisable()
    {
        UnsubscribeRuntimeEvent();
    }

    // 현재 터렛 상태를 랭크 배지와 레벨 텍스트에 반영한다
    public void RefreshDisplay()
    {
        RefreshReferences();

        if (turretRuntime == null)
        {
            LogMissingReference("터렛 런타임 컨트롤러가 없어 랭크 표시를 갱신할 수 없습니다.");
            return;
        }

        RefreshRank();
        RefreshLevelText();
    }

    // 외부에서 랭크 표시 프로필을 교체하고 즉시 갱신한다
    public void SetRankDisplayProfile(TurretRankDisplayProfileSO profile)
    {
        rankDisplayProfile = profile;
        lastRankIndex = -1;
        RefreshDisplay();
    }

    // 터렛 런타임 적용 완료 이벤트를 받아 표시를 갱신한다
    private void HandleTurretApplied(TurretDefinitionRuntimeController runtimeController)
    {
        if (runtimeController != turretRuntime)
        {
            return;
        }

        RefreshDisplay();
    }

    // 현재 터렛 Definition과 티어 레벨에 맞는 랭크를 적용한다
    private void RefreshRank()
    {
        if (rankDisplayProfile == null)
        {
            LogMissingReference("랭크 표시 프로필이 없어 랭크 배지를 갱신할 수 없습니다.");
            return;
        }

        if (rankAnimationController == null)
        {
            LogMissingReference("3D Rank 애니메이션 컨트롤러가 없어 랭크 배지를 갱신할 수 없습니다.");
            return;
        }

        if (!rankDisplayProfile.TryGetRankIndex(turretRuntime.CurrentTurretDefinition, turretRuntime.CurrentTierLevel, out int rankIndex))
        {
            if (hideRankWhenRuleMissing)
            {
                rankAnimationController.DisableEverything();
            }

            return;
        }

        if (rankIndex == lastRankIndex)
        {
            return;
        }

        lastRankIndex = rankIndex;
        rankAnimationController.ApplyRank(rankIndex);
    }

    // 현재 터렛 레벨 값을 텍스트에 반영한다
    private void RefreshLevelText()
    {
        if (levelText == null)
        {
            return;
        }

        int tierLevel = turretRuntime.CurrentTierLevel;
        int totalLevel = turretRuntime.CurrentTotalLevel;
        if (tierLevel == lastTierLevel && totalLevel == lastTotalLevel)
        {
            return;
        }

        lastTierLevel = tierLevel;
        lastTotalLevel = totalLevel;

        if (showTotalLevel)
        {
            levelText.text = string.Concat(totalLevelPrefix, " ", totalLevel);
            return;
        }

        levelText.text = string.Concat(tierLevelPrefix, " ", tierLevel);
    }

    // 터렛 런타임 변경 이벤트를 중복 없이 구독한다
    private void SubscribeRuntimeEvent()
    {
        if (isSubscribed || turretRuntime == null)
        {
            return;
        }

        turretRuntime.Applied += HandleTurretApplied;
        isSubscribed = true;
    }

    // 터렛 런타임 변경 이벤트 구독을 해제한다
    private void UnsubscribeRuntimeEvent()
    {
        if (!isSubscribed || turretRuntime == null)
        {
            return;
        }

        turretRuntime.Applied -= HandleTurretApplied;
        isSubscribed = false;
    }

    // 필요한 컴포넌트 참조를 현재 오브젝트와 부모 계층에서 수집한다
    private void RefreshReferences()
    {
        if (turretRuntime == null)
        {
            turretRuntime = GetComponentInParent<TurretDefinitionRuntimeController>();
        }

        if (rankAnimationController == null)
        {
            rankAnimationController = GetComponent<RanksAnimationController>();
        }

        if (rankAnimationController == null)
        {
            rankAnimationController = GetComponentInChildren<RanksAnimationController>(true);
        }
    }

    // 누락 참조 경고를 설정에 따라 출력한다
    private void LogMissingReference(string message)
    {
        if (!logMissingReference)
        {
            return;
        }

        Debug.LogWarning("[TurretRankDisplayController] " + message, this);
    }
}
