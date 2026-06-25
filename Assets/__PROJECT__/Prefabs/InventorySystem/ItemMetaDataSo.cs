using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 제작에 필요한 아이템 정보
/// </summary>
[Serializable]
public struct ItemMaterialData
{
    [Header("아이템 타입")] public RewardCurrencyType Type;
    [Header("필요 개수")] public int Count;
}

[CreateAssetMenu(fileName = "ItemMetaData", menuName = "Scriptable Objects/ItemMetaData")]
public class ItemMetaDataSo : ScriptableObject
{
    [Header("아이템 타입")] public RewardCurrencyType Type;
    [Header("표시할 아이템 이름 텍스트")] public string Name;
    [Header("표시할 아이템 설명 텍스트")][TextArea(3, 10)] public string InfoText;
    [Header("표시할 아이템 이미지")] public Sprite ItemImage;
    [Header("제작 가능 여부")] public bool Craftable;
    [Header("제작에 필요한 아이템 목록")] public List<ItemMaterialData> ItemsToCreate;
    [Header("한 번 제작할 때 만들어지는 개수")] public int CreateCount;
}

