using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HelicopterMissileSkillSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private HelicopterMissileSkillCaster skillCaster;
    [SerializeField] private HelicopterMissileSkillDefinitionSO skillDefinition;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image cooldownFillImage;
    [SerializeField] private TMP_Text cooldownText;

    [Header("Runtime")]
    [Min(1)] [SerializeField] private int skillLevel = 1;

    private float cooldownTextRefreshTimer;
    private int lastCooldownTextValue = -1;
    private bool isDragging;

    // 기본 UI 참조를 자동 연결한다.
    private void Reset()
    {
        iconImage = GetComponentInChildren<Image>();
    }

    // 슬롯 초기 표시 상태를 갱신한다.
    private void Awake()
    {
        RefreshIcon();
        RefreshCooldownUI(true);
    }

    // 쿨타임 표시를 갱신한다.
    private void Update()
    {
        UpdateCooldown();
    }

    // 인스펙터가 아닌 런타임에서 스킬 슬롯 데이터를 주입한다.
    public void Initialize(HelicopterMissileSkillDefinitionSO definition, HelicopterMissileSkillCaster caster, int level)
    {
        skillDefinition = definition;
        skillCaster = caster;
        skillLevel = Mathf.Max(1, level);
        RefreshIcon();
        RefreshCooldownUI(true);
    }

    // 드래그 시작 시 범위 지정을 시작한다.
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanBeginPlacement())
        {
            return;
        }

        isDragging = skillCaster.BeginPlacement(skillDefinition, skillLevel, eventData.position);
    }

    // 드래그 중 범위 프리뷰를 이동한다.
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || skillCaster == null)
        {
            return;
        }

        skillCaster.UpdatePlacement(eventData.position);
    }

    // 드래그 종료 시 현재 위치에 스킬을 발동한다.
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || skillCaster == null)
        {
            return;
        }

        isDragging = false;
        skillCaster.TryCast(eventData.position);
    }

    // 클릭 입력으로 범위 지정 시작 또는 발동을 처리한다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (skillCaster == null)
        {
            return;
        }

        if (skillCaster.IsPlacing)
        {
            skillCaster.TryCast(eventData.position);
            return;
        }

        if (!CanBeginPlacement())
        {
            return;
        }

        skillCaster.BeginPlacement(skillDefinition, skillLevel, eventData.position);
    }

    // 필수 참조와 캐스터 사용 가능 상태를 확인한다.
    private bool CanBeginPlacement()
    {
        if (skillDefinition == null || skillCaster == null)
        {
            Debug.LogWarning("[헬기 스킬] 슬롯에 스킬 데이터 또는 캐스터가 연결되지 않았습니다.", this);
            return false;
        }

        if (!skillCaster.CanUseSkill())
        {
            return false;
        }

        return true;
    }

    // 스킬 아이콘을 데이터 기준으로 갱신한다.
    private void RefreshIcon()
    {
        if (iconImage == null || skillDefinition == null || skillDefinition.Icon == null)
        {
            return;
        }

        iconImage.sprite = skillDefinition.Icon;
        iconImage.enabled = true;
    }

    // 쿨타임 시간을 감소시키고 UI를 갱신한다.
    private void UpdateCooldown()
    {
        if (skillCaster == null)
        {
            RefreshCooldownUI(false);
            return;
        }

        cooldownTextRefreshTimer -= Time.deltaTime;

        if (cooldownTextRefreshTimer <= 0f || skillCaster.CooldownRemaining <= 0f)
        {
            cooldownTextRefreshTimer = 0.1f;
            RefreshCooldownUI(false);
        }
    }

    // 쿨타임 오버레이와 텍스트를 현재 상태에 맞춘다.
    private void RefreshCooldownUI(bool force)
    {
        float cooldownRemaining = skillCaster != null ? skillCaster.CooldownRemaining : 0f;
        float ratio = skillCaster != null ? skillCaster.CooldownRatio : 0f;

        if (cooldownFillImage != null)
        {
            cooldownFillImage.fillAmount = ratio;
            cooldownFillImage.enabled = ratio > 0f;
        }

        if (cooldownText != null && (force || cooldownTextRefreshTimer <= 0.1f))
        {
            int cooldownTextValue = cooldownRemaining > 0f ? Mathf.CeilToInt(cooldownRemaining) : 0;
            if (force || cooldownTextValue != lastCooldownTextValue)
            {
                cooldownText.text = cooldownTextValue > 0 ? cooldownTextValue.ToString() : string.Empty;
                lastCooldownTextValue = cooldownTextValue;
            }
        }
    }
}
