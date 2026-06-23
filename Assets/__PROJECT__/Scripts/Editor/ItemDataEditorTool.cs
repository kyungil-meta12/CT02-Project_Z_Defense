#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 아이템 메타데이터 CSV와 ScriptableObject 간 임포트/익스포트 및 재화 enum 갱신을 관리한다.
/// </summary>
public class ItemDataEditorTool : EditorWindow
{
    private const string TYPE_COLUMN = "Type";
    private const string NAME_COLUMN = "Name";
    private const string INFO_TEXT_COLUMN = "InfoText";
    private const string IMAGE_PATH_COLUMN = "ItemImageAssetPath";
    private const string CRAFT_COLUMN = "ItemsToCreate";
    private const string DEFAULT_CSV_PATH = "Assets/__PROJECT__/ItemData.csv";
    private const string DEFAULT_SO_SAVE_PATH = "Assets/__PROJECT__/Resources/ItemData";
    private const string DEFAULT_LIST_SO_PATH = "Assets/__PROJECT__/Resources/ItemData/ItemMetaDataList.asset";

    private string csvFilePath = DEFAULT_CSV_PATH;
    private string soSavePath = DEFAULT_SO_SAVE_PATH;
    private string mainListSoPath = DEFAULT_LIST_SO_PATH;
    private ItemMetaDataListSo mainListSo;
    private bool createMainListIfMissing = true;
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

