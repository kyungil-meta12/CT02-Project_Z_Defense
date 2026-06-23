using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제작에 필요한 아이템 정보
/// </summary>
[Serializable]
public struct ItemMaterialData
{
    [Header("필요 개수")] public int Count;
    [Header("아이템 타입")] public RewardCurrencyType Type;
}

[CreateAssetMenu(fileName = "ItemMetaData", menuName = "Scriptable Objects/ItemMetaData")]
public class ItemMetaDataSo : ScriptableObject
{
    [Header("아이템 타입")] public RewardCurrencyType Type;
    [Header("표시할 아이템 이름 텍스트")] public string Name;
    [Header("표시할 아이템 설명 텍스트")][TextArea(3, 10)] public string InfoText;
    [Header("표시할 아이템 이미지")] public Sprite ItemImage;
    // 비워두면 조합법 자제가 없는 기초 재료이다.
    [Header("제작에 필요한 아이템 목록")] public List<ItemMaterialData> ItemsToCreate;
}

