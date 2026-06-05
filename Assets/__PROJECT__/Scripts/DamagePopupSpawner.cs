using TMPro;
using UnityEngine;

public class DamagePopupSpawner : MonoBehaviour
{
    private const int INITIAL_POOL_SIZE = 32;
    private const string DAMAGE_POPUP_RESOURCE_PATH = "DamagePopup";

    private static DamagePopupSpawner Inst;

    [SerializeField] private DamagePopup damagePopupPrefab;
    [SerializeField] private int fontSize = 24;
    [SerializeField] private float lifetime = 0.75f;
    [SerializeField] private float heightOffset = 2.2f;
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.12f, 1f);

    private Camera targetCamera;

    private void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(gameObject);
            return;
        }

        Inst = this;
        targetCamera = Camera.main;
        EnsurePrefab();
        Prewarm();
    }

    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    /// <summary>
    /// 대상 위치 위에 데미지 숫자 팝업을 표시한다
    /// </summary>
    /// <param name="target"></param>
    /// <param name="damage"></param>
    public static void SpawnDamage(Transform target, float damage)
    {
        if (target == null || damage <= 0f)
        {
            return;
        }

        DamagePopupSpawner spawner = GetOrCreateInstance();
        Vector3 spawnPosition = target.position + (Vector3.up * spawner.heightOffset);
        spawner.Spawn(Mathf.RoundToInt(damage).ToString(), spawnPosition);
    }

    private static DamagePopupSpawner GetOrCreateInstance()
    {
        if (Inst != null)
        {
            return Inst;
        }

        GameObject spawnerObject = new GameObject("DamagePopupSpawner");
        return spawnerObject.AddComponent<DamagePopupSpawner>();
    }

    private static MemoryPool GetOrCreateMemoryPool()
    {
        if (MemoryPool.Inst != null)
        {
            return MemoryPool.Inst;
        }

        GameObject memoryPoolObject = new GameObject("MemoryPool");
        return memoryPoolObject.AddComponent<MemoryPool>();
    }

    private void Prewarm()
    {
        if (damagePopupPrefab == null)
        {
            return;
        }

        GetOrCreateMemoryPool().Prewarm(damagePopupPrefab, INITIAL_POOL_SIZE);
    }

    private void Spawn(string text, Vector3 position)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        EnsurePrefab();

        if (damagePopupPrefab == null)
        {
            return;
        }

        DamagePopup popup = GetOrCreateMemoryPool().GetInstance<DamagePopup>(damagePopupPrefab);
        if (popup == null)
        {
            return;
        }

        popup.Init(text, position, damageColor, fontSize, lifetime, moveOffset, targetCamera);
    }

    private void EnsurePrefab()
    {
        if (damagePopupPrefab != null)
        {
            ApplyTextSettings(damagePopupPrefab.GetComponent<TextMeshPro>());
            return;
        }

        damagePopupPrefab = Resources.Load<DamagePopup>(DAMAGE_POPUP_RESOURCE_PATH);
        if (damagePopupPrefab != null)
        {
            ApplyTextSettings(damagePopupPrefab.GetComponent<TextMeshPro>());
            return;
        }

        damagePopupPrefab = CreateRuntimePrefab();
    }

    private DamagePopup CreateRuntimePrefab()
    {
        GameObject popupObject = new GameObject("DamagePopup");
        popupObject.transform.SetParent(transform);
        popupObject.SetActive(false);

        TextMeshPro textMesh = popupObject.AddComponent<TextMeshPro>();
        ApplyTextSettings(textMesh);

        return popupObject.AddComponent<DamagePopup>();
    }

    private void ApplyTextSettings(TextMeshPro textMesh)
    {
        if (textMesh == null)
        {
            return;
        }

        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = fontSize;
        textMesh.enableAutoSizing = false;
        textMesh.color = damageColor;
        textMesh.text = "0";
    }
}
