using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치된 터렛별 실제 피해량을 웨이브 단위로 집계하고 UI 갱신 데이터를 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TurretDamageMeterManager : MonoBehaviour
{
    private const int DEFAULT_ROW_CAPACITY = 8;

    private static TurretDamageMeterManager instance;
    private static readonly List<TurretDamageMeterSource> PendingSources = new List<TurretDamageMeterSource>(DEFAULT_ROW_CAPACITY);

    [Header("갱신 주기")]
    [SerializeField, Min(0.05f)] private float rankingRefreshInterval = 0.25f;
    [SerializeField, Min(0.05f)] private float uiRefreshInterval = 0.15f;

    [Header("표시 정책")]
    [SerializeField, Min(1)] private int visibleRowLimit = DEFAULT_ROW_CAPACITY;
    [SerializeField] private bool keepPreviousWaveUntilFirstDamage = true;

    [Header("UI 참조")]
    [SerializeField] private TurretDamageMeterUI meterUI;

    private readonly List<TurretDamageMeterEntry> entries = new List<TurretDamageMeterEntry>(DEFAULT_ROW_CAPACITY);
    private readonly List<TurretDamageMeterEntry> sortedEntries = new List<TurretDamageMeterEntry>(DEFAULT_ROW_CAPACITY);
    private float rankingTimer;
    private float uiTimer;
    private int activeWave = -1;
    private bool hasCurrentWaveDamage;
    private bool rankingDirty = true;
    private bool uiDirty = true;

    public static TurretDamageMeterManager Instance
    {
        get
        {
            return instance;
        }
    }

    public int VisibleRowLimit
    {
        get
        {
            return Mathf.Max(1, visibleRowLimit);
        }
    }

    // 씬에 배치된 딜 미터기 매니저를 정적 접근 대상으로 등록한다
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("딜 미터기 매니저가 중복 배치되어 비활성화합니다.", this);
            enabled = false;
            return;
        }

        instance = this;
        ValidateRequiredReferences();
        FlushPendingSources();
    }

    // 에디터에서 컴포넌트를 추가할 때 같은 오브젝트의 UI 참조를 연결한다
    private void Reset()
    {
        meterUI = GetComponent<TurretDamageMeterUI>();
    }

    // 활성화될 때 현재 웨이브 번호를 기록한다
    private void OnEnable()
    {
        activeWave = ResolveCurrentWave();
        FlushPendingSources();
    }

    // 제거될 때 정적 참조를 정리한다
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // 정렬과 UI 갱신을 각각 지정된 주기로 처리한다
    private void Update()
    {
        rankingTimer -= Time.deltaTime;
        if (rankingTimer <= 0.0f)
        {
            rankingTimer += rankingRefreshInterval;
            RefreshRankingIfNeeded();
        }

        uiTimer -= Time.deltaTime;
        if (uiTimer <= 0.0f)
        {
            uiTimer += uiRefreshInterval;
            RefreshUIIfNeeded();
        }
    }

    // 터렛 데미지 출처를 전역 딜 미터기에 등록한다
    public static void RegisterSource(TurretDamageMeterSource source)
    {
        if (source == null)
        {
            return;
        }

        if (instance == null)
        {
            if (!PendingSources.Contains(source))
            {
                PendingSources.Add(source);
            }

            return;
        }

        instance.RegisterSourceInternal(source);
    }

    // 터렛 데미지 출처를 전역 딜 미터기에서 제거한다
    public static void UnregisterSource(TurretDamageMeterSource source)
    {
        if (source == null)
        {
            return;
        }

        PendingSources.Remove(source);
        if (instance != null)
        {
            instance.UnregisterSourceInternal(source);
        }
    }

    // 실제 깎인 데미지를 터렛 출처에 누적한다
    public static void ReportDamage(TurretDamageMeterSource source, float actualDamage)
    {
        if (source == null || actualDamage <= 0.0f || instance == null)
        {
            return;
        }

        instance.ReportDamageInternal(source, actualDamage);
    }

    // 현재 정렬된 딜 미터기 항목 목록을 반환한다
    public IReadOnlyList<TurretDamageMeterEntry> GetSortedEntries()
    {
        return sortedEntries;
    }

    // 런타임에 필요한 직접 연결 참조가 있는지 검증한다
    private void ValidateRequiredReferences()
    {
        if (meterUI == null)
        {
            Debug.LogWarning("[딜 미터 매니저] Meter UI 참조가 비어 있어 딜 미터기 화면을 갱신할 수 없습니다.", this);
        }
    }

    // 매니저 생성 전에 등록된 터렛 출처들을 실제 항목으로 옮긴다
    private void FlushPendingSources()
    {
        for (int i = 0; i < PendingSources.Count; i++)
        {
            RegisterSourceInternal(PendingSources[i]);
        }

        PendingSources.Clear();
    }

    // 새 터렛 출처를 항목 목록에 추가하거나 기존 항목을 갱신한다
    private void RegisterSourceInternal(TurretDamageMeterSource source)
    {
        if (source == null || FindEntry(source) != null)
        {
            return;
        }

        TurretDamageMeterEntry entry = new TurretDamageMeterEntry(source);
        entries.Add(entry);
        sortedEntries.Add(entry);
        rankingDirty = true;
        uiDirty = true;
    }

    // 제거되거나 진화로 사라진 터렛 출처를 항목 목록에서 제거한다
    private void UnregisterSourceInternal(TurretDamageMeterSource source)
    {
        TurretDamageMeterEntry entry = FindEntry(source);
        if (entry == null)
        {
            return;
        }

        entries.Remove(entry);
        sortedEntries.Remove(entry);
        rankingDirty = true;
        uiDirty = true;
    }

    // 피해가 들어온 웨이브를 확인하고 실제 데미지를 누적한다
    private void ReportDamageInternal(TurretDamageMeterSource source, float actualDamage)
    {
        int currentWave = ResolveCurrentWave();
        if (ShouldStartNewWaveBoard(currentWave))
        {
            ResetCurrentWaveBoard(currentWave);
        }

        TurretDamageMeterEntry entry = FindEntry(source);
        if (entry == null)
        {
            RegisterSourceInternal(source);
            entry = FindEntry(source);
        }

        if (entry == null)
        {
            return;
        }

        hasCurrentWaveDamage = true;
        entry.AddDamage(actualDamage);
        rankingDirty = true;
        uiDirty = true;
    }

    // 현재 웨이브 첫 데미지인지 확인한다
    private bool ShouldStartNewWaveBoard(int currentWave)
    {
        if (!keepPreviousWaveUntilFirstDamage)
        {
            return currentWave != activeWave;
        }

        return currentWave != activeWave || !hasCurrentWaveDamage && activeWave < 0;
    }

    // 현재 설치된 터렛을 유지한 채 새 웨이브 집계를 0부터 시작한다
    private void ResetCurrentWaveBoard(int currentWave)
    {
        activeWave = currentWave;
        hasCurrentWaveDamage = false;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            TurretDamageMeterEntry entry = entries[i];
            if (entry == null || entry.Source == null || !entry.Source.isActiveAndEnabled)
            {
                entries.RemoveAt(i);
                sortedEntries.Remove(entry);
                continue;
            }

            entry.ResetDamage();
        }

        rankingDirty = true;
        uiDirty = true;
    }

    // 필요 시 데미지 내림차순으로 항목을 재정렬한다
    private void RefreshRankingIfNeeded()
    {
        if (!rankingDirty)
        {
            return;
        }

        sortedEntries.Sort(CompareEntries);
        rankingDirty = false;
        uiDirty = true;
    }

    // 필요 시 연결된 UI에 최신 표시 데이터를 전달한다
    private void RefreshUIIfNeeded()
    {
        if (!uiDirty)
        {
            return;
        }

        if (meterUI != null)
        {
            meterUI.Refresh(this);
        }

        uiDirty = false;
    }

    // 특정 터렛 출처에 대응하는 항목을 찾는다
    private TurretDamageMeterEntry FindEntry(TurretDamageMeterSource source)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            TurretDamageMeterEntry entry = entries[i];
            if (entry != null && entry.Source == source)
            {
                return entry;
            }
        }

        return null;
    }

    // 현재 게임 매니저의 웨이브 번호를 반환한다
    private static int ResolveCurrentWave()
    {
        return GameManager.Inst == null ? 1 : GameManager.Inst.Wave;
    }

    // 데미지 내림차순과 이름 기준으로 표시 순서를 정한다
    private static int CompareEntries(TurretDamageMeterEntry a, TurretDamageMeterEntry b)
    {
        if (a == null && b == null)
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        int damageCompare = b.TotalDamage.CompareTo(a.TotalDamage);
        if (damageCompare != 0)
        {
            return damageCompare;
        }

        return string.CompareOrdinal(a.DisplayName, b.DisplayName);
    }
}

/// <summary>
/// 딜 미터기에서 표시할 터렛 하나의 누적 피해량과 표시 메타데이터를 보관한다.
/// </summary>
public sealed class TurretDamageMeterEntry
{
    public TurretDamageMeterSource Source { get; }
    public float TotalDamage { get; private set; }

    public string DisplayName
    {
        get
        {
            return Source == null ? string.Empty : Source.DisplayName;
        }
    }

    public Sprite Icon
    {
        get
        {
            return Source == null ? null : Source.Icon;
        }
    }

    public TurretDefinitionSO TurretDefinition
    {
        get
        {
            return Source == null ? null : Source.CurrentDefinition;
        }
    }

    // 터렛 출처를 받아 딜 미터기 항목을 생성한다
    public TurretDamageMeterEntry(TurretDamageMeterSource source)
    {
        Source = source;
    }

    // 실제 적용된 데미지를 누적한다
    public void AddDamage(float damage)
    {
        TotalDamage += Mathf.Max(0.0f, damage);
    }

    // 웨이브 변경 시 누적 데미지를 초기화한다
    public void ResetDamage()
    {
        TotalDamage = 0.0f;
    }
}
