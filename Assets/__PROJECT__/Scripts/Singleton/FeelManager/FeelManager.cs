using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// 공용 Feel 피드백 참조와 런타임 피드백 재생 정책을 관리한다.
/// </summary>
public class FeelManager : MonoBehaviour
{
    public static FeelManager Inst;

    [Header("피드백 재생 설정")]
    // 게이트/장애물 반복 피격음이 과도하게 겹쳐 원활한 플레이테스트를 심각하게 방해하므로 테스트 중 즉시 끌 수 있게 둔다
    [Tooltip("게이트/장애물 반복 피격음이 과도하게 겹쳐 플레이테스트를 방해할 때 끕니다.")]
    [SerializeField] private bool enableAttackFeedback = true;

    [Header("피드백 참조")]
    public MMFeedbacks attackFeedback;
    public MMFeedbacks tankSkillFeedback;
    public MMFeedbacks boomerSkillFeedback;
    public MMFeedbacks screamerSkillFeedback;
    public MMFeedbacks obstacleBrokenFeedback;

    public bool EnableAttackFeedback => enableAttackFeedback;

    // 싱글톤 인스턴스를 초기화한다
    private void Awake()
    {
        if(Inst && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
    }

    // 싱글톤 인스턴스가 자신일 때만 해제한다
    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    // 장애물 피격 피드백을 현재 설정에 따라 재생한다
    public void PlayAttackFeedback(Vector3 position)
    {
        if (!enableAttackFeedback)
        {
            return;
        }

        attackFeedback?.PlayFeedbacks(position);
    }

    // UI 버튼에서 장애물 피격 피드백 재생 여부를 전환한다
    public void ToggleAttackFeedback()
    {
        enableAttackFeedback = !enableAttackFeedback;
    }

    // UI 토글에서 장애물 피격 피드백 재생 여부를 지정한다
    public void SetAttackFeedbackEnabled(bool isEnabled)
    {
        enableAttackFeedback = isEnabled;
    }
}
