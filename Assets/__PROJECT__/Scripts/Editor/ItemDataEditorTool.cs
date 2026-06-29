#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 아이템 메타데이터 CSV와 ScriptableObject 간 임포트/익스포트 및 재화 enum 갱신을 관리한다.
/// </summary>
public class ItemDataEditorTool : EditorWindow
{
    private const string TYPE_COLUMN = "Type";
    private const string GRADE_COLUMN = "Grade";
    private const string NAME_COLUMN = "Name";
    private const string INFO_TEXT_COLUMN = "InfoText";
    private const string IMAGE_PATH_COLUMN = "ItemImageAssetPath";
    private const string CREATEABLE_COLUMN = "Createable";
    private const string COUNT_PER_CRAFT_COLUMN = "CountPerCraft";
    private const string CRAFT_COLUMN = "ItemsToCreate";
    private const string DECOMPOSABLE_COLUMN = "Decomposable";
    private const string DECOMPOSE_COLUMN = "ItemsFromDecompose";
    private const string DEFAULT_CSV_PATH = "Assets/__PROJECT__/Prefabs/InventorySystem/ItemData.csv";
    private const string DEFAULT_SO_SAVE_PATH = "Assets/__PROJECT__/Prefabs/InventorySystem/Items";
    private const string DEFAULT_LIST_SO_PATH = "Assets/__PROJECT__/Prefabs/InventorySystem/ItemMetaDataList.asset";

    private ItemMetaDataListSo mainListSo;
    private bool treatMissingSpriteAsError;
    private Vector2 scrollPosition;
    private readonly List<string> lastMessages = new List<string>(32);
    private readonly List<string> missingEnumNames = new List<string>(16);

    [MenuItem("Tools/아이템 데이터 관리 도구")]
    // 아이템 데이터 관리 창을 연다
    public static void ShowWindow()
    {
        GetWindow<ItemDataEditorTool>("아이템 데이터 관리");
    }

    // 에디터 창이 열릴 때 고정 경로의 메인 리스트 SO를 로드한다
    private void OnEnable()
    {
        LoadFixedMainListSo();
    }

