using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제작에 필요한 아이템 정보
/// </summary>
[Serializable]
public struct ItemCreaftData
{
    [Header("필요 아이템 타입")] public RewardCurrencyType Type;
    [Header("필요 개수")] public int Count;
}

/// <summary>
/// 분해 시 나오는 아이템 정보
/// </summary>
[Serializable]
public struct ItemDecomposeData
{
    [Header("나오는 아이템 타입")] public RewardCurrencyType Type;
    [Header("분해 시 나오는 개수 범위")]
    public int min;
    public int max;
}

/// <summary>
/// 아이템 등급
/// </summary>
public enum ItemGrade
{
   S = 0,
   A,
   B,
   C,
   D
}

[CreateAssetMenu(fileName = "ItemMetaData", menuName = "Scriptable Objects/ItemMetaData")]
public class ItemMetaDataSo : ScriptableObject
{
    [Header("아이템 타입")] public RewardCurrencyType Type;
    [Header("아이템 등급")] public ItemGrade Grade;
    [Header("제작 가능 여부")] public bool Createable;
    [Header("한 번 제작할 때 만들어지는 개수")] public int CountPerCraft;
    [Header("제작에 필요한 아이템 목록")] public List<ItemCreaftData> ItemsToCreate;
    [Header("분해 가능 여부")] public bool Decomposable;
    [Header("분해 시 나오는 아이템들")] public List<ItemDecomposeData> ItemsFromDecompose;
    [Header("표시할 아이템 이름 텍스트")] public string Name;
    [Header("표시할 아이템 설명 텍스트")][TextArea(3, 10)] public string InfoText;
    [Header("표시할 아이템 이미지")] public Sprite ItemImage;
}