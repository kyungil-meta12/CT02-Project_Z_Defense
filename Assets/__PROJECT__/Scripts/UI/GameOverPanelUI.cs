using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 게임오버 전환 패널의 표시 상태와 투명도 페이드를 관리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class GameOverPanelUI : MonoBehaviour
{
    [Header("게임오버 패널")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;

    private string defaultTitleText;
    private string defaultStatusText;

    // 필요한 UI 참조를 자동으로 연결한다
    private void Reset()
    {
        AutoBindReferences();
    }

    // 시작 전에 필요한 UI 참조를 준비하고 패널을 숨긴다
    private void Awake()
    {
        AutoBindReferences();
        CacheDefaultMessages();
        SetAlpha(0.0f);
        SetVisible(false);
    }

    // 제목/상태 문구를 지정한다. null이나 빈 문자열을 넘긴 항목은 변경하지 않는다
    public void SetMessage(string title, string status = null)
    {
        if (!string.IsNullOrEmpty(title) && titleText != null)
        {
            titleText.text = title;
        }

        if (!string.IsNullOrEmpty(status) && statusText != null)
        {
            statusText.text = status;
        }
    }

    // 제목/상태 문구를 기본 게임오버 문구로 되돌린다
    public void ResetMessageToDefault()
    {
        if (titleText != null)
        {
            titleText.text = defaultTitleText;
        }

        if (statusText != null)
        {
            statusText.text = defaultStatusText;
        }
    }

    // 시작 시점의 기본 문구를 캐싱해 이후 복원에 사용한다
    private void CacheDefaultMessages()
    {
        defaultTitleText = titleText != null ? titleText.text : string.Empty;
        defaultStatusText = statusText != null ? statusText.text : string.Empty;
    }

    // 지정 시간 동안 패널을 불투명하게 만든다
    public IEnumerator FadeIn(float duration)
    {
        SetVisible(true);
        yield return Fade(0.0f, 1.0f, duration);
    }

    // 지정 시간 동안 패널을 투명하게 만든 뒤 숨긴다
    public IEnumerator FadeOut(float duration)
    {
        SetVisible(true);
        yield return Fade(1.0f, 0.0f, duration);
        SetVisible(false);
    }

    // 패널 노출 상태를 설정한다
    public void SetVisible(bool visible)
    {
        GameObject target = panelRoot != null ? panelRoot : gameObject;
        target.SetActive(visible);

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
    }

    // 패널 투명도를 즉시 설정한다
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    // 시작 투명도에서 목표 투명도까지 보간한다
    private IEnumerator Fade(float fromAlpha, float toAlpha, float duration)
    {
        float safeDuration = Mathf.Max(0.0f, duration);
        if (safeDuration <= 0.0f)
        {
            SetAlpha(toAlpha);
            yield break;
        }

        float timer = 0.0f;
        while (timer < safeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / safeDuration);
            SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, progress));
            yield return null;
        }

        SetAlpha(toAlpha);
    }

    // 비어 있는 참조를 현재 오브젝트 기준으로 자동 연결한다
    private void AutoBindReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (titleText == null)
        {
            titleText = FindChildText("Title");
        }

        if (statusText == null)
        {
            statusText = FindChildText("Status");
        }
    }

    // panelRoot 하위에서 지정한 이름의 텍스트 컴포넌트를 찾는다
    private TMP_Text FindChildText(string childName)
    {
        Transform root = panelRoot != null ? panelRoot.transform : transform;
        Transform child = root.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
