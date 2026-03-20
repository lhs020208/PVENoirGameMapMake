using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class PlacementExportMerger : EditorWindow
{
    [Serializable]
    private struct PlacementEntry
    {
        public string assetName;
        public string objectName;
        public Vector3 worldPos;
        public Quaternion worldRot;
    }

    private string inputFolderRelativeToProject = "PlacementExports";
    private string outputFileName = "placement_export_merged.txt";
    private bool includeSubdirectories = false;
    private bool excludeMergedOutputFile = true;
    private bool deduplicateExactEntries = false;

    [MenuItem("Tools/Placement Export/Merge TXT Files")]
    public static void OpenWindow()
    {
        GetWindow<PlacementExportMerger>("Placement Export Merger");
    }

    private void OnGUI()
    {
        GUILayout.Label("Placement Export Merge", EditorStyles.boldLabel);

        inputFolderRelativeToProject = EditorGUILayout.TextField(
            "Input Folder",
            inputFolderRelativeToProject
        );

        outputFileName = EditorGUILayout.TextField(
            "Output File Name",
            outputFileName
        );

        includeSubdirectories = EditorGUILayout.Toggle(
            "Include Subdirectories",
            includeSubdirectories
        );

        excludeMergedOutputFile = EditorGUILayout.Toggle(
            "Exclude Output File From Input",
            excludeMergedOutputFile
        );

        deduplicateExactEntries = EditorGUILayout.Toggle(
            "Deduplicate Exact Entries",
            deduplicateExactEntries
        );

        GUILayout.Space(10);

        if (GUILayout.Button("Merge Now", GUILayout.Height(32)))
        {
            MergeNow();
        }
    }

    private void MergeNow()
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string inputFolder = Path.Combine(projectRoot, inputFolderRelativeToProject);

            if (!Directory.Exists(inputFolder))
            {
                Debug.LogError($"[PlacementExportMerger] 폴더가 없음: {inputFolder}");
                return;
            }

            if (string.IsNullOrWhiteSpace(outputFileName))
                outputFileName = "placement_export_merged.txt";

            if (!outputFileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                outputFileName += ".txt";

            SearchOption searchOption = includeSubdirectories
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            string[] allTxtFiles = Directory.GetFiles(inputFolder, "*.txt", searchOption);

            string outputFullPath = Path.Combine(inputFolder, outputFileName);

            var inputFiles = new List<string>();
            foreach (string file in allTxtFiles)
            {
                if (excludeMergedOutputFile &&
                    string.Equals(
                        Path.GetFullPath(file),
                        Path.GetFullPath(outputFullPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                inputFiles.Add(file);
            }

            if (inputFiles.Count == 0)
            {
                Debug.LogWarning("[PlacementExportMerger] 병합할 txt 파일이 없음");
                return;
            }

            List<PlacementEntry> mergedEntries = new List<PlacementEntry>();

            foreach (string file in inputFiles)
            {
                string text = File.ReadAllText(file, Encoding.UTF8);
                List<PlacementEntry> entries = ParseEntries(text);
                mergedEntries.AddRange(entries);
            }

            if (deduplicateExactEntries)
            {
                mergedEntries = DeduplicateEntries(mergedEntries);
            }

            Dictionary<string, int> assetCounts = BuildAssetCounts(mergedEntries);
            string mergedText = BuildOutputString(
                ownerName: "MERGED",
                entries: mergedEntries,
                assetCounts: assetCounts
            );

            File.WriteAllText(outputFullPath, mergedText, new UTF8Encoding(false));
            AssetDatabase.Refresh();

            Debug.Log(
                $"[PlacementExportMerger] 병합 완료\n" +
                $"입력 파일 수: {inputFiles.Count}\n" +
                $"엔트리 수: {mergedEntries.Count}\n" +
                $"저장 경로: {outputFullPath}"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlacementExportMerger] 실패\n{ex}");
        }
    }

    private static List<PlacementEntry> ParseEntries(string text)
    {
        var results = new List<PlacementEntry>();
        if (string.IsNullOrEmpty(text)) return results;

        string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("ENTRY|", StringComparison.Ordinal))
                continue;

            if (TryParseEntry(line, out PlacementEntry entry))
            {
                results.Add(entry);
            }
        }

        return results;
    }

    private static bool TryParseEntry(string line, out PlacementEntry entry)
    {
        entry = default;

        try
        {
            string asset = ExtractQuotedValue(line, "asset=\"");
            string obj = ExtractQuotedValue(line, "object=\"");
            Vector3 pos = ExtractVector3(line, "|pos=(");
            Quaternion rot = ExtractQuaternion(line, "|rot=(");

            entry = new PlacementEntry
            {
                assetName = asset,
                objectName = obj,
                worldPos = pos,
                worldRot = rot
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractQuotedValue(string line, string key)
    {
        int start = line.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            throw new FormatException($"키를 찾지 못함: {key}");

        start += key.Length;
        int end = line.IndexOf('"', start);
        if (end < 0)
            throw new FormatException($"닫는 따옴표를 찾지 못함: {key}");

        string value = line.Substring(start, end - start);
        return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private static Vector3 ExtractVector3(string line, string key)
    {
        int start = line.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            throw new FormatException($"키를 찾지 못함: {key}");

        start += key.Length;
        int end = line.IndexOf(')', start);
        if (end < 0)
            throw new FormatException($"닫는 괄호를 찾지 못함: {key}");

        string content = line.Substring(start, end - start);
        string[] parts = content.Split(',');

        if (parts.Length != 3)
            throw new FormatException($"Vector3 파싱 실패: {content}");

        return new Vector3(
            ParseFloat(parts[0]),
            ParseFloat(parts[1]),
            ParseFloat(parts[2])
        );
    }

    private static Quaternion ExtractQuaternion(string line, string key)
    {
        int start = line.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            throw new FormatException($"키를 찾지 못함: {key}");

        start += key.Length;
        int end = line.IndexOf(')', start);
        if (end < 0)
            throw new FormatException($"닫는 괄호를 찾지 못함: {key}");

        string content = line.Substring(start, end - start);
        string[] parts = content.Split(',');

        if (parts.Length != 4)
            throw new FormatException($"Quaternion 파싱 실패: {content}");

        return new Quaternion(
            ParseFloat(parts[0]),
            ParseFloat(parts[1]),
            ParseFloat(parts[2]),
            ParseFloat(parts[3])
        );
    }

    private static float ParseFloat(string s)
    {
        return float.Parse(s.Trim(), CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, int> BuildAssetCounts(List<PlacementEntry> entries)
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

    private static List<PlacementEntry> DeduplicateEntries(List<PlacementEntry> entries)
    {
        var results = new List<PlacementEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var e in entries)
        {
            string key =
                $"{e.assetName}|{e.objectName}|" +
                $"{F(e.worldPos.x)},{F(e.worldPos.y)},{F(e.worldPos.z)}|" +
                $"{F(e.worldRot.x)},{F(e.worldRot.y)},{F(e.worldRot.z)},{F(e.worldRot.w)}";

            if (seen.Add(key))
                results.Add(e);
        }

        return results;
    }

    private static string BuildOutputString(
        string ownerName,
        List<PlacementEntry> entries,
        Dictionary<string, int> assetCounts)
    {
        var sb = new StringBuilder(4096);

        sb.AppendLine("[PLACEMENT_EXPORT_BEGIN]");
        sb.AppendLine($"owner=\"{Escape(ownerName)}\"");
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