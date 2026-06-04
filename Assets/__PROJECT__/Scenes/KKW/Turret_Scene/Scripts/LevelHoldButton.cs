using UnityEngine;
using UnityEngine.EventSystems;

public class LevelHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private TurretEvolutionRuntimeUI owner;

    public void Initialize(TurretEvolutionRuntimeUI owner_)
    {
        owner = owner_;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.BeginLevelHold();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.EndLevelHold();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.EndLevelHold();
        }
    }
}
