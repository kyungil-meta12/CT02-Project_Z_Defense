using UnityEditor;
using UnityEngine;

// InventorySystem 런타임 디버그 조작 버튼을 인스펙터에 표시한다
[CustomEditor(typeof(InventorySystem))]
internal sealed class InventorySystemEditor : Editor
{
    // 기본 인스펙터 아래에 플레이 중 디버그 코인 지급 버튼을 표시한다
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        DrawDebugCoinButton();
    }

    // 플레이 중에만 코인 100만 지급 버튼을 활성화한다
    private void DrawDebugCoinButton()
    {
        InventorySystem inventorySystem = target as InventorySystem;
        using (new EditorGUI.DisabledScope(!Application.isPlaying || inventorySystem == null))
        {
            if (GUILayout.Button("디버그: 코인 100만 지급"))
            {
                inventorySystem.AddDebugMillionCoins();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 중에만 코인 지급 버튼을 사용할 수 있습니다.", MessageType.Info);
        }
    }
}
