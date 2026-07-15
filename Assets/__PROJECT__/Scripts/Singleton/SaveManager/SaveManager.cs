using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 재화, 웨이브, 터렛 등 여러 시스템의 저장/불러오기를 한 파일에서 통합 관리한다.<para/>
/// 각 시스템은 ISaveable을 구현해 Register만 하면 되고, 저장 시점 최적화(더티 체크 + 주기 저장)와
/// 파일 IO는 이 매니저가 전담한다.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class SaveManager : MonoBehaviour
{
    public static SaveManager Inst;

    [Header("자동 저장")]
    [Tooltip("변경된 뒤 이 주기(초)마다, 변경 사항이 있을 때만 저장 파일을 갱신한다.")]
    [SerializeField, Min(1.0f)] private float autoSaveIntervalSeconds = 30.0f;

    [Header("디버그")]
    [Tooltip("체크한 상태로 실행하면 기존 저장 파일을 삭제하고 초기화한다. 초기화 후 자동으로 체크가 해제된다.")]
    [SerializeField] private bool resetSaveDataOnLoad = false;

    private const string SAVE_FILE_NAME = "game_save.json";
    private string SaveFilePath => Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

    // 등록된 시스템 목록 (SaveKey 기준)
    private readonly Dictionary<string, ISaveable> saveables = new(8);
    // 디스크에서 읽어온 구간별 원본 JSON 캐시. 아직 등록되지 않은 시스템의 데이터도 여기 보관했다가 등록 시점에 넘겨준다.
    private readonly Dictionary<string, string> loadedSections = new(8);

    private bool isFileLoaded;
    private bool isDirty;

    // 인스펙터에 연결할 씬 참조가 없는 순수 유틸리티 매니저이므로, 씬에 수동 배치하지 않아도
    // 첫 씬 로드 전에 자동으로 생성한다. 다른 시스템의 Awake보다 먼저 실행되어 Inst가 항상 준비돼 있다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Inst != null)
        {
            return;
        }

        GameObject managerObject = new GameObject(nameof(SaveManager));
        managerObject.AddComponent<SaveManager>();
    }

    // 싱글톤을 초기화하고 씬 인스턴스의 저장 초기화 요청을 가장 먼저 처리한다
    private void Awake()
    {
        if (Inst && Inst != this)
        {
            if (resetSaveDataOnLoad)
            {
                Inst.ResetSaveData();
                resetSaveDataOnLoad = false;
            }

            Destroy(gameObject);
            return;
        }

        Inst = this;
        DontDestroyOnLoad(gameObject);

        if (resetSaveDataOnLoad)
        {
            ResetSaveData();
            resetSaveDataOnLoad = false;
        }
    }

    // 자동 저장 코루틴을 시작한다
    private void Start()
    {
        StartCoroutine(AutoSaveLoop());
    }

    // 현재 싱글톤 인스턴스가 제거될 때 정적 참조를 정리한다
    private void OnDestroy()
    {
        if (Inst == this)
        {
            Inst = null;
        }
    }

    // 모바일에서 홈 버튼 등으로 앱이 백그라운드로 전환될 때 변경 사항을 저장한다
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && isDirty)
        {
            SaveAll();
        }
    }

    // 앱 종료(에디터 플레이 모드 정지 포함) 시 변경 사항을 저장한다
    private void OnApplicationQuit()
    {
        if (isDirty)
        {
            SaveAll();
        }
    }

    /// <summary>
    /// 저장 대상 시스템을 등록한다. 저장 파일에 이 시스템의 데이터가 있으면 즉시 복원한다.<para/>
    /// Awake가 아닌 Start에서 호출해야 한다(SaveManager.Inst가 이미 준비된 시점을 보장하기 위함).
    /// </summary>
    public void Register(ISaveable saveable)
    {
        if (saveable == null || string.IsNullOrEmpty(saveable.SaveKey))
        {
            Debug.LogWarning("[SaveManager] SaveKey가 없는 ISaveable은 등록할 수 없습니다.");
            return;
        }

        if (saveables.ContainsKey(saveable.SaveKey))
        {
            Debug.LogWarning($"[SaveManager] 이미 등록된 SaveKey입니다: {saveable.SaveKey}");
            return;
        }

        saveables.Add(saveable.SaveKey, saveable);

        EnsureFileLoaded();
        if (loadedSections.TryGetValue(saveable.SaveKey, out string sectionJson))
        {
            // 한 시스템의 손상된 저장 데이터가 이 호출을 트리거한 다른 시스템의 Start()까지 끊지 않도록 격리한다
            try
            {
                saveable.RestoreSaveData(sectionJson);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveManager] '{saveable.SaveKey}' 데이터 복원에 실패했습니다: {exception.Message}");
            }
        }
    }

    /// <summary>
    /// 등록된 시스템을 해제한다. 씬 전환 등으로 오브젝트가 파괴될 때 반드시 호출해야 한다.
    /// </summary>
    public void Unregister(ISaveable saveable)
    {
        if (saveable == null || string.IsNullOrEmpty(saveable.SaveKey))
        {
            return;
        }

        saveables.Remove(saveable.SaveKey);
    }

    /// <summary>
    /// 저장이 필요한 변경이 있음을 표시한다.<para/>
    /// 실제 파일 쓰기는 자동 저장 주기 또는 앱 일시정지/종료 시점에만 일어난다.
    /// </summary>
    public void MarkDirty()
    {
        isDirty = true;
    }

    /// <summary>
    /// 등록된 모든 시스템의 데이터를 모아 즉시 파일에 저장한다.
    /// </summary>
    public void SaveAll()
    {
        SaveFileData fileData = new();
        foreach (KeyValuePair<string, ISaveable> pair in saveables)
        {
            string json;
            try
            {
                json = pair.Value.CaptureSaveData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveManager] '{pair.Key}' 데이터 캡처에 실패해 이번 저장에서 제외합니다: {exception.Message}");
                continue;
            }

            if (string.IsNullOrEmpty(json))
            {
                continue;
            }

            fileData.Sections.Add(new SaveSection { Key = pair.Key, Json = json });
        }

        try
        {
            string fileJson = JsonUtility.ToJson(fileData);
            File.WriteAllText(SaveFilePath, fileJson);
            isDirty = false;
        }
        catch (IOException exception)
        {
            Debug.LogWarning($"[SaveManager] 저장에 실패했습니다: {exception.Message}");
        }
    }

    // 저장 파일과 캐시된 데이터를 모두 삭제해 초기 상태로 되돌린다
    private void ResetSaveData()
    {
        bool resetSucceeded = true;
        if (File.Exists(SaveFilePath))
        {
            try
            {
                File.Delete(SaveFilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveManager] 저장 파일 삭제에 실패했습니다: {exception.Message}");
                resetSucceeded = false;
            }
        }

        loadedSections.Clear();
        isFileLoaded = true;
        isDirty = false;

        if (resetSucceeded)
        {
            Debug.Log($"[SaveManager] 저장 데이터를 초기화했습니다: {SaveFilePath}", this);
        }
    }

    // 저장 파일을 한 번만 읽어 구간별 JSON을 캐시에 올려둔다
    private void EnsureFileLoaded()
    {
        if (isFileLoaded)
        {
            return;
        }

        isFileLoaded = true;
        if (!File.Exists(SaveFilePath))
        {
            return;
        }

        try
        {
            string fileJson = File.ReadAllText(SaveFilePath);
            SaveFileData fileData = JsonUtility.FromJson<SaveFileData>(fileJson);
            if (fileData?.Sections == null)
            {
                return;
            }

            foreach (SaveSection section in fileData.Sections)
            {
                loadedSections[section.Key] = section.Json;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SaveManager] 저장 파일을 불러오지 못했습니다: {exception.Message}");
        }
    }

    /// <summary>
    /// 변경된 데이터가 있을 때만 일정 주기로 저장한다.<para/>
    /// Update()로 매 프레임 검사하지 않고 코루틴 대기로 처리해 평상시 비용이 들지 않는다.
    /// </summary>
    private IEnumerator AutoSaveLoop()
    {
        WaitForSeconds wait = new(autoSaveIntervalSeconds);
        while (true)
        {
            yield return wait;
            if (isDirty)
            {
                SaveAll();
            }
        }
    }

    [Serializable]
    private class SaveSection
    {
        public string Key;
        public string Json;
    }

    [Serializable]
    private class SaveFileData
    {
        public List<SaveSection> Sections = new();
    }
}