    // 에디터 창 UI를 그린다
    private void OnGUI()
    {
        EditorGUILayout.LabelField("아이템 CSV ↔ ScriptableObject 관리 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        csvFilePath = EditorGUILayout.TextField("CSV 파일 경로", csvFilePath);
        soSavePath = EditorGUILayout.TextField("SO 저장 폴더", soSavePath);
        mainListSoPath = EditorGUILayout.TextField("메인 리스트 생성 경로", mainListSoPath);
        mainListSo = (ItemMetaDataListSo)EditorGUILayout.ObjectField("메인 리스트 SO", mainListSo, typeof(ItemMetaDataListSo), false);
        createMainListIfMissing = EditorGUILayout.Toggle("리스트 SO 자동 생성", createMainListIfMissing);
        treatMissingSpriteAsError = EditorGUILayout.Toggle("이미지 누락을 오류 처리", treatMissingSpriteAsError);

        EditorGUILayout.HelpBox("CSV 컬럼: Type, Name, InfoText, ItemImageAssetPath, ItemsToCreate. 제작 재료는 Type:Count;Type:Count 형식입니다. 없는 enum은 Import를 중단하고 별도 버튼으로 RewardCurrencyType.cs를 재생성합니다.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("검증만 실행", GUILayout.Height(34)))
        {
            ValidateOnly();
        }

        if (GUILayout.Button("CSV에서 임포트", GUILayout.Height(34)))
        {
            ImportFromCsv();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("CSV로 익스포트", GUILayout.Height(34)))
        {
            ExportToCsv();
        }

        using (new EditorGUI.DisabledScope(missingEnumNames.Count == 0))
        {
            if (GUILayout.Button("누락 enum 재생성", GUILayout.Height(34)))
            {
                RegenerateRewardCurrencyEnumFromMissingNames();
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

        EnsureAssetFolder(soSavePath);

        int createdCount = 0;
        int updatedCount = 0;
        List<ItemMetaDataSo> importedItems = new List<ItemMetaDataSo>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            ItemCsvRow row = rows[i];
            string assetPath = $"{soSavePath}/{row.Type}_ItemMetaData.asset";
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
            itemSo.Name = row.Name;
            itemSo.InfoText = row.InfoText;
            itemSo.ItemImage = row.ItemImage;
            itemSo.ItemsToCreate = row.ItemsToCreate;
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
        ItemMetaDataListSo targetListSo = mainListSo;
        if (targetListSo == null)
        {
            AddMessage("메인 리스트 SO가 할당되지 않았습니다.");
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
        builder.AppendLine($"{TYPE_COLUMN},{NAME_COLUMN},{INFO_TEXT_COLUMN},{IMAGE_PATH_COLUMN},{CRAFT_COLUMN}");
        for (int i = 0; i < targetListSo.MetaDataList.Count; i++)
        {
            ItemMetaDataSo item = targetListSo.MetaDataList[i];
            if (item == null)
            {
                continue;
            }

            string imagePath = item.ItemImage == null ? string.Empty : AssetDatabase.GetAssetPath(item.ItemImage);
            string craftText = FormatCraftItems(item.ItemsToCreate);
            builder.Append(EscapeCsvField(item.Type.ToString()));
            builder.Append(',');
            builder.Append(EscapeCsvField(item.Name));
            builder.Append(',');
            builder.Append(EscapeCsvField(item.InfoText));
            builder.Append(',');
            builder.Append(EscapeCsvField(imagePath));
            builder.Append(',');
            builder.Append(EscapeCsvField(craftText));
            builder.AppendLine();
        }

        string directoryPath = Path.GetDirectoryName(csvFilePath);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(csvFilePath, builder.ToString(), new UTF8Encoding(true));
        AssetDatabase.Refresh();
        AddMessage("익스포트 완료: " + csvFilePath);
        FlushMessagesToConsole(true);
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

        List<string> enumNames = GetCurrentRewardCurrencyNames();
        for (int i = 0; i < missingEnumNames.Count; i++)
        {
            string enumName = missingEnumNames[i];
            if (!enumNames.Contains(enumName))
            {
                enumNames.Add(enumName);
            }
        }

        if (!EditorUtility.DisplayDialog("RewardCurrencyType 재생성", $"다음 enum 파일을 재생성합니다.\n{enumPath}\n\nUnity 컴파일 후 다시 Import를 실행하세요.", "재생성", "취소"))
        {
            return;
        }

        File.WriteAllText(enumPath, BuildRewardCurrencyTypeSource(enumNames), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        AddMessage("RewardCurrencyType.cs를 재생성했습니다. 컴파일 완료 후 CSV 임포트를 다시 실행하세요.");
        FlushMessagesToConsole(true);
    }

    // CSV 파일을 읽고 모든 행을 검증한다
    private bool TryReadAndValidateCsv(out List<ItemCsvRow> rows)
    {
        rows = new List<ItemCsvRow>();
        if (!File.Exists(csvFilePath))
        {
            AddMessage("CSV 파일을 찾을 수 없습니다: " + csvFilePath);
            return false;
        }

        List<List<string>> table = ParseCsv(File.ReadAllText(csvFilePath, Encoding.UTF8));
        if (table.Count <= 1)
        {
            AddMessage("CSV에 데이터 행이 없습니다.");
            return false;
        }

        if (!TryBuildHeaderMap(table[0], out Dictionary<string, int> headerMap))
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
    private bool TryBuildHeaderMap(List<string> headers, out Dictionary<string, int> headerMap)
    {
        headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            string header = (headers[i] ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(header) && !headerMap.ContainsKey(header))
            {
                headerMap.Add(header, i);
            }
        }

        string[] requiredColumns = { TYPE_COLUMN, NAME_COLUMN, INFO_TEXT_COLUMN, IMAGE_PATH_COLUMN, CRAFT_COLUMN };
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

    // CSV 한 행을 아이템 행 데이터로 변환한다
    private bool TryParseRow(List<string> fields, Dictionary<string, int> headerMap, int lineNumber, List<ItemCsvRow> rows, List<string> errors)
    {
        string typeText = GetField(fields, headerMap, TYPE_COLUMN).Trim();
        string itemName = GetField(fields, headerMap, NAME_COLUMN);
        string infoText = GetField(fields, headerMap, INFO_TEXT_COLUMN);
        string imagePath = GetField(fields, headerMap, IMAGE_PATH_COLUMN).Trim();
        string craftText = GetField(fields, headerMap, CRAFT_COLUMN).Trim();

        if (string.IsNullOrEmpty(typeText))
        {
            errors.Add($"{lineNumber}행: Type이 비어 있습니다.");
            return false;
        }

        if (!IsValidIdentifier(typeText))
        {
            errors.Add($"{lineNumber}행: Type이 C# enum 이름으로 유효하지 않습니다. 값: {typeText}");
            return false;
        }

        if (!Enum.TryParse(typeText, out RewardCurrencyType itemType))
        {
            AddMissingEnumName(typeText);
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

        if (!TryParseCraftItems(craftText, lineNumber, out List<ItemMaterialData> craftItems, errors))
        {
            return false;
        }

        rows.Add(new ItemCsvRow
        {
            Type = itemType,
            Name = itemName,
            InfoText = infoText,
            ItemImage = sprite,
            ItemsToCreate = craftItems
        });
        return true;
    }

    // 제작 재료 문자열을 ItemMaterialData 목록으로 변환한다
    private bool TryParseCraftItems(string craftText, int lineNumber, out List<ItemMaterialData> craftItems, List<string> errors)
    {
        craftItems = new List<ItemMaterialData>();
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
            if (!IsValidIdentifier(typeText))
            {
                errors.Add($"{lineNumber}행: 제작 재료 Type이 C# enum 이름으로 유효하지 않습니다. 값: {typeText}");
                return false;
            }

            if (!Enum.TryParse(typeText, out RewardCurrencyType materialType))
            {
                AddMissingEnumName(typeText);
                return false;
            }

            if (!int.TryParse(parts[1].Trim(), out int count) || count < 0)
            {
                errors.Add($"{lineNumber}행: 제작 재료 Count가 유효하지 않습니다. 값: {parts[1]}");
                return false;
            }

            craftItems.Add(new ItemMaterialData { Type = materialType, Count = count });
        }

        return true;
    }

    // 메인 리스트 SO를 가져오거나 필요한 경우 생성한다
    private bool TryEnsureMainListSo(out ItemMetaDataListSo targetListSo)
    {
        targetListSo = mainListSo;
        if (targetListSo != null)
        {
            return true;
        }

        if (!createMainListIfMissing)
        {
            AddMessage("메인 리스트 SO가 할당되지 않았습니다.");
            return false;
        }

        string existingPath = mainListSoPath;
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

    // CSV 텍스트를 행과 필드 목록으로 파싱한다
    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool inQuotes = false;

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
            else if (c == ',' && !inQuotes)
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

    // CSV 필드 값을 안전하게 반환한다
    private static string GetField(List<string> fields, Dictionary<string, int> headerMap, string columnName)
    {
        int index = headerMap[columnName];
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
    private static string FormatCraftItems(List<ItemMaterialData> craftItems)
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

            ItemMaterialData item = craftItems[i];
            builder.Append(item.Type);
            builder.Append(':');
            builder.Append(Mathf.Max(0, item.Count));
        }

        return builder.ToString();
    }

    // 누락된 enum 이름을 중복 없이 기록한다
    private void AddMissingEnumName(string enumName)
    {
        if (!missingEnumNames.Contains(enumName))
        {
            missingEnumNames.Add(enumName);
        }
    }

    // 현재 RewardCurrencyType enum 이름 목록을 반환한다
    private static List<string> GetCurrentRewardCurrencyNames()
    {
        Array values = Enum.GetValues(typeof(RewardCurrencyType));
        List<string> names = new List<string>(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            names.Add(values.GetValue(i).ToString());
        }

        return names;
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
    private static string BuildRewardCurrencyTypeSource(List<string> enumNames)
    {
        StringBuilder builder = new StringBuilder(256);
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// 보상과 비용 계산에 공통으로 사용하는 재화 종류.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("public enum RewardCurrencyType");
        builder.AppendLine("{");
        for (int i = 0; i < enumNames.Count; i++)
        {
            string suffix = i + 1 < enumNames.Count ? "," : string.Empty;
            builder.AppendLine($"    {enumNames[i]}{suffix}");
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

    private struct ItemCsvRow
    {
        public RewardCurrencyType Type;
        public string Name;
        public string InfoText;
        public Sprite ItemImage;
        public List<ItemMaterialData> ItemsToCreate;
    }
}
#endif
