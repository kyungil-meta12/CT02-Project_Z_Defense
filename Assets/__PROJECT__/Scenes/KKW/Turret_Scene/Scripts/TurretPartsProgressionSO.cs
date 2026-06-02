using UnityEngine;

[CreateAssetMenu(menuName = "Project Z Defense/Turret Parts Progression")]
public class TurretPartsProgressionSO : ScriptableObject
{
    public TurretPartProgressionEntry[] levelEntries;
}

[System.Serializable]
public class TurretPartProgressionEntry
{
    public int requiredLevel = 1;
    public string socketName;
    public GameObject partPrefab;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale = Vector3.one;
}