    // 에디터 창 UI를 그린다
    private void OnGUI()
    {
        EditorGUILayout.LabelField("아이템 CSV ↔ ScriptableObject 관리 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("CSV 파일 경로", DEFAULT_CSV_PATH);
        EditorGUILayout.LabelField("SO 저장 폴더", DEFAULT_SO_SAVE_PATH);
        EditorGUILayout.LabelField("메인 리스트 경로", DEFAULT_LIST_SO_PATH);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("메인 리스트 SO", mainListSo, typeof(ItemMetaDataListSo), false);
        }

        treatMissingSpriteAsError = EditorGUILayout.Toggle("이미지 누락을 오류 처리", treatMissingSpriteAsError);

        EditorGUILayout.HelpBox("CSV 컬럼은 컬럼명<타입>(한글 설명) 형태로 출력됩니다. 제작 재료는 Type:Count;Type:Count, 분해 결과는 Type:Min~Max;Type:Min~Max 형식입니다. 임포트 파서가 없는 타입은 익스포트 전에 중단됩니다.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("CSV로 익스포트", GUILayout.Height(34)))
        {
            ExecuteSafely(ExportToCsv);
        }

        if (GUILayout.Button("CSV에서 임포트", GUILayout.Height(34)))
        {
            ExecuteSafely(ImportFromCsv);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("검증만 실행", GUILayout.Height(34)))
        {
            ExecuteSafely(ValidateOnly);
        }

        if (GUILayout.Button("CSV 헤더 동기화", GUILayout.Height(34)))
        {
            ExecuteSafely(SyncCsvHeader);
        }

        if (GUILayout.Button("CSV 열기", GUILayout.Height(34)))
        {
            ExecuteSafely(OpenItemCsv);
        }

        using (new EditorGUI.DisabledScope(missingEnumNames.Count == 0))
        {
            if (GUILayout.Button("누락 enum 재생성", GUILayout.Height(34)))
            {
                ExecuteSafely(RegenerateRewardCurrencyEnumFromMissingNames);
            }
        }
        EditorGUILayout.EndHorizontal();

        DrawMissingEnums();
        DrawMessages();
    }

    // 누락된 enum 목록을 UI에 표시한다
    private void DrawMissingEnums()
    {
        if (missingEnumNames.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("CSV에 RewardCurrencyType에 없는 타입이 있습니다. 아래 목록을 확인한 뒤 enum 재생성을 실행하고 컴파일 후 다시 임포트하세요.", MessageType.Warning);
        for (int i = 0; i < missingEnumNames.Count; i++)
        {
            EditorGUILayout.LabelField("- " + missingEnumNames[i]);
        }
    }

    // 최근 실행 결과 메시지를 UI에 표시한다
    private void DrawMessages()
    {
        if (lastMessages.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("실행 결과", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(120));
        for (int i = 0; i < lastMessages.Count; i++)
        {
            EditorGUILayout.LabelField(lastMessages[i], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    // CSV 검증만 실행한다
    private void ValidateOnly()
    {
        ClearRunState();
        if (!TryReadAndValidateCsv(out List<ItemCsvRow> rows))
        {
            FlushMessagesToConsole(false);
            return;
        }

        AddMessage($"검증 완료: {rows.Count}개 행이 유효합니다.");
        FlushMessagesToConsole(true);
    }

    // ItemMetaDataSo 필드 기준으로 CSV 헤더를 최신 상태로 맞춘다
    private void SyncCsvHeader()
    {
        ClearRunState();
        CsvColumnDefinition[] columnDefinitions = BuildCsvColumnDefinitions();
        if (!ValidateSupportedColumnDefinitions(columnDefinitions))
        {
            FlushMessagesToConsole(false);
            return;
        }

        if (!File.Exists(DEFAULT_CSV_PATH))
        {
            StringBuilder builder = new StringBuilder(256);
            AppendHeader(builder, columnDefinitions);
            if (!WriteCsvText(builder.ToString()))
            {
                FlushMessagesToConsole(false);
                return;
            }

            AddMessage("CSV 파일이 없어 헤더만 포함한 파일을 생성했습니다: " + DEFAULT_CSV_PATH);
            FlushMessagesToConsole(true);
            return;
        }

        if (!TryReadCsvText(out string csvText))
        {
            FlushMessagesToConsole(false);
            return;
        }

        List<List<string>> table = ParseCsv(csvText);
        if (table.Count == 0)
        {
            table.Add(new List<string>());
        }

        int addedCount = AddMissingHeaderColumns(table, columnDefinitions);
        if (addedCount <= 0)
        {
            AddMessage("CSV 헤더가 이미 최신 상태입니다.");
            FlushMessagesToConsole(true);
            return;
        }

        if (!WriteCsvText(BuildCsvText(table)))
        {
            FlushMessagesToConsole(false);
            return;
        }

        AssetDatabase.Refresh();
        AddMessage($"CSV 헤더 동기화 완료: 누락 컬럼 {addedCount}개를 추가했습니다.");
        FlushMessagesToConsole(true);
    }

    // CSV 데이터를 ScriptableObject로 임포트한다
    private void ImportFromCsv()
    {
        ClearRunState();
        if (!TryReadAndValidateCsv(out List<ItemCsvRow> rows))
        {
            FlushMessagesToConsole(false);
            return;
        }

        if (!TryEnsureMainListSo(out ItemMetaDataListSo targetListSo))
        {
            FlushMessagesToConsole(false);
            return;
        }

        EnsureAssetFolder(DEFAULT_SO_SAVE_PATH);

        int createdCount = 0;
        int updatedCount = 0;
        List<ItemMetaDataSo> importedItems = new List<ItemMetaDataSo>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            ItemCsvRow row = rows[i];
            string assetPath = $"{DEFAULT_SO_SAVE_PATH}/{row.Type}_ItemMetaData.asset";
            ItemMetaDataSo itemSo = AssetDatabase.LoadAssetAtPath<ItemMetaDataSo>(assetPath);
            if (itemSo == null)
            {
                itemSo = CreateInstance<ItemMetaDataSo>();
                AssetDatabase.CreateAsset(itemSo, assetPath);
                createdCount++;
            }
            else
            {
                updatedCount++;
            }

            itemSo.Type = row.Type;
            itemSo.Grade = row.Grade;
            itemSo.Createable = row.Createable;
            itemSo.CountPerCraft = row.CountPerCraft;
            itemSo.ItemsToCreate = row.ItemsToCreate;
            itemSo.Decomposable = row.Decomposable;
            itemSo.ItemsFromDecompose = row.ItemsFromDecompose;
            itemSo.Name = row.Name;
            itemSo.InfoText = row.InfoText;
            itemSo.ItemImage = row.ItemImage;
            ApplyAdditionalFieldValues(itemSo, row.FieldValues, row.LineNumber);
            EditorUtility.SetDirty(itemSo);
            importedItems.Add(itemSo);
        }

        targetListSo.MetaDataList = importedItems;
        EditorUtility.SetDirty(targetListSo);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AddMessage($"임포트 완료: 생성 {createdCount}개, 갱신 {updatedCount}개, 리스트 갱신 {importedItems.Count}개");
        FlushMessagesToConsole(true);
    }

    // ScriptableObject 데이터를 CSV로 익스포트한다
    private void ExportToCsv()
    {
        ClearRunState();
        if (!TryEnsureMainListSo(out ItemMetaDataListSo targetListSo))
        {
            FlushMessagesToConsole(false);
            return;
        }

        if (targetListSo.MetaDataList == null || targetListSo.MetaDataList.Count == 0)
        {
            AddMessage("메인 리스트 SO가 비어 있습니다.");
            FlushMessagesToConsole(false);
            return;
        }

        StringBuilder builder = new StringBuilder(1024);
        CsvColumnDefinition[] columnDefinitions = BuildCsvColumnDefinitions();
        if (!ValidateSupportedColumnDefinitions(columnDefinitions))
        {
            FlushMessagesToConsole(false);
            return;
        }

        AppendHeader(builder, columnDefinitions);
        for (int i = 0; i < targetListSo.MetaDataList.Count; i++)
        {
            ItemMetaDataSo item = targetListSo.MetaDataList[i];
            if (item == null)
            {
                continue;
            }

            AppendItemRow(builder, columnDefinitions, item);
        }

        if (!WriteCsvText(builder.ToString()))
        {
            FlushMessagesToConsole(false);
            return;
        }

        AssetDatabase.Refresh();
        AddMessage("익스포트 완료: " + DEFAULT_CSV_PATH);
        FlushMessagesToConsole(true);
    }

    // 아이템 CSV 파일을 기본 앱으로 연다
    private void OpenItemCsv()
    {
        ClearRunState();
        bool isSuccess = TryOpenCsvFile(DEFAULT_CSV_PATH);
        FlushMessagesToConsole(isSuccess);
    }

    // 누락된 enum 이름을 포함해 RewardCurrencyType 파일을 재생성한다
    private void RegenerateRewardCurrencyEnumFromMissingNames()
    {
        if (missingEnumNames.Count == 0)
        {
            return;
        }

        string enumPath = FindRewardCurrencyTypeScriptPath();
        if (string.IsNullOrEmpty(enumPath))
        {
            AddMessage("RewardCurrencyType.cs 파일을 찾을 수 없습니다.");
            FlushMessagesToConsole(false);
            return;
        }

        List<RewardCurrencyEnumEntry> enumEntries = GetCurrentRewardCurrencyEntries();
        for (int i = 0; i < missingEnumNames.Count; i++)
        {
            string enumName = missingEnumNames[i];
            if (!ContainsRewardCurrencyEntry(enumEntries, enumName))
            {
                enumEntries.Add(new RewardCurrencyEnumEntry
                {
                    Name = enumName,
                    Value = GetNextRewardCurrencyValue(enumEntries)
                });
            }
        }

        if (!EditorUtility.DisplayDialog("RewardCurrencyType 재생성", $"다음 enum 파일을 재생성합니다.\n{enumPath}\n\nUnity 컴파일 후 다시 Import를 실행하세요.", "재생성", "취소"))
        {
            return;
        }

        File.WriteAllText(enumPath, BuildRewardCurrencyTypeSource(enumEntries), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        AddMessage("RewardCurrencyType.cs를 재생성했습니다. 컴파일 완료 후 CSV 임포트를 다시 실행하세요.");
        FlushMessagesToConsole(true);
    }

    // CSV 파일을 읽고 모든 행을 검증한다
    private bool TryReadAndValidateCsv(out List<ItemCsvRow> rows)
    {
        rows = new List<ItemCsvRow>();
        if (!File.Exists(DEFAULT_CSV_PATH))
        {
            AddMessage("CSV 파일을 찾을 수 없습니다: " + DEFAULT_CSV_PATH);
            return false;
        }

        if (!TryReadCsvText(out string csvText))
        {
            return false;
        }

        List<List<string>> table = ParseCsv(csvText);
        if (table.Count <= 1)
        {
            AddMessage("CSV에 데이터 행이 없습니다.");
            return false;
        }

        if (!TryBuildHeaderMap(table[0], out Dictionary<string, int> headerMap, out Dictionary<string, string> headerTypeMap))
        {
            return false;
        }

        if (!ValidateHeaderTypes(headerTypeMap))
        {
            return false;
        }

        List<string> errors = new List<string>();
        for (int i = 1; i < table.Count; i++)
        {
            List<string> fields = table[i];
            if (IsEmptyCsvRow(fields))
            {
                continue;
            }

            if (TryParseRow(fields, headerMap, i + 1, rows, errors))
            {
                continue;
            }
        }

        if (missingEnumNames.Count > 0)
        {
            AddMessage("누락된 RewardCurrencyType이 있어 임포트를 중단합니다. enum 재생성 후 다시 실행하세요.");
            return false;
        }

        if (errors.Count > 0)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                AddMessage(errors[i]);
            }

            return false;
        }

        return true;
    }

    // CSV 헤더를 컬럼 인덱스 맵으로 변환한다
    private bool TryBuildHeaderMap(List<string> headers, out Dictionary<string, int> headerMap, out Dictionary<string, string> headerTypeMap)
    {
        headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        headerTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            string header = NormalizeHeaderName(headers[i]);
            if (!string.IsNullOrEmpty(header) && !headerMap.ContainsKey(header))
            {
                headerMap.Add(header, i);
            }

            string typeName = ExtractHeaderTypeName(headers[i]);
            if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(typeName) && !headerTypeMap.ContainsKey(header))
            {
                headerTypeMap.Add(header, typeName);
            }
        }

        string[] requiredColumns = { TYPE_COLUMN, GRADE_COLUMN, CREATEABLE_COLUMN, COUNT_PER_CRAFT_COLUMN, CRAFT_COLUMN, DECOMPOSABLE_COLUMN, DECOMPOSE_COLUMN, NAME_COLUMN, INFO_TEXT_COLUMN, IMAGE_PATH_COLUMN };
        for (int i = 0; i < requiredColumns.Length; i++)
        {
            if (!headerMap.ContainsKey(requiredColumns[i]))
            {
                AddMessage("필수 CSV 컬럼이 없습니다: " + requiredColumns[i]);
                return false;
            }
        }

        return true;
    }

    // CSV로 임포트 가능한 타입만 헤더 생성과 익스포트를 허용한다
    private bool ValidateSupportedColumnDefinitions(CsvColumnDefinition[] columnDefinitions)
    {
        bool isValid = true;
        for (int i = 0; i < columnDefinitions.Length; i++)
        {
            CsvColumnDefinition definition = columnDefinitions[i];
            if (IsSupportedCsvFieldType(definition.Field.FieldType))
            {
                continue;
            }

            AddMessage($"익스포트 중단: {definition.Field.Name} 필드는 CSV 임포트 파서가 없는 타입입니다. 타입: {GetReadableTypeName(definition.Field.FieldType)}");
            isValid = false;
        }

        if (!isValid)
        {
            AddMessage("지원 타입: string, int, float, bool, enum, Sprite, List<ItemCreatfData>, List<ItemDecomposeData>");
        }

        return isValid;
    }

    // CSV 임포트 파서가 준비된 필드 타입인지 확인한다
    private static bool IsSupportedCsvFieldType(Type fieldType)
    {
        return fieldType == typeof(string)
            || fieldType == typeof(int)
            || fieldType == typeof(float)
            || fieldType == typeof(bool)
            || fieldType.IsEnum
            || fieldType == typeof(Sprite)
            || fieldType == typeof(List<ItemCreatfData>)
            || fieldType == typeof(List<ItemDecomposeData>);
    }

    // CSV 헤더 타입이 현재 ItemMetaDataSo 필드 타입과 일치하는지 확인한다
    private bool ValidateHeaderTypes(Dictionary<string, string> headerTypeMap)
    {
        CsvColumnDefinition[] columnDefinitions = BuildCsvColumnDefinitions();
        for (int i = 0; i < columnDefinitions.Length; i++)
        {
            CsvColumnDefinition definition = columnDefinitions[i];
            if (!headerTypeMap.TryGetValue(definition.ColumnName, out string csvTypeName))
            {
                continue;
            }

            if (string.Equals(csvTypeName, definition.TypeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddMessage($"CSV 헤더 타입이 현재 ItemMetaDataSo 필드 타입과 다릅니다. 컬럼: {definition.ColumnName}, CSV: {csvTypeName}, 현재: {definition.TypeName}");
            return false;
        }

        return true;
    }

    // 설명과 타입이 붙은 CSV 헤더에서 실제 컬럼 키만 추출한다
    private static string NormalizeHeaderName(string header)
    {
        string normalizedHeader = (header ?? string.Empty).Trim('\uFEFF').Trim();
        int descriptionIndex = normalizedHeader.IndexOf('(');
        if (descriptionIndex < 0)
        {
            descriptionIndex = normalizedHeader.IndexOf('（');
        }

        string columnName = descriptionIndex >= 0 ? normalizedHeader.Substring(0, descriptionIndex).Trim() : normalizedHeader;
        int typeIndex = columnName.IndexOf('<');
        return typeIndex >= 0 ? columnName.Substring(0, typeIndex).Trim() : columnName;
    }

    // CSV 헤더에서 타입 이름을 추출한다
    private static string ExtractHeaderTypeName(string header)
    {
        string normalizedHeader = (header ?? string.Empty).Trim('\uFEFF').Trim();
        int typeStartIndex = normalizedHeader.IndexOf('<');
        if (typeStartIndex < 0)
        {
            return string.Empty;
        }

        int typeEndIndex = normalizedHeader.IndexOf('>', typeStartIndex + 1);
        if (typeEndIndex <= typeStartIndex)
        {
            return string.Empty;
        }

        return normalizedHeader.Substring(typeStartIndex + 1, typeEndIndex - typeStartIndex - 1).Trim();
    }

    // ItemMetaDataSo 필드 목록을 CSV 컬럼 정의로 변환한다
    private static CsvColumnDefinition[] BuildCsvColumnDefinitions()
    {
        FieldInfo[] fields = typeof(ItemMetaDataSo).GetFields(BindingFlags.Instance | BindingFlags.Public);
        Array.Sort(fields, CompareFieldDeclarationOrder);

        CsvColumnDefinition[] definitions = new CsvColumnDefinition[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            definitions[i] = new CsvColumnDefinition
            {
                Field = field,
                ColumnName = GetCsvColumnName(field),
                TypeName = GetCsvTypeName(field),
                Description = GetCsvColumnDescription(field)
            };
        }

        return definitions;
    }

    // 필드 선언 순서에 가깝게 정렬한다
    private static int CompareFieldDeclarationOrder(FieldInfo left, FieldInfo right)
    {
        return left.MetadataToken.CompareTo(right.MetadataToken);
    }

    // 필드에 대응되는 CSV 컬럼명을 반환한다
    private static string GetCsvColumnName(FieldInfo field)
    {
        if (field.FieldType == typeof(Sprite))
        {
            return field.Name + "AssetPath";
        }

        return field.Name;
    }

    // 필드에 대응되는 CSV 타입 이름을 반환한다
    private static string GetCsvTypeName(FieldInfo field)
    {
        Type fieldType = field.FieldType;
        if (fieldType == typeof(Sprite))
        {
            return "SpriteAssetPath";
        }

        if (fieldType == typeof(List<ItemCreatfData>))
        {
            return "ItemCreatfDataList";
        }

        if (fieldType == typeof(List<ItemDecomposeData>))
        {
            return "ItemDecomposeDataList";
        }

        if (fieldType.IsEnum)
        {
            return fieldType.Name;
        }

        return fieldType.Name;
    }

    // 사용자에게 표시할 타입 이름을 반환한다
    private static string GetReadableTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        Type[] genericArguments = type.GetGenericArguments();
        StringBuilder builder = new StringBuilder(type.Name);
        int genericMarkerIndex = builder.ToString().IndexOf('`');
        if (genericMarkerIndex >= 0)
        {
            builder.Length = genericMarkerIndex;
        }

        builder.Append('<');
        for (int i = 0; i < genericArguments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(GetReadableTypeName(genericArguments[i]));
        }

        builder.Append('>');
        return builder.ToString();
    }

    // 필드의 HeaderAttribute 설명을 CSV 설명으로 반환한다
    private static string GetCsvColumnDescription(FieldInfo field)
    {
        HeaderAttribute headerAttribute = Attribute.GetCustomAttribute(field, typeof(HeaderAttribute)) as HeaderAttribute;
        if (headerAttribute != null && !string.IsNullOrWhiteSpace(headerAttribute.header))
        {
            return headerAttribute.header;
        }

        return ObjectNames.NicifyVariableName(field.Name);
    }

    // CSV 헤더 행을 추가한다
    private static void AppendHeader(StringBuilder builder, CsvColumnDefinition[] columnDefinitions)
    {
        for (int i = 0; i < columnDefinitions.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            CsvColumnDefinition definition = columnDefinitions[i];
            builder.Append(EscapeCsvField(FormatHeader(definition)));
        }

        builder.AppendLine();
    }

    // CSV 헤더 셀 문자열을 생성한다
    private static string FormatHeader(CsvColumnDefinition definition)
    {
        return $"{definition.ColumnName}<{definition.TypeName}>({definition.Description})";
    }

    // 아이템 데이터를 CSV 행으로 추가한다
    private static void AppendItemRow(StringBuilder builder, CsvColumnDefinition[] columnDefinitions, ItemMetaDataSo item)
    {
        for (int i = 0; i < columnDefinitions.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(EscapeCsvField(FormatFieldValue(columnDefinitions[i], item)));
        }

        builder.AppendLine();
    }

    // 필드 값을 CSV 셀 문자열로 변환한다
    private static string FormatFieldValue(CsvColumnDefinition definition, ItemMetaDataSo item)
    {
        object value = definition.Field.GetValue(item);
        if (value == null)
        {
            return string.Empty;
        }

        if (definition.Field.FieldType == typeof(Sprite))
        {
            return AssetDatabase.GetAssetPath((Sprite)value);
        }

        if (definition.Field.FieldType == typeof(List<ItemCreatfData>))
        {
            return FormatCraftItems((List<ItemCreatfData>)value);
        }

        if (definition.Field.FieldType == typeof(List<ItemDecomposeData>))
        {
            return FormatDecomposeItems((List<ItemDecomposeData>)value);
        }

        if (definition.Field.FieldType.IsEnum)
        {
            return FormatEnumValue((Enum)value);
        }

        return value.ToString();
    }

    // CSV 파일을 UTF-8 BOM 포함 텍스트로 저장한다
    private bool WriteCsvText(string csvText)
    {
        string directoryPath = Path.GetDirectoryName(DEFAULT_CSV_PATH);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        try
        {
            File.WriteAllText(DEFAULT_CSV_PATH, csvText, new UTF8Encoding(true));
            return true;
        }
        catch (IOException exception)
        {
            AddMessage("CSV 파일을 쓸 수 없습니다. Excel이나 다른 프로그램에서 파일을 열어 잠금 중일 수 있습니다. 파일을 닫은 뒤 다시 실행하세요.");
            AddMessage(exception.Message);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            AddMessage("CSV 파일 쓰기 권한이 없습니다: " + DEFAULT_CSV_PATH);
            AddMessage(exception.Message);
            return false;
        }
    }

    // CSV 테이블에 누락된 헤더 컬럼을 추가한다
    private static int AddMissingHeaderColumns(List<List<string>> table, CsvColumnDefinition[] columnDefinitions)
    {
        Dictionary<string, int> headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<string> headers = table[0];
        for (int i = 0; i < headers.Count; i++)
        {
            string header = NormalizeHeaderName(headers[i]);
            if (!string.IsNullOrEmpty(header) && !headerMap.ContainsKey(header))
            {
                headerMap.Add(header, i);
            }
        }

        int addedCount = 0;
        for (int i = 0; i < columnDefinitions.Length; i++)
        {
            CsvColumnDefinition definition = columnDefinitions[i];
            if (headerMap.ContainsKey(definition.ColumnName))
            {
                continue;
            }

            headers.Add(FormatHeader(definition));
            for (int rowIndex = 1; rowIndex < table.Count; rowIndex++)
            {
                table[rowIndex].Add(string.Empty);
            }

            addedCount++;
        }

        return addedCount;
    }

    // CSV 테이블을 텍스트로 재조립한다
    private static string BuildCsvText(List<List<string>> table)
    {
        StringBuilder builder = new StringBuilder(1024);
        for (int rowIndex = 0; rowIndex < table.Count; rowIndex++)
        {
            List<string> row = table[rowIndex];
            for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                if (columnIndex > 0)
                {
                    builder.Append(',');
                }

                builder.Append(EscapeCsvField(row[columnIndex]));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    // CSV 파일을 UTF-8, UTF-16, CP949 순서로 읽는다
    private bool TryReadCsvText(out string csvText)
    {
        csvText = string.Empty;
        byte[] bytes;
        try
        {
            using (FileStream stream = new FileStream(DEFAULT_CSV_PATH, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytes = new byte[stream.Length];
                int totalRead = 0;
                while (totalRead < bytes.Length)
                {
                    int read = stream.Read(bytes, totalRead, bytes.Length - totalRead);
                    if (read <= 0)
                    {
                        break;
                    }

                    totalRead += read;
                }
            }
        }
        catch (IOException exception)
        {
            AddMessage("CSV 파일을 읽을 수 없습니다. Excel에서 저장 중이거나 파일을 독점 잠금 중일 수 있습니다. Excel 저장을 끝내거나 파일을 닫은 뒤 다시 실행하세요.");
            AddMessage(exception.Message);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            AddMessage("CSV 파일 접근 권한이 없습니다: " + DEFAULT_CSV_PATH);
            AddMessage(exception.Message);
            return false;
        }

        if (HasUtf8Bom(bytes))
        {
            csvText = new UTF8Encoding(true, false).GetString(bytes, 3, bytes.Length - 3);
            WarnIfReplacementCharacterExists(csvText);
            return true;
        }

        if (HasUtf16LittleEndianBom(bytes))
        {
            csvText = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            WarnIfReplacementCharacterExists(csvText);
            AddMessage("CSV가 UTF-16 LE 인코딩이어서 변환 가능한 텍스트로 읽었습니다.");
            return true;
        }

        if (HasUtf16BigEndianBom(bytes))
        {
            csvText = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            WarnIfReplacementCharacterExists(csvText);
            AddMessage("CSV가 UTF-16 BE 인코딩이어서 변환 가능한 텍스트로 읽었습니다.");
            return true;
        }

        if (TryDecodeStrictUtf8(bytes, out csvText))
        {
            WarnIfReplacementCharacterExists(csvText);
            return true;
        }

        if (TryDecodeCp949(bytes, out csvText))
        {
            WarnIfReplacementCharacterExists(csvText);
            AddMessage("CSV가 UTF-8이 아니어서 CP949(한국어 Windows CSV)로 읽었습니다.");
            return true;
        }

        csvText = Encoding.Default.GetString(bytes);
        WarnIfReplacementCharacterExists(csvText);
        AddMessage("CSV가 UTF-8/UTF-16/CP949로 명확히 읽히지 않아 OS 기본 인코딩으로 읽었습니다.");
        return true;
    }

    // UTF-8 BOM이 있는지 확인한다
    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }

    // UTF-16 LE BOM이 있는지 확인한다
    private static bool HasUtf16LittleEndianBom(byte[] bytes)
    {
        return bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE;
    }

    // UTF-16 BE BOM이 있는지 확인한다
    private static bool HasUtf16BigEndianBom(byte[] bytes)
    {
        return bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF;
    }

    // 엄격한 UTF-8 디코딩을 시도한다
    private static bool TryDecodeStrictUtf8(byte[] bytes, out string text)
    {
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    // CP949 디코딩을 시도한다
    private static bool TryDecodeCp949(byte[] bytes, out string text)
    {
        text = string.Empty;
        try
        {
            Encoding encoding = Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            text = encoding.GetString(bytes);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // 깨진 문자 대체 기호가 포함되어 있는지 확인한다
    private static bool ContainsReplacementCharacter(string text)
    {
        return !string.IsNullOrEmpty(text) && text.IndexOf('\uFFFD') >= 0;
    }

    // 깨진 문자 대체 기호가 있으면 복구 불가 가능성을 메시지에 기록한다
    private void WarnIfReplacementCharacterExists(string text)
    {
        if (ContainsReplacementCharacter(text))
        {
            AddMessage("경고: CSV 텍스트에 깨진 대체 문자(�)가 포함되어 있습니다. 이미 깨진 상태로 저장된 파일일 수 있습니다.");
        }
    }

    // CSV 한 행을 아이템 행 데이터로 변환한다
    private bool TryParseRow(List<string> fields, Dictionary<string, int> headerMap, int lineNumber, List<ItemCsvRow> rows, List<string> errors)
    {
        string typeText = GetField(fields, headerMap, TYPE_COLUMN).Trim();
        string gradeText = GetOptionalField(fields, headerMap, GRADE_COLUMN).Trim();
        string itemName = GetField(fields, headerMap, NAME_COLUMN);
        string infoText = GetField(fields, headerMap, INFO_TEXT_COLUMN);
        string imagePath = GetField(fields, headerMap, IMAGE_PATH_COLUMN).Trim();
        string createableText = GetField(fields, headerMap, CREATEABLE_COLUMN).Trim();
        string countPerCraftText = GetField(fields, headerMap, COUNT_PER_CRAFT_COLUMN).Trim();
        string craftText = GetField(fields, headerMap, CRAFT_COLUMN).Trim();
        string decomposableText = GetField(fields, headerMap, DECOMPOSABLE_COLUMN).Trim();
        string decomposeText = GetField(fields, headerMap, DECOMPOSE_COLUMN).Trim();

        if (string.IsNullOrEmpty(typeText))
        {
            errors.Add($"{lineNumber}행: Type이 비어 있습니다.");
            return false;
        }

        if (!TryParseRewardCurrencyTypeCell(typeText, lineNumber, TYPE_COLUMN, out RewardCurrencyType itemType, errors, true))
        {
            return false;
        }

        ItemGrade itemGrade = default;
        object gradeValue = null;
        if (!string.IsNullOrEmpty(gradeText) && !TryParseEnumCell(gradeText, typeof(ItemGrade), lineNumber, GRADE_COLUMN, out gradeValue, errors, false))
        {
            return false;
        }
        else if (!string.IsNullOrEmpty(gradeText))
        {
            itemGrade = (ItemGrade)gradeValue;
        }

        if (!bool.TryParse(createableText, out bool createable))
        {
            errors.Add($"{lineNumber}행: Createable 값이 유효하지 않습니다. 값: {createableText}");
            return false;
        }

        if (!int.TryParse(countPerCraftText, out int countPerCraft) || countPerCraft < 0)
        {
            errors.Add($"{lineNumber}행: CountPerCraft 값이 유효하지 않습니다. 값: {countPerCraftText}");
            return false;
        }

        if (!bool.TryParse(decomposableText, out bool decomposable))
        {
            errors.Add($"{lineNumber}행: Decomposable 값이 유효하지 않습니다. 값: {decomposableText}");
            return false;
        }

        Sprite sprite = null;
        if (!string.IsNullOrEmpty(imagePath))
        {
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            if (sprite == null)
            {
                string message = $"{lineNumber}행: Sprite를 찾을 수 없습니다. 경로: {imagePath}";
                if (treatMissingSpriteAsError)
                {
                    errors.Add(message);
                    return false;
                }

                AddMessage("경고: " + message);
            }
        }

        if (!TryParseCraftItems(craftText, lineNumber, out List<ItemCreaftData> craftItems, errors))
        {
            return false;
        }

        if (!TryParseDecomposeItems(decomposeText, lineNumber, out List<ItemDecomposeData> decomposeItems, errors))
        {
            return false;
        }

        rows.Add(new ItemCsvRow
        {
            LineNumber = lineNumber,
            Type = itemType,
            Grade = itemGrade,
            Createable = createable,
            CountPerCraft = countPerCraft,
            ItemsToCreate = craftItems,
            Decomposable = decomposable,
            ItemsFromDecompose = decomposeItems,
            Name = itemName,
            InfoText = infoText,
            ItemImage = sprite,
            FieldValues = BuildFieldValueMap(fields, headerMap)
        });
        return true;
    }

    // CSV 행의 전체 필드 값을 컬럼명 기준으로 보관한다
    private static Dictionary<string, string> BuildFieldValueMap(List<string> fields, Dictionary<string, int> headerMap)
    {
        Dictionary<string, string> fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, int> pair in headerMap)
        {
            int index = pair.Value;
            string value = index >= 0 && index < fields.Count ? fields[index] ?? string.Empty : string.Empty;
            fieldValues[pair.Key] = value;
        }

        return fieldValues;
    }

    // 고정 매핑 외의 지원 가능한 CSV 값을 ScriptableObject 필드에 반영한다
    private void ApplyAdditionalFieldValues(ItemMetaDataSo itemSo, Dictionary<string, string> fieldValues, int lineNumber)
    {
        if (fieldValues == null)
        {
            return;
        }

        CsvColumnDefinition[] columnDefinitions = BuildCsvColumnDefinitions();
        for (int i = 0; i < columnDefinitions.Length; i++)
        {
            CsvColumnDefinition definition = columnDefinitions[i];
            if (IsManuallyAssignedColumn(definition.ColumnName) || !fieldValues.TryGetValue(definition.ColumnName, out string value))
            {
                continue;
            }

            TrySetFieldValue(itemSo, definition, value, lineNumber);
        }
    }

    // 수동으로 검증과 할당을 마친 컬럼인지 확인한다
    private static bool IsManuallyAssignedColumn(string columnName)
    {
        return string.Equals(columnName, TYPE_COLUMN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnName, GRADE_COLUMN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnName, NAME_COLUMN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnName, INFO_TEXT_COLUMN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnName, IMAGE_PATH_COLUMN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnName, CREATEABLE_COLUMN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnName, COUNT_PER_CRAFT_COLUMN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnName, CRAFT_COLUMN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnName, DECOMPOSABLE_COLUMN, StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnName, DECOMPOSE_COLUMN, StringComparison.OrdinalIgnoreCase);
    }

    // 지원 가능한 타입의 CSV 값을 필드에 설정한다
    private bool TrySetFieldValue(ItemMetaDataSo itemSo, CsvColumnDefinition definition, string value, int lineNumber)
    {
        Type fieldType = definition.Field.FieldType;
        string trimmedValue = value == null ? string.Empty : value.Trim();
        if (fieldType == typeof(string))
        {
            definition.Field.SetValue(itemSo, value ?? string.Empty);
            return true;
        }

        if (fieldType == typeof(int))
        {
            if (int.TryParse(trimmedValue, out int result))
            {
                definition.Field.SetValue(itemSo, result);
                return true;
            }

            AddMessage($"{lineNumber}행: {definition.ColumnName} 정수 값이 유효하지 않아 기존 값을 유지합니다. 값: {value}");
            return false;
        }

        if (fieldType == typeof(float))
        {
            if (float.TryParse(trimmedValue, out float result))
            {
                definition.Field.SetValue(itemSo, result);
                return true;
            }

            AddMessage($"{lineNumber}행: {definition.ColumnName} 실수 값이 유효하지 않아 기존 값을 유지합니다. 값: {value}");
            return false;
        }

        if (fieldType == typeof(bool))
        {
            if (bool.TryParse(trimmedValue, out bool result))
            {
                definition.Field.SetValue(itemSo, result);
                return true;
            }

            AddMessage($"{lineNumber}행: {definition.ColumnName} bool 값이 유효하지 않아 기존 값을 유지합니다. 값: {value}");
            return false;
        }

        if (fieldType.IsEnum)
        {
            if (TryParseEnumCell(trimmedValue, fieldType, lineNumber, definition.ColumnName, out object enumValue, null, false))
            {
                definition.Field.SetValue(itemSo, enumValue);
                return true;
            }

            AddMessage($"{lineNumber}행: {definition.ColumnName} enum 값이 유효하지 않아 기존 값을 유지합니다. 값: {value}");
            return false;
        }

        if (fieldType == typeof(Sprite))
        {
            Sprite sprite = string.IsNullOrEmpty(trimmedValue) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(trimmedValue);
            if (sprite == null && !string.IsNullOrEmpty(trimmedValue))
            {
                AddMessage($"{lineNumber}행: {definition.ColumnName} Sprite를 찾을 수 없어 기존 값을 유지합니다. 경로: {trimmedValue}");
                return false;
            }

            definition.Field.SetValue(itemSo, sprite);
            return true;
        }

        return false;
    }

    // 제작 재료 문자열을 ItemCreatfData 목록으로 변환한다
    private bool TryParseCraftItems(string craftText, int lineNumber, out List<ItemCreatfData> craftItems, List<string> errors)
    {
        craftItems = new List<ItemCreaftData>();
        if (string.IsNullOrWhiteSpace(craftText))
        {
            return true;
        }

        string[] entries = craftText.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }

            string[] parts = entry.Split(':');
            if (parts.Length != 2)
            {
                errors.Add($"{lineNumber}행: 제작 재료 형식이 잘못되었습니다. 값: {entry}");
                return false;
            }

            string typeText = parts[0].Trim();
            if (!TryParseRewardCurrencyTypeCell(typeText, lineNumber, CRAFT_COLUMN, out RewardCurrencyType materialType, errors, true))
            {
                return false;
            }

            if (!int.TryParse(parts[1].Trim(), out int count) || count < 0)
            {
                errors.Add($"{lineNumber}행: 제작 재료 Count가 유효하지 않습니다. 값: {parts[1]}");
                return false;
            }

            craftItems.Add(new ItemCreatfData { Type = materialType, Count = count });
        }

        return true;
    }

    // 분해 결과 문자열을 ItemDecomposeData 목록으로 변환한다
    private bool TryParseDecomposeItems(string decomposeText, int lineNumber, out List<ItemDecomposeData> decomposeItems, List<string> errors)
    {
        decomposeItems = new List<ItemDecomposeData>();
        if (string.IsNullOrWhiteSpace(decomposeText))
        {
            return true;
        }

        string[] entries = decomposeText.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }

            string[] parts = entry.Split(':');
            if (parts.Length != 2)
            {
                errors.Add($"{lineNumber}행: 분해 결과 형식이 잘못되었습니다. 값: {entry}");
                return false;
            }

            string typeText = parts[0].Trim();
            if (!TryParseExistingDecomposeTypeCell(typeText, lineNumber, out RewardCurrencyType itemType, errors))
            {
                return false;
            }

            string[] rangeParts = parts[1].Trim().Split('~');
            if (rangeParts.Length != 2)
            {
                errors.Add($"{lineNumber}행: 분해 결과 개수 범위 형식이 잘못되었습니다. 값: {parts[1]}");
                return false;
            }

            if (!int.TryParse(rangeParts[0].Trim(), out int min) || min < 0)
            {
                errors.Add($"{lineNumber}행: 분해 결과 최소 개수가 유효하지 않습니다. 값: {rangeParts[0]}");
                return false;
            }

            if (!int.TryParse(rangeParts[1].Trim(), out int max) || max < min)
            {
                errors.Add($"{lineNumber}행: 분해 결과 최대 개수가 유효하지 않습니다. 값: {rangeParts[1]}");
                return false;
            }

            decomposeItems.Add(new ItemDecomposeData { Type = itemType, min = min, max = max });
        }

        return true;
    }

    // 고정 경로의 메인 리스트 SO를 로드한다
    private void LoadFixedMainListSo()
    {
        mainListSo = AssetDatabase.LoadAssetAtPath<ItemMetaDataListSo>(DEFAULT_LIST_SO_PATH);
    }

    // 메인 리스트 SO를 가져오거나 필요한 경우 생성한다
    private bool TryEnsureMainListSo(out ItemMetaDataListSo targetListSo)
    {
        targetListSo = mainListSo;
        if (targetListSo != null)
        {
            return true;
        }

        string existingPath = DEFAULT_LIST_SO_PATH;
        targetListSo = AssetDatabase.LoadAssetAtPath<ItemMetaDataListSo>(existingPath);
        if (targetListSo != null)
        {
            mainListSo = targetListSo;
            return true;
        }

        EnsureAssetFolder(Path.GetDirectoryName(existingPath)?.Replace('\\', '/'));
        targetListSo = CreateInstance<ItemMetaDataListSo>();
        targetListSo.MetaDataList = new List<ItemMetaDataSo>();
        AssetDatabase.CreateAsset(targetListSo, existingPath);
        mainListSo = targetListSo;
        AddMessage("메인 리스트 SO를 생성했습니다: " + existingPath);
        return true;
    }

    // 에셋 저장 폴더가 없으면 생성한다
    private static void EnsureAssetFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] parts = assetFolder.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }

    // 지정한 CSV 파일을 기본 앱으로 연다
    private bool TryOpenCsvFile(string path)
    {
        if (!File.Exists(path))
        {
            AddMessage("CSV 파일을 찾을 수 없습니다: " + path);
            return false;
        }

        EditorUtility.OpenWithDefaultApp(path);
        AddMessage("CSV 파일 열기 요청: " + path);
        return true;
    }

    // CSV 텍스트를 행과 필드 목록으로 파싱한다
    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool inQuotes = false;
        char delimiter = DetectDelimiter(text);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                row.Add(field.ToString());
                field.Length = 0;
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString());
                rows.Add(row);
                row = new List<string>();
                field.Length = 0;
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    // CSV 텍스트의 구분자를 쉼표 또는 탭 중에서 감지한다
    private static char DetectDelimiter(string text)
    {
        int commaCount = 0;
        int tabCount = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r' || c == '\n')
            {
                break;
            }

            if (c == ',')
            {
                commaCount++;
            }
            else if (c == '\t')
            {
                tabCount++;
            }
        }

