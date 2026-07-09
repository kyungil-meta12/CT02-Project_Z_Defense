using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 진화 후보 버튼의 누르고 있기 입력을 TurretEvolutionPopupUI로 전달한다.
/// </summary>
public sealed class TurretEvolutionCandidatePressForwarder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private TurretEvolutionPopupUI owner;
    private int candidateIndex;

    // 전달 대상 팝업과 후보 인덱스를 설정한다
    public void Initialize(TurretEvolutionPopupUI owner_, int candidateIndex_)
    {
        owner = owner_;
        candidateIndex = candidateIndex_;
    }

    // 포인터 누르기 시작을 전달한다
    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.NotifyCandidatePointerDown(candidateIndex);
        }
    }

    // 포인터 누르기 종료를 전달한다
    public void OnPointerUp(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.NotifyCandidatePointerUp(candidateIndex);
        }
    }

    // 포인터가 버튼 영역을 벗어난 상태를 전달한다
    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.NotifyCandidatePointerExit(candidateIndex);
        }
    }
}
