using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 메타데이터 에셋 목록을 저장한다.
/// </summary>
[CreateAssetMenu(fileName = "ItemMetaDataList", menuName = "Scriptable Objects/ItemMetaDataList")]
public class ItemMetaDataListSo : ScriptableObject
{
    public List<ItemMetaDataSo> MetaDataList;
}
