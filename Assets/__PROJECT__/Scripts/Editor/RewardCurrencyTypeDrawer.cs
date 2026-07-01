using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

// [핵심] 프로젝트 전역에서 RewardCurrencyType을 그릴 때 이 드로어가 강제 개입합니다.
[CustomPropertyDrawer(typeof(RewardCurrencyType))]
public class RewardCurrencyTypeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var allValues = (RewardCurrencyType[])Enum.GetValues(typeof(RewardCurrencyType));

        // 현재 이 칸에 선택되어 있는 값의 가독성 좋은 이름(문자열)을 구합니다.
        string currentName = property.enumDisplayNames[property.enumValueIndex];

        // 인스펙터 좌측 라벨("아이템 타입", "필요 아이템 타입" 등)을 순정 그대로 그려줍니다.
        position = EditorGUI.PrefixLabel(position, label);

        // 순정 드롭다운 화살표 모양 버튼을 인스펙터 칸에 그려줍니다.
        if (GUI.Button(position, currentName, EditorStyles.popup))
        {
            // [구조체/리스트 대응 핵심 키] 현재 인스펙터 칸의 직렬화 객체와 고유 주소 경로를 박제합니다.
            SerializedObject serializedObject = property.serializedObject;
            string propertyPath = property.propertyPath;

            // 검색 창 팝업 호출
            SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)),
                new RewardCurrencyTypeSearchProvider(allValues, (selectedValue) =>
                {
                    // 사용자가 고른 값을 박제해둔 고유 주소(propertyPath)를 찾아 정확히 꽂아 넣습니다.
                    serializedObject.Update();
                    SerializedProperty targetProp = serializedObject.FindProperty(propertyPath);
                    if (targetProp != null)
                    {
                        // 수동으로 번호를 지정했더라도 enumValueIndex를 실제 매핑된 인덱스로 역추적해 할당합니다.
                        int index = Array.IndexOf(allValues, selectedValue);
                        targetProp.enumValueIndex = index;

                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(serializedObject.targetObject);
                    }
                }));
        }
    }
}

// 유니티 공식 검색 창(Search Window) 프로바이더 구현
public class RewardCurrencyTypeSearchProvider : ScriptableObject, ISearchWindowProvider
{
    private RewardCurrencyType[] _values;
    private Action<RewardCurrencyType> _onSelected;

    public RewardCurrencyTypeSearchProvider(RewardCurrencyType[] values, Action<RewardCurrencyType> onSelected)
    {
        _values = values;
        _onSelected = onSelected;
    }

    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        var tree = new List<SearchTreeEntry>
        {
            new SearchTreeGroupEntry(new GUIContent("아이템 타입 검색"), 0)
        };

        foreach (var val in _values)
        {
            tree.Add(new SearchTreeEntry(new GUIContent(val.ToString()))
            {
                level = 1,
                userData = val
            });
        }
        return tree;
    }

    public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
    {
        _onSelected?.Invoke((RewardCurrencyType)searchTreeEntry.userData);
        return true;
    }
}