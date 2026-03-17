using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class AssetPlacementExporter : MonoBehaviour
{
    [Serializable]
    public class AssetNameObjectNamePair
    {
        [Tooltip("에셋 이름 (an)")]
        public string assetName;

        [Tooltip("오브젝트 이름 prefix (on)")]
        public string objectNamePrefix;
    }

    [Serializable]
    private struct PlacementEntry
    {
        public string assetName;
        public string objectName;
        public Vector3 worldPos;
        public Quaternion worldRot;
    }

    [Header("(an, on) 목록")]
    public List<AssetNameObjectNamePair> pairs = new List<AssetNameObjectNamePair>();

    [Header("출력 결과 문자열")]
    [TextArea(12, 40)]
    public string s;

    [Header("TXT 저장 옵션")]
    [Tooltip("프로젝트 루트 기준 폴더명. 예: PlacementExports")]
    public string outputFolderRelativeToProject = "PlacementExports";

    [Tooltip("저장할 txt 파일명")]
    public string outputFileName = "placement_export.txt";

    [ContextMenu("Build Placement String And Save Txt")]
    public void BuildPlacementStringAndSaveTxt()
    {
        List<PlacementEntry> entries = CollectEntries();
        Dictionary<string, int> assetCounts = BuildAssetCounts(entries);

        s = BuildOutputString(entries, assetCounts);

        string savedPath = SaveToTxt(s);

        Debug.Log(
            $"[AssetPlacementExporter] Export complete\n" +
            $"Saved Path: {savedPath}\n\n{s}",
            this
        );
    }

    private List<PlacementEntry> CollectEntries()
    {
        var results = new List<PlacementEntry>();

        // owner의 직계 자식만 본다.
        foreach (Transform child in transform)
        {
            if (child == null) continue;

            AssetNameObjectNamePair matchedPair = FindFirstMatchingPair(child.name);
            if (matchedPair == null) continue;

            PlacementEntry entry = new PlacementEntry
            {
                assetName = matchedPair.assetName,
                objectName = child.name,
                worldPos = child.position,
                worldRot = child.rotation
            };

            results.Add(entry);
        }

        return results;
    }

    private Dictionary<string, int> BuildAssetCounts(List<PlacementEntry> entries)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.assetName))
                continue;

            if (!dict.ContainsKey(entry.assetName))
                dict[entry.assetName] = 0;

            dict[entry.assetName]++;
        }

        return dict;
    }

    private string BuildOutputString(List<PlacementEntry> entries, Dictionary<string, int> assetCounts)
    {
        var sb = new StringBuilder(4096);

        sb.AppendLine("[PLACEMENT_EXPORT_BEGIN]");
        sb.AppendLine($"owner=\"{Escape(name)}\"");
        sb.AppendLine("position_format=\"world_xyz\"");
        sb.AppendLine("rotation_format=\"quaternion_xyzw_world\"");
        sb.AppendLine("count_section_format=\"ASSET_COUNT|asset=...|count=n\"");
        sb.AppendLine("entry_section_format=\"ENTRY|asset=...|object=...|pos=(x,y,z)|rot=(x,y,z,w)\"");

        sb.AppendLine();
        sb.AppendLine("[ASSET_COUNTS_BEGIN]");

        foreach (var kv in assetCounts)
        {
            sb.Append("ASSET_COUNT");
            sb.Append("|asset=\"").Append(Escape(kv.Key)).Append("\"");
            sb.Append("|count=").Append(kv.Value);
            sb.AppendLine();
        }

        sb.AppendLine($"asset_type_count={assetCounts.Count}");
        sb.AppendLine("[ASSET_COUNTS_END]");

        sb.AppendLine();
        sb.AppendLine("[PLACEMENT_ENTRIES_BEGIN]");

        foreach (var entry in entries)
        {
            sb.Append("ENTRY");
            sb.Append("|asset=\"").Append(Escape(entry.assetName)).Append("\"");
            sb.Append("|object=\"").Append(Escape(entry.objectName)).Append("\"");
            sb.Append("|pos=(")
              .Append(F(entry.worldPos.x)).Append(",")
              .Append(F(entry.worldPos.y)).Append(",")
              .Append(F(entry.worldPos.z)).Append(")");
            sb.Append("|rot=(")
              .Append(F(entry.worldRot.x)).Append(",")
              .Append(F(entry.worldRot.y)).Append(",")
              .Append(F(entry.worldRot.z)).Append(",")
              .Append(F(entry.worldRot.w)).Append(")");
            sb.AppendLine();
        }

        sb.AppendLine($"entry_count={entries.Count}");
        sb.AppendLine("[PLACEMENT_ENTRIES_END]");
        sb.AppendLine("[PLACEMENT_EXPORT_END]");

        return sb.ToString();
    }

    private string SaveToTxt(string text)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        string folderName = string.IsNullOrWhiteSpace(outputFolderRelativeToProject)
            ? "PlacementExports"
            : outputFolderRelativeToProject.Trim();

        string fileName = string.IsNullOrWhiteSpace(outputFileName)
            ? "placement_export.txt"
            : outputFileName.Trim();

        if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            fileName += ".txt";

        string directoryPath = Path.Combine(projectRoot, folderName);
        Directory.CreateDirectory(directoryPath);

        string fullPath = Path.Combine(directoryPath, fileName);
        File.WriteAllText(fullPath, text, new UTF8Encoding(false));

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif

        return fullPath;
    }

    private AssetNameObjectNamePair FindFirstMatchingPair(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        for (int i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            if (pair == null) continue;
            if (string.IsNullOrEmpty(pair.assetName)) continue;
            if (string.IsNullOrEmpty(pair.objectNamePrefix)) continue;

            // prefix match
            if (objectName.StartsWith(pair.objectNamePrefix, StringComparison.Ordinal))
                return pair;
        }

        return null;
    }

    private static string F(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}