        return tabCount > commaCount ? '\t' : ',';
    }

    // CSV 필드 값을 안전하게 반환한다
    private static string GetField(List<string> fields, Dictionary<string, int> headerMap, string columnName)
    {
        int index = headerMap[columnName];
        return index >= 0 && index < fields.Count ? fields[index] ?? string.Empty : string.Empty;
    }

    // 선택 CSV 필드 값을 안전하게 반환한다
    private static string GetOptionalField(List<string> fields, Dictionary<string, int> headerMap, string columnName)
    {
        if (!headerMap.TryGetValue(columnName, out int index))
        {
            return string.Empty;
        }

        return index >= 0 && index < fields.Count ? fields[index] ?? string.Empty : string.Empty;
    }

    // CSV 행이 비어 있는지 확인한다
    private static bool IsEmptyCsvRow(List<string> fields)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(fields[i]))
            {
                return false;
            }
        }

        return true;
    }

    // CSV 필드로 안전하게 출력할 문자열로 변환한다
    private static string EscapeCsvField(string value)
    {
        string safeValue = value ?? string.Empty;
        bool needsQuote = safeValue.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuote)
        {
            return safeValue;
        }

        return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
    }

    // 제작 재료 목록을 CSV용 문자열로 변환한다
    private static string FormatCraftItems(List<ItemCreaftData> craftItems)
    {
        if (craftItems == null || craftItems.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(64);
        for (int i = 0; i < craftItems.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            ItemCreatfData item = craftItems[i];
            builder.Append(FormatEnumValue(item.Type));
            builder.Append(':');
            builder.Append(Mathf.Max(0, item.Count));
        }

        return builder.ToString();
    }

    // 분해 결과 목록을 CSV용 문자열로 변환한다
    private static string FormatDecomposeItems(List<ItemDecomposeData> decomposeItems)
    {
        if (decomposeItems == null || decomposeItems.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(64);
        for (int i = 0; i < decomposeItems.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            ItemDecomposeData item = decomposeItems[i];
            int min = Mathf.Max(0, item.min);
            int max = Mathf.Max(min, item.max);
            builder.Append(FormatEnumValue(item.Type));
            builder.Append(':');
            builder.Append(min);
            builder.Append('~');
            builder.Append(max);
        }

        return builder.ToString();
    }

    // enum 값을 이름=인덱스 형식으로 변환한다
    private static string FormatEnumValue(Enum enumValue)
    {
        return enumValue + "=" + Convert.ToInt64(enumValue);
    }

    // RewardCurrencyType 셀을 enum 이름과 인덱스로 검증해 변환한다
    private bool TryParseRewardCurrencyTypeCell(string text, int lineNumber, string columnName, out RewardCurrencyType result, List<string> errors, bool collectMissingEnum)
    {
        result = default;
        if (TryParseEnumCell(text, typeof(RewardCurrencyType), lineNumber, columnName, out object enumValue, errors, collectMissingEnum))
        {
            result = (RewardCurrencyType)enumValue;
            return true;
        }

        return false;
    }

    // 분해 결과에 이미 존재하는 RewardCurrencyType만 허용해 변환한다
    private bool TryParseExistingDecomposeTypeCell(string text, int lineNumber, out RewardCurrencyType result, List<string> errors)
    {
        result = default;
        string safeText = (text ?? string.Empty).Trim();
        int equalsIndex = safeText.IndexOf('=');
        string enumName = equalsIndex >= 0 ? safeText.Substring(0, equalsIndex).Trim() : safeText;
        string enumIndexText = equalsIndex >= 0 ? safeText.Substring(equalsIndex + 1).Trim() : string.Empty;
        if (!IsValidIdentifier(enumName))
        {
            errors.Add($"{lineNumber}행: ItemsFromDecompose에 잘못된 enum 이름이 들어 있습니다. 값: {text}");
            return false;
        }

        if (!Enum.TryParse(enumName, out result))
        {
            errors.Add($"{lineNumber}행: ItemsFromDecompose에 RewardCurrencyType에 없는 enum이 들어 있습니다. 값: {text}");
            return false;
        }

        if (string.IsNullOrEmpty(enumIndexText))
        {
            return true;
        }

        if (!long.TryParse(enumIndexText, out long csvEnumIndex))
        {
            errors.Add($"{lineNumber}행: ItemsFromDecompose enum 인덱스가 정수가 아닙니다. 값: {text}");
            return false;
        }

        long currentEnumIndex = Convert.ToInt64(result);
        if (csvEnumIndex != currentEnumIndex)
        {
            errors.Add($"{lineNumber}행: ItemsFromDecompose enum 인덱스가 현재 코드와 다릅니다. CSV: {text}, 현재: {enumName}={currentEnumIndex}");
            return false;
        }

        return true;
    }

    // enum 셀을 이름 또는 이름=인덱스 형식으로 변환한다
    private bool TryParseEnumCell(string text, Type enumType, int lineNumber, string columnName, out object enumValue, List<string> errors, bool collectMissingRewardCurrency)
    {
        enumValue = null;
        string safeText = (text ?? string.Empty).Trim();
        int equalsIndex = safeText.IndexOf('=');
        string enumName = equalsIndex >= 0 ? safeText.Substring(0, equalsIndex).Trim() : safeText;
        string enumIndexText = equalsIndex >= 0 ? safeText.Substring(equalsIndex + 1).Trim() : string.Empty;
        if (!IsValidIdentifier(enumName))
        {
            AddEnumParseError(errors, $"{lineNumber}행: {columnName} enum 이름이 C# 식별자로 유효하지 않습니다. 값: {text}");
            return false;
        }

        try
        {
            enumValue = Enum.Parse(enumType, enumName);
        }
        catch (ArgumentException)
        {
            if (collectMissingRewardCurrency && enumType == typeof(RewardCurrencyType))
            {
                AddMissingEnumName(enumName);
            }
            else
            {
                AddEnumParseError(errors, $"{lineNumber}행: {columnName} enum 이름이 유효하지 않습니다. 값: {text}");
            }

            return false;
        }

        if (string.IsNullOrEmpty(enumIndexText))
        {
            return true;
        }

        if (!long.TryParse(enumIndexText, out long csvEnumIndex))
        {
            AddEnumParseError(errors, $"{lineNumber}행: {columnName} enum 인덱스가 정수가 아닙니다. 값: {text}");
            return false;
        }

        long currentEnumIndex = Convert.ToInt64(enumValue);
        if (csvEnumIndex != currentEnumIndex)
        {
            AddEnumParseError(errors, $"{lineNumber}행: {columnName} enum 인덱스가 현재 코드와 다릅니다. CSV: {text}, 현재: {enumName}={currentEnumIndex}");
            return false;
        }

        return true;
    }

    // enum 파싱 오류를 검증 목록이나 실행 메시지에 기록한다
    private void AddEnumParseError(List<string> errors, string message)
    {
        if (errors != null)
        {
            errors.Add(message);
            return;
        }

        AddMessage(message);
    }

    // 누락된 enum 이름을 중복 없이 기록한다
    private void AddMissingEnumName(string enumName)
    {
        if (!missingEnumNames.Contains(enumName))
        {
            missingEnumNames.Add(enumName);
        }
    }

    // 현재 RewardCurrencyType enum 이름과 값을 반환한다
    private static List<RewardCurrencyEnumEntry> GetCurrentRewardCurrencyEntries()
    {
        Array values = Enum.GetValues(typeof(RewardCurrencyType));
        List<RewardCurrencyEnumEntry> entries = new List<RewardCurrencyEnumEntry>(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            object value = values.GetValue(i);
            entries.Add(new RewardCurrencyEnumEntry
            {
                Name = value.ToString(),
                Value = Convert.ToInt64(value)
            });
        }

        return entries;
    }

    // RewardCurrencyType 엔트리에 같은 이름이 있는지 확인한다
    private static bool ContainsRewardCurrencyEntry(List<RewardCurrencyEnumEntry> enumEntries, string enumName)
    {
        for (int i = 0; i < enumEntries.Count; i++)
        {
            if (string.Equals(enumEntries[i].Name, enumName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // 새 RewardCurrencyType에 부여할 다음 명시값을 계산한다
    private static long GetNextRewardCurrencyValue(List<RewardCurrencyEnumEntry> enumEntries)
    {
        long maxValue = -1L;
        for (int i = 0; i < enumEntries.Count; i++)
        {
            if (enumEntries[i].Value > maxValue)
            {
                maxValue = enumEntries[i].Value;
            }
        }

        return maxValue + 1L;
    }

    // RewardCurrencyType 스크립트 경로를 찾는다
    private static string FindRewardCurrencyTypeScriptPath()
    {
        string[] guids = AssetDatabase.FindAssets("RewardCurrencyType t:MonoScript");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileName(path) == "RewardCurrencyType.cs")
            {
                return path;
            }
        }

        return null;
    }

    // RewardCurrencyType enum 소스 코드를 생성한다
    private static string BuildRewardCurrencyTypeSource(List<RewardCurrencyEnumEntry> enumEntries)
    {
        StringBuilder builder = new StringBuilder(256);
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// 보상과 비용 계산에 공통으로 사용하는 재화 종류. <para/>");
        builder.AppendLine("/// 작업 UI에서 오름차순으로 정렬되기 때문에 등급이 높은 아이템일 수록 높은 번호를 부여할 것");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("public enum RewardCurrencyType");
        builder.AppendLine("{");
        for (int i = 0; i < enumEntries.Count; i++)
        {
            string suffix = i + 1 < enumEntries.Count ? "," : string.Empty;
            builder.AppendLine($"    {enumEntries[i].Name} = {enumEntries[i].Value}{suffix}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    // 문자열이 C# 식별자로 유효한지 확인한다
    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || !IsIdentifierStart(value[0]))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            if (!IsIdentifierPart(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    // 문자가 C# 식별자 시작 문자로 유효한지 확인한다
    private static bool IsIdentifierStart(char c)
    {
        return c == '_' || char.IsLetter(c);
    }

    // 문자가 C# 식별자 본문 문자로 유효한지 확인한다
    private static bool IsIdentifierPart(char c)
    {
        return c == '_' || char.IsLetterOrDigit(c);
    }

    // 실행 상태 메시지를 초기화한다
    private void ClearRunState()
    {
        lastMessages.Clear();
        missingEnumNames.Clear();
    }

    // 실행 메시지를 추가한다
    private void AddMessage(string message)
    {
        lastMessages.Add(message);
    }

    // 실행 메시지를 콘솔에 출력한다
    private void FlushMessagesToConsole(bool isSuccess)
    {
        string joinedMessage = string.Join("\n", lastMessages);
        if (isSuccess)
        {
            Debug.Log("[ItemDataEditorTool]\n" + joinedMessage);
        }
        else
        {
            Debug.LogWarning("[ItemDataEditorTool]\n" + joinedMessage);
        }
    }

    // OnGUI 레이아웃이 깨지지 않도록 버튼 동작 예외를 처리한다
    private void ExecuteSafely(Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (ExitGUIException)
        {
            throw;
        }
        catch (Exception exception)
        {
            ClearRunState();
            AddMessage("처리 중 예외가 발생했습니다. 콘솔 로그를 확인하세요.");
            AddMessage(exception.Message);
            Debug.LogException(exception);
        }
    }

    private struct ItemCsvRow
    {
        public int LineNumber;
        public RewardCurrencyType Type;
        public ItemGrade Grade;
        public bool Createable;
        public int CountPerCraft;
        public List<ItemCreatfData> ItemsToCreate;
        public bool Decomposable;
        public List<ItemDecomposeData> ItemsFromDecompose;
        public string Name;
        public string InfoText;
        public Sprite ItemImage;
        public Dictionary<string, string> FieldValues;
    }

    private struct CsvColumnDefinition
    {
        public FieldInfo Field;
        public string ColumnName;
        public string TypeName;
        public string Description;
    }

    private struct RewardCurrencyEnumEntry
    {
        public string Name;
        public long Value;
    }
}
#endif
