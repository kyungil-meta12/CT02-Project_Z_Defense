using UnityEditor;
using UnityEngine;

/// <summary>
/// StatusEffectVisualSlot 인스펙터를 상태이상 타입과 부착 방식에 맞게 정리해서 표시한다.
/// </summary>
[CustomPropertyDrawer(typeof(StatusEffectVisualSlot))]
public sealed class StatusEffectVisualSlotDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2.0f;

    // 상태이상 슬롯의 타입과 부착 방식에 맞는 필드만 그린다
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect currentRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(currentRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        currentRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;

        SerializedProperty visualTypeProperty = property.FindPropertyRelative("visualType");
        SerializedProperty attachModeProperty = property.FindPropertyRelative("attachMode");
        DrawProperty(ref currentRect, visualTypeProperty);
        DrawProperty(ref currentRect, attachModeProperty);
        DrawProperty(ref currentRect, property.FindPropertyRelative("visualPrefab"));

        StatusEffectVisualType visualType = (StatusEffectVisualType)visualTypeProperty.enumValueIndex;
        StatusEffectVisualAttachMode attachMode = (StatusEffectVisualAttachMode)attachModeProperty.enumValueIndex;
        if (attachMode == StatusEffectVisualAttachMode.Anchor)
        {
            DrawAnchorFields(ref currentRect, property);
        }
        else
        {
            DrawRendererOverlayFields(ref currentRect, property);
        }

        DrawProperty(ref currentRect, property.FindPropertyRelative("restartParticlesOnEnable"));
        if (visualType == StatusEffectVisualType.Poison)
        {
            DrawPoisonLethalIndicatorFields(ref currentRect, property);
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    // 상태이상 슬롯의 동적 높이를 계산한다
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        SerializedProperty visualTypeProperty = property.FindPropertyRelative("visualType");
        SerializedProperty attachModeProperty = property.FindPropertyRelative("attachMode");
        StatusEffectVisualType visualType = (StatusEffectVisualType)visualTypeProperty.enumValueIndex;
        StatusEffectVisualAttachMode attachMode = (StatusEffectVisualAttachMode)attachModeProperty.enumValueIndex;

        float height = EditorGUIUtility.singleLineHeight + VerticalSpacing;
        height += GetPropertyLineHeight(property.FindPropertyRelative("visualType"));
        height += GetPropertyLineHeight(property.FindPropertyRelative("attachMode"));
        height += GetPropertyLineHeight(property.FindPropertyRelative("visualPrefab"));

        if (attachMode == StatusEffectVisualAttachMode.Anchor)
        {
            height += GetPropertyLineHeight(property.FindPropertyRelative("anchor"));
            height += GetPropertyLineHeight(property.FindPropertyRelative("localPositionOffset"));
            height += GetPropertyLineHeight(property.FindPropertyRelative("localEulerOffset"));
            height += GetPropertyLineHeight(property.FindPropertyRelative("localScale"));
        }
        else
        {
            height += GetPropertyLineHeight(property.FindPropertyRelative("targetRenderers"));
            height += GetPropertyLineHeight(property.FindPropertyRelative("particleScaleMultiplier"));
        }

        height += GetPropertyLineHeight(property.FindPropertyRelative("restartParticlesOnEnable"));
        if (visualType == StatusEffectVisualType.Poison)
        {
            height += GetPropertyLineHeight(property.FindPropertyRelative("lethalIndicatorChildName"));
            height += GetPropertyLineHeight(property.FindPropertyRelative("lethalIndicatorLocalPositionOffset"));
        }

        return height;
    }

    // 앵커 부착 방식에 필요한 필드를 그린다
    private static void DrawAnchorFields(ref Rect currentRect, SerializedProperty property)
    {
        DrawProperty(ref currentRect, property.FindPropertyRelative("anchor"));
        DrawProperty(ref currentRect, property.FindPropertyRelative("localPositionOffset"));
        DrawProperty(ref currentRect, property.FindPropertyRelative("localEulerOffset"));
        DrawProperty(ref currentRect, property.FindPropertyRelative("localScale"));
    }

    // 렌더러 오버레이 방식에 필요한 필드를 그린다
    private static void DrawRendererOverlayFields(ref Rect currentRect, SerializedProperty property)
    {
        DrawProperty(ref currentRect, property.FindPropertyRelative("targetRenderers"));
        DrawProperty(ref currentRect, property.FindPropertyRelative("particleScaleMultiplier"));
    }

    // Poison 전용 처치 예고 표시 필드를 그린다
    private static void DrawPoisonLethalIndicatorFields(ref Rect currentRect, SerializedProperty property)
    {
        DrawProperty(ref currentRect, property.FindPropertyRelative("lethalIndicatorChildName"));
        DrawProperty(ref currentRect, property.FindPropertyRelative("lethalIndicatorLocalPositionOffset"));
    }

    // 단일 SerializedProperty를 그리고 다음 줄 위치로 이동한다
    private static void DrawProperty(ref Rect currentRect, SerializedProperty property)
    {
        float propertyHeight = EditorGUI.GetPropertyHeight(property, true);
        currentRect.height = propertyHeight;
        EditorGUI.PropertyField(currentRect, property, true);
        currentRect.y += propertyHeight + VerticalSpacing;
        currentRect.height = EditorGUIUtility.singleLineHeight;
    }

    // 단일 SerializedProperty의 높이에 줄 간격을 더해 반환한다
    private static float GetPropertyLineHeight(SerializedProperty property)
    {
        return EditorGUI.GetPropertyHeight(property, true) + VerticalSpacing;
    }
}
