using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

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
        string currentName = property.enumDisplayNames[property.enumValueIndex];

        position = EditorGUI.PrefixLabel(position, label);

        if (GUI.Button(position, currentName, EditorStyles.popup))
        {
            SerializedObject serializedObject = property.serializedObject;
            string propertyPath = property.propertyPath;

            // [수정 포인트 1] new 대신 CreateInstance를 사용하여 유니티 정석대로 객체를 생성합니다.
            var searchProvider = ScriptableObject.CreateInstance<RewardCurrencyTypeSearchProvider>();

            // 데이터를 안전하게 넘겨줍니다.
            searchProvider.Initialize(allValues, (selectedValue) =>
            {
                serializedObject.Update();
                SerializedProperty targetProp = serializedObject.FindProperty(propertyPath);
                if (targetProp != null)
                {
                    int index = Array.IndexOf(allValues, selectedValue);
                    targetProp.enumValueIndex = index;

                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(serializedObject.targetObject);
                }
            });

            // 검색 창 팝업 호출
            SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), searchProvider);
        }
    }
}

public class RewardCurrencyTypeSearchProvider : ScriptableObject, ISearchWindowProvider
{
    private RewardCurrencyType[] _values;
    private Action<RewardCurrencyType> _onSelected;

    // [수정 포인트 2] CreateInstance 제약 때문에 기존 생성자(Constructor)를 지우고, 
    // 이를 대체할 Initialize 메서드를 만듭니다.
    public void Initialize(RewardCurrencyType[] values, Action<RewardCurrencyType> onSelected)
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