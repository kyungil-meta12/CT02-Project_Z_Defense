using UnityEngine;

/// <summary>
/// 터렛 관련 성공 피드백 팝업 문구를 씬에서 설정하고 표시한다.
/// </summary>
public class TurretFeedbackPopupSettings : MonoBehaviour
{
    private const string DEFAULT_PLACEMENT_SUCCESS_MESSAGE = "터렛 설치 성공";
    private const string DEFAULT_EVOLUTION_SUCCESS_MESSAGE = "터렛 진화 성공";
    private const int DEFAULT_PLACEMENT_SUCCESS_MESSAGE_COUNT = 5;
    private const int DEFAULT_EVOLUTION_SUCCESS_MESSAGE_COUNT = 5;
    private const int DEFAULT_FINAL_EVOLUTION_SUCCESS_MESSAGE_COUNT = 3;

    public static TurretFeedbackPopupSettings Inst { get; private set; }

    [Header("터렛 설치 성공 문구")]
    [SerializeField]
    private string[] placementSuccessMessages =
    {
        "터렛 설치 완료",
        "방어 준비 완료",
        "새 터렛이 배치되었습니다",
        "화력 거점 확보",
        "터렛 전개 성공"
    };

    [Header("1세대 진화 성공 문구")]
    [SerializeField]
    private string[] firstGenerationEvolutionSuccessMessages =
    {
        "터렛 진화 완료",
        "초기 화력 강화 완료",
        "1세대 전투 체계 확장",
        "새 전술 분기가 열렸습니다",
        "기초 방어선이 강화되었습니다"
    };

    [Header("2세대 진화 성공 문구")]
    [SerializeField]
    private string[] secondGenerationEvolutionSuccessMessages =
    {
        "2세대 터렛 전개 완료",
        "전문화 화력 체계 활성화",
        "고급 터렛 진화 성공",
        "전장 화력이 한 단계 상승했습니다",
        "2세대 전투 모듈 가동"
    };

    [Header("3세대 진화 성공 문구")]
    [SerializeField]
    private string[] thirdGenerationEvolutionSuccessMessages =
    {
        "최종 진화 완료",
        "3세대 터렛 기동",
        "결전 병기가 활성화되었습니다"
    };

