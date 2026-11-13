using System;
using System.IO;
using System.Text;
using UnityEngine;

// Recovery must not rotate a corrupt main file over the valid backup.
public sealed class JsonEconomyStore : IEconomyStore
{
    public string FilePath { get; }
    private bool allowWrite;
    public JsonEconomyStore(string path) { FilePath = Path.GetFullPath(path); }

    public EconomyLoadResult Load()
    {
        allowWrite = false;
        var main = Read(FilePath);
        if (main.Status == EconomyLoadStatus.FutureVersion) return main;
        if (main.Status == EconomyLoadStatus.Loaded) { allowWrite = true; return main; }
        var backup = Read(FilePath + ".bak");
        if (backup.Status == EconomyLoadStatus.FutureVersion) return backup;
        if (backup.Status == EconomyLoadStatus.Loaded)
        {
            try
            {
                WriteFlushed(FilePath + ".recover", JsonUtility.ToJson(backup.Data, true));
                if (File.Exists(FilePath)) File.Replace(FilePath + ".recover", FilePath, null);
                else File.Move(FilePath + ".recover", FilePath);
                allowWrite = true;
                return new EconomyLoadResult(EconomyLoadStatus.RecoveredBackup, backup.Data);
            }
            catch (Exception ex) when (IsStorageException(ex)) { return new EconomyLoadResult(EconomyLoadStatus.Invalid); }
        }
        if (main.Status == EconomyLoadStatus.Missing && backup.Status == EconomyLoadStatus.Missing)
        { allowWrite = true; return main; }
        return new EconomyLoadResult(EconomyLoadStatus.Invalid);
    }

    /// <summary>Deletes the save file and its backup/recovery/temp copies (developer reset). True when none remain.</summary>
    public bool DeleteSaveFiles()
    {
        bool clean = true;
        foreach (string path in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp", FilePath + ".recover" })
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) when (IsStorageException(ex)) { clean = false; }
        }
        return clean;
    }

    /// <summary>
    /// Renames the save file and its backup/recovery/temp copies to "&lt;name&gt;.legacy-&lt;label&gt;" so the next load starts a
    /// brand-new profile without destroying the old data. True when no live save file remains.
    /// </summary>
    public bool ArchiveSaveFiles(string label)
    {
        bool clean = true;
        foreach (string path in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp", FilePath + ".recover" })
        {
            try { if (File.Exists(path)) File.Move(path, path + ".legacy-" + label); }
            catch (Exception ex) when (IsStorageException(ex)) { clean = false; }
        }
        return clean;
    }

    public bool TrySave(EconomyProfileData data)
    {
        if (!allowWrite || !EconomyLedger.Validate(data)) return false;
        string temp = FilePath + ".tmp";
        try
        {
            WriteFlushed(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(FilePath))
            {
                if (Read(FilePath).Status != EconomyLoadStatus.Loaded) return false;
                File.Replace(temp, FilePath, FilePath + ".bak");
            }
            else File.Move(temp, FilePath);
            return true;
        }
        catch (Exception ex) when (IsStorageException(ex)) { return false; }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception ex) when (IsStorageException(ex)) { }
        }
    }

    private static EconomyLoadResult Read(string path)
    {
        if (!File.Exists(path)) return new EconomyLoadResult(EconomyLoadStatus.Missing);
        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"schemaVersion\"") ||
                !json.Contains("\"profileId\"") || !json.Contains("\"pieces\"") ||
                !json.Contains("\"pendingGrants\"") || !json.Contains("\"processedTransactions\""))
                return new EconomyLoadResult(EconomyLoadStatus.Invalid);
            var data = JsonUtility.FromJson<EconomyProfileData>(json);
            int sourceVersion = data != null ? data.schemaVersion : 0;
            data?.Normalize();
            if (data != null && data.schemaVersion > EconomyProfileData.CurrentSchemaVersion)
                return new EconomyLoadResult(EconomyLoadStatus.FutureVersion);
            // v1 had only economic assets. The optional run domain starts empty;
            // no initial grant or asset reset is performed during migration.
            if (data != null && data.schemaVersion == 1)
            {
                if (data.hasRun) return new EconomyLoadResult(EconomyLoadStatus.Invalid);
                data.schemaVersion = 2;
            }
            if (data != null && sourceVersion >= 1 && sourceVersion <= 4)
            {
                data.narrative = new NarrativeProgressData { tutorial = TutorialProgress.LegacyEligible };
                data.schemaVersion = EconomyProfileData.CurrentSchemaVersion;
            }
            else if (data != null && sourceVersion == 5 && !HasNarrativeFields(json))
                return new EconomyLoadResult(EconomyLoadStatus.Invalid);
            return EconomyLedger.Validate(data)
                ? new EconomyLoadResult(EconomyLoadStatus.Loaded, data)
                : new EconomyLoadResult(EconomyLoadStatus.Invalid);
        }
        catch (Exception ex) when (IsStorageException(ex) || ex is ArgumentException)
        { return new EconomyLoadResult(EconomyLoadStatus.Invalid); }
    }

    private static bool HasNarrativeFields(string json)
    {
        // Missing fields must not acquire new-profile initializers and replay a completed story.
        // Check at the correct object depth; a death journal also has a field named "phase".
        string narrative = JsonField(json, "narrative");
        string story = JsonField(narrative, "firstAltar");
        return int.TryParse(JsonField(narrative, "version"), out int version) && version == 1 &&
            int.TryParse(JsonField(narrative, "tutorial"), out _) && int.TryParse(JsonField(story, "phase"), out _);
    }

    private static string JsonField(string json, string field)
    {
        if (string.IsNullOrEmpty(json)) return null;
        int depth = 0;
        for (int i = 0; i < json.Length; i++)
        {
            char ch = json[i];
            if (ch == '{' || ch == '[') { depth++; continue; }
            if (ch == '}' || ch == ']') { depth--; continue; }
            if (ch != '"') continue;
            int first = ++i;
            while (i < json.Length && json[i] != '"') { if (json[i] == '\\') i++; i++; }
            if (i >= json.Length) return null;
            string key = json.Substring(first, i - first);
            if (depth != 1 || key != field) continue;
            int value = i + 1;
            while (value < json.Length && char.IsWhiteSpace(json[value])) value++;
            if (value >= json.Length || json[value++] != ':') continue;
            while (value < json.Length && char.IsWhiteSpace(json[value])) value++;
            int end = value, nested = 0;
            bool quoted = false;
            for (; end < json.Length; end++)
            {
                char part = json[end];
                if (quoted) { if (part == '\\') end++; else if (part == '"') quoted = false; continue; }
                if (part == '"') { quoted = true; continue; }
                if (part == '{' || part == '[') nested++;
                else if (part == '}' || part == ']') { if (nested == 0) break; nested--; }
                else if (part == ',' && nested == 0) break;
            }
            return json.Substring(value, end - value).Trim();
        }
        return null;
    }

    private static void WriteFlushed(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        byte[] bytes = new UTF8Encoding(false).GetBytes(json);
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
    }

    private static bool IsStorageException(Exception ex) =>
        ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException || ex is NotSupportedException;
}
