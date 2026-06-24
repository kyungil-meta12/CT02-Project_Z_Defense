using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 경고 메시지 팝업의 표시 상태와 풀 반환 생명주기를 관리한다.
/// </summary>
public class WarningPopup : PoolObject
{
    [Header("자식 참조")] [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text messageText;

    private float duration;
    private float elapsedTime;
    private bool isInitialized;

    // 경고 팝업에 필요한 자식 UI 참조와 입력 차단 해제 상태를 준비한다
    private void Awake()
    {
        EnsureChildReferences();
        DisableRaycastTargets();
    }

    // 풀에서 꺼내기 전에 재사용 상태를 초기화한다
    public override void OnBeforeSpawn()
    {
        elapsedTime = 0f;
        duration = 0f;
        isInitialized = false;
        EnsureChildReferences();
        DisableRaycastTargets();
    }

    // 풀에 반환될 때 표시 상태를 정리한다
    public override void OnDespawn()
    {
        elapsedTime = 0f;
        duration = 0f;
        isInitialized = false;

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }

    /// <summary>
    /// 경고 팝업의 메시지, 아이콘, 표시 시간을 초기화한다.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="iconSprite"></param>
    /// <param name="duration_"></param>
    public void Init(string message, Sprite iconSprite, float duration_)
    {
        EnsureChildReferences();
        DisableRaycastTargets();

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.enabled = iconSprite != null;
        }

        elapsedTime = 0f;
        duration = Mathf.Max(0.01f, duration_);
        isInitialized = true;
    }

    // 즉시 풀로 반환할 수 있도록 표시 상태를 종료한다
    public void ForceReturnToPool()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        isInitialized = false;
        ReturnToPool();
    }

    // 표시 시간이 끝난 팝업을 풀로 반환한다
    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        elapsedTime += Time.deltaTime;
        if (elapsedTime >= duration)
        {
            isInitialized = false;
            ReturnToPool();
        }
    }

    // 자식 이미지와 텍스트 참조를 찾고 없으면 생성한다
    private void EnsureChildReferences()
    {
        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>(true);
        }

        if (messageText == null)
        {
            messageText = GetComponentInChildren<TMP_Text>(true);
        }

        if (iconImage == null)
        {
            iconImage = CreateIconImage();
        }

        if (messageText == null)
        {
            messageText = CreateMessageText();
        }
    }

    // 기본 아이콘 이미지 자식 오브젝트를 생성한다
    private Image CreateIconImage()
    {
        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(transform, false);
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(48f, 48f);

        Image createdImage = iconObject.GetComponent<Image>();
        createdImage.enabled = false;
        createdImage.raycastTarget = false;
        return createdImage;
    }

    // 기본 메시지 텍스트 자식 오브젝트를 생성한다
    private TMP_Text CreateMessageText()
    {
        GameObject textObject = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(transform, false);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(56f, 0f);
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI createdText = textObject.GetComponent<TextMeshProUGUI>();
        createdText.alignment = TextAlignmentOptions.MidlineLeft;
        createdText.raycastTarget = false;
        createdText.text = string.Empty;
        return createdText;
    }

    // 이미지와 텍스트가 UI 레이캐스트를 받지 않도록 설정한다
    private void DisableRaycastTargets()
    {
        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
        }

        if (messageText != null)
        {
            messageText.raycastTarget = false;
        }
    }
}