    // 싱글톤 참조를 등록한다
    private void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Debug.LogWarning("[TurretFeedbackPopupSettings] 중복 설정 컴포넌트가 있어 기존 인스턴스를 유지합니다.", this);
            return;
        }

        Inst = this;
    }

    // 싱글톤 참조를 정리한다
    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    // 인스펙터 문구 배열을 기본 편집 개수로 보정한다
    private void OnValidate()
    {
        EnsureMessageArraySize(ref placementSuccessMessages, DEFAULT_PLACEMENT_SUCCESS_MESSAGE_COUNT);
        EnsureMessageArraySize(ref firstGenerationEvolutionSuccessMessages, DEFAULT_EVOLUTION_SUCCESS_MESSAGE_COUNT);
        EnsureMessageArraySize(ref secondGenerationEvolutionSuccessMessages, DEFAULT_EVOLUTION_SUCCESS_MESSAGE_COUNT);
        EnsureMessageArraySize(ref thirdGenerationEvolutionSuccessMessages, DEFAULT_FINAL_EVOLUTION_SUCCESS_MESSAGE_COUNT);
    }

    // 터렛 설치 성공 문구 중 하나를 랜덤으로 표시한다
    public static void ShowRandomPlacementSuccess()
    {
        string message = Inst == null
            ? DEFAULT_PLACEMENT_SUCCESS_MESSAGE
            : Inst.GetRandomPlacementSuccessMessage();

        WarningPopupManager.ShowWarning(message);
    }

    // 터렛 진화 단계에 맞는 성공 문구 중 하나를 랜덤으로 표시한다
    public static void ShowRandomEvolutionSuccess(TurretDefinitionSO sourceDefinition, TurretDefinitionSO targetDefinition)
    {
        string message = Inst == null
            ? DEFAULT_EVOLUTION_SUCCESS_MESSAGE
            : Inst.GetRandomEvolutionSuccessMessage(sourceDefinition, targetDefinition);

        WarningPopupManager.ShowWarning(message);
    }

    // 설정된 터렛 설치 성공 문구 중 하나를 반환한다
    private string GetRandomPlacementSuccessMessage()
    {
        return GetRandomMessage(placementSuccessMessages, DEFAULT_PLACEMENT_SUCCESS_MESSAGE);
    }

    // 진화 대상 세대에 맞는 랜덤 성공 문구를 반환한다
    private string GetRandomEvolutionSuccessMessage(TurretDefinitionSO sourceDefinition, TurretDefinitionSO targetDefinition)
    {
        EvolutionFeedbackMessageGroup messageGroup = ResolveEvolutionMessageGroup(sourceDefinition, targetDefinition);
        switch (messageGroup)
        {
            case EvolutionFeedbackMessageGroup.ThirdGeneration:
                return GetRandomMessage(thirdGenerationEvolutionSuccessMessages, DEFAULT_EVOLUTION_SUCCESS_MESSAGE);
            case EvolutionFeedbackMessageGroup.SecondGeneration:
                return GetRandomMessage(secondGenerationEvolutionSuccessMessages, DEFAULT_EVOLUTION_SUCCESS_MESSAGE);
            default:
                return GetRandomMessage(firstGenerationEvolutionSuccessMessages, DEFAULT_EVOLUTION_SUCCESS_MESSAGE);
        }
    }

    // 진화 대상 터렛 ID를 기준으로 사용할 문구 그룹을 결정한다
    private static EvolutionFeedbackMessageGroup ResolveEvolutionMessageGroup(TurretDefinitionSO sourceDefinition, TurretDefinitionSO targetDefinition)
    {
        string targetId = GetNormalizedTurretId(targetDefinition);
        if (IsThirdGenerationTurretId(targetId))
        {
            return EvolutionFeedbackMessageGroup.ThirdGeneration;
        }

        if (IsSecondGenerationTurretId(targetId))
        {
            return EvolutionFeedbackMessageGroup.SecondGeneration;
        }

        string sourceId = GetNormalizedTurretId(sourceDefinition);
        return IsSecondGenerationTurretId(sourceId)
            ? EvolutionFeedbackMessageGroup.SecondGeneration
            : EvolutionFeedbackMessageGroup.FirstGeneration;
    }

    // 터렛 ID를 비교 가능한 소문자 형식으로 변환한다
    private static string GetNormalizedTurretId(TurretDefinitionSO definition)
    {
        if (definition == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(definition.turretId)
            ? string.Empty
            : definition.turretId.Trim().ToLowerInvariant();
    }

    // 터렛 ID가 2세대 형식인지 확인한다
    private static bool IsSecondGenerationTurretId(string turretId)
    {
        return turretId.EndsWith("_1") || turretId.EndsWith("_2") || turretId.EndsWith("_3");
    }

    // 터렛 ID가 3세대 최종 터렛인지 확인한다
    private static bool IsThirdGenerationTurretId(string turretId)
    {
        return turretId == "frost_turret" ||
               turretId == "poison_turret" ||
               turretId == "electro_turret" ||
               turretId == "ignition_turret";
    }

    // 문구 배열에서 비어 있지 않은 항목 하나를 랜덤으로 반환한다
    private static string GetRandomMessage(string[] messages, string fallbackMessage)
    {
        if (messages == null || messages.Length == 0)
        {
            return fallbackMessage;
        }

        int startIndex = Random.Range(0, messages.Length);
        for (int i = 0; i < messages.Length; i++)
        {
            int index = (startIndex + i) % messages.Length;
            string message = messages[index];
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }
        }

        return fallbackMessage;
    }

    // 인스펙터 문구 배열의 최소 칸 수를 보장한다
    private static void EnsureMessageArraySize(ref string[] messages, int minimumSize)
    {
        if (messages == null || messages.Length == 0)
        {
            messages = new string[minimumSize];
            return;
        }

        if (messages.Length < minimumSize)
        {
            System.Array.Resize(ref messages, minimumSize);
        }
    }

    private enum EvolutionFeedbackMessageGroup
    {
        FirstGeneration,
        SecondGeneration,
        ThirdGeneration
    }
}
