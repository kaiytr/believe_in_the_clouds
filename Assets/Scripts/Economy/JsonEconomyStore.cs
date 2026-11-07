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
            if (data != null && data.schemaVersion >= 2 && data.schemaVersion <= 3)
                data.schemaVersion = EconomyProfileData.CurrentSchemaVersion;
            return EconomyLedger.Validate(data)
                ? new EconomyLoadResult(EconomyLoadStatus.Loaded, data)
                : new EconomyLoadResult(EconomyLoadStatus.Invalid);
        }
        catch (Exception ex) when (IsStorageException(ex) || ex is ArgumentException)
        { return new EconomyLoadResult(EconomyLoadStatus.Invalid); }
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
