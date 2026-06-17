using System;
using UnityEngine;

/// <summary>
/// 아이템 추가 시 지정될 이름(인스펙터에서 설정)
/// </summary>
[Serializable]
public class ItemMetaData
{
    [Header("아이템 타입")] public RewardCurrencyType Type;
    [Header("표시할 아이템 이름 텍스트")] public string Name;
    [Header("표시할 아이템 설명 텍스트")][TextArea(3, 10)] public string InfoText;
    [Header("표시할 아이템 이미지")] public Sprite ItemImage;
}

/// <summary>
/// 아이템 메타 데이터를 서술하는 스크립터블 오브젝트
/// </summary>
[CreateAssetMenu(fileName = "ItemMetaData", menuName = "Scriptable Objects/ItemMetaData")]
public class ItemMetaDataSo : ScriptableObject
{
    public ItemMetaData[] MetaDataList;
}
