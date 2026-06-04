using NUnit.Framework.Internal;
using UnityEngine;

[CreateAssetMenu(fileName = "ZombieSpawnData", menuName = "Scriptable Objects/ZombieSpawnData")]
public class ZombieSpawnData : ScriptableObject
{
    [Header("기본 스폰 간격(웨이브1 기준)")] public float DefaultSpawnInterval;
    [Header("스폰 간격 가중치")] public float SpawnIntervalWeight;
    [Header("기본 일반 좀비 스폰 횟수(웨이브1 기준)")] public int DefaultSpawnCount;
    [Header("일반 좀비 스폰 횟수 가중치(int)")] public int SpawnCountWeight;
}
