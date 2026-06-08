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

    private float cooldownRemaining;
    private float cooldownTextRefreshTimer;
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

        isDragging = true;
        skillCaster.BeginPlacement(skillDefinition, skillLevel, eventData.position);
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
        bool castSuccess = skillCaster.TryCast(eventData.position);
        if (castSuccess)
        {
            StartCooldown();
        }
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
            bool castSuccess = skillCaster.TryCast(eventData.position);
            if (castSuccess)
            {
                StartCooldown();
            }

            return;
        }

        if (!CanBeginPlacement())
        {
            return;
        }

        skillCaster.BeginPlacement(skillDefinition, skillLevel, eventData.position);
    }

    // 쿨타임과 필수 참조를 확인해 범위 지정 가능 여부를 판단한다.
    private bool CanBeginPlacement()
    {
        if (skillDefinition == null || skillCaster == null)
        {
            Debug.LogWarning("[헬기 스킬] 슬롯에 스킬 데이터 또는 캐스터가 연결되지 않았습니다.", this);
            return false;
        }

        if (cooldownRemaining > 0f)
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

    // 쿨타임을 시작한다.
    private void StartCooldown()
    {
        cooldownRemaining = skillDefinition != null ? skillDefinition.Cooldown : 0f;
        cooldownTextRefreshTimer = 0f;
        RefreshCooldownUI(true);
    }

    // 쿨타임 시간을 감소시키고 UI를 갱신한다.
    private void UpdateCooldown()
    {
        if (cooldownRemaining <= 0f)
        {
            return;
        }

        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
        cooldownTextRefreshTimer -= Time.deltaTime;

        if (cooldownTextRefreshTimer <= 0f || cooldownRemaining <= 0f)
        {
            cooldownTextRefreshTimer = 0.1f;
            RefreshCooldownUI(false);
        }
    }

    // 쿨타임 오버레이와 텍스트를 현재 상태에 맞춘다.
    private void RefreshCooldownUI(bool force)
    {
        float cooldown = skillDefinition != null ? skillDefinition.Cooldown : 0f;
        float ratio = cooldown > 0f ? cooldownRemaining / cooldown : 0f;

        if (cooldownFillImage != null)
        {
            cooldownFillImage.fillAmount = ratio;
            cooldownFillImage.enabled = ratio > 0f;
        }

        if (cooldownText != null && (force || cooldownTextRefreshTimer <= 0.1f))
        {
            cooldownText.text = cooldownRemaining > 0f ? Mathf.CeilToInt(cooldownRemaining).ToString() : string.Empty;
        }
    }
}
