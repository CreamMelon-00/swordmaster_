using System;
using System.IO;
using System.Text;
using TurnLimbo.Runtime.Save;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>The single auto-save file. Writes go to a temporary file first and then replace the save, so a
    /// crash mid-write keeps the previous save intact. Format and rules belong to Runtime.</summary>
    public sealed class GameSaveStore
    {
        public const string FileName = "save.txt";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public GameSaveStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save path is required.", nameof(path));
            Path = path;
        }

        /// <summary>The save next to the player's other data, e.g. %USERPROFILE%/AppData/LocalLow/&lt;company&gt;/&lt;product&gt;/save.txt.</summary>
        public static string DefaultPath => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public string Path { get; }
        private string TemporaryPath => Path + ".tmp";
        private string BackupPath => Path + ".bak";
        /// <summary>Whether any copy of the save exists, including one left by an interrupted write.</summary>
        public bool Exists => File.Exists(Path) || File.Exists(TemporaryPath) || File.Exists(BackupPath);

        /// <summary>Reads and parses the save. Game rules are not checked here; use <see cref="GameSave.Validate"/>.
        /// When the save itself is missing after an interrupted write, the newest readable leftover is used instead.</summary>
        public bool TryLoad(out GameSave save, out string error)
        {
            save = null;
            if (File.Exists(Path)) return TryLoadFile(Path, out save, out error);
            error = "저장 파일이 없습니다.";
            foreach (string leftover in new[] { TemporaryPath, BackupPath })
            {
                if (!File.Exists(leftover)) continue;
                if (TryLoadFile(leftover, out save, out string leftoverError))
                {
                    error = null;
                    return true;
                }
                error = leftoverError;
            }
            return false;
        }

        private static bool TryLoadFile(string path, out GameSave save, out string error)
        {
            save = null;
            string text;
            try
            {
                text = File.ReadAllText(path, Utf8);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"저장 파일을 읽지 못했습니다: {exception.Message}";
                return false;
            }
            return GameSaveCodec.TryParse(text, out save, out error);
        }

        public bool TrySave(GameSave save, out string error)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            try
            {
                string directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(TemporaryPath, GameSaveCodec.Serialize(save), Utf8);
                if (File.Exists(Path))
                {
                    // The backup keeps the previous save if the replace fails half way; TryLoad can read either copy.
                    File.Replace(TemporaryPath, Path, BackupPath);
                    TryDelete(BackupPath);
                }
                else File.Move(TemporaryPath, Path);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"저장하지 못했습니다: {exception.Message}";
                return false;
            }
        }

        /// <summary>Removes the save and any copy left by an interrupted write. Returns whether a save existed.</summary>
        public bool Delete()
        {
            bool existed = Exists;
            try
            {
                foreach (string path in new[] { Path, TemporaryPath, BackupPath })
                    if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"저장 파일을 지우지 못했습니다: {exception.Message}");
                return false;
            }
            return existed;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // A stale backup only matters when the save itself is missing, and then it is the right copy to read.
            }
        }
    }
}
