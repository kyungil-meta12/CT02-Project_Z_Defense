using UnityEngine;

public class MemoryPoolPrewarmer : MonoBehaviour
{
    [System.Serializable]
    public class PrewarmEntry
    {
        public GameObject prefab;
        [Min(0)] public int count;
    }

    [SerializeField] private PrewarmEntry[] prewarmEntries;

    private void Start()
    {
        Prewarm();
    }

    public void Prewarm()
    {
        if (MemoryPool.Inst == null || prewarmEntries == null)
        {
            return;
        }

        for (int i = 0; i < prewarmEntries.Length; i++)
        {
            PrewarmEntry entry = prewarmEntries[i];
            if (entry == null || entry.prefab == null || entry.count <= 0)
            {
                continue;
            }

            MemoryPool.Inst.Prewarm(entry.prefab, entry.count);
        }
    }
}
