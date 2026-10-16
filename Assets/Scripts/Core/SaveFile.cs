using System;
using System.IO;
using UnityEngine;

namespace FallingWizard.Core
{
    public enum SaveRead
    {
        Missing,
        Loaded,
        Unreadable,
    }

    public static class SaveFile
    {
        const string FolderName = "Saves";

        const string TemporaryExtension = ".tmp";
        const string BackupExtension = ".bak";
        const string CorruptExtension = ".corrupt";
        const string ProbeName = "write.probe";

        static string folder;
        static bool hasAnnounced;

        static string Folder
        {
            get
            {
                if (folder == null)
                    folder = ChooseFolder();

                return folder;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            folder = null;
            hasAnnounced = false;
        }

        public static string PathFor(string fileName) => Path.Combine(Folder, fileName);

        public static void Announce()
        {
            if (hasAnnounced)
                return;

            hasAnnounced = true;
            Debug.Log($"Falling Wizard saves live in {Folder}");
        }

        public static bool Exists(string fileName)
        {
            try
            {
                return File.Exists(PathFor(fileName));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not look for the save file '{fileName}' in {Folder}, so " +
                                 $"the game will act as though there is no save there. {error.Message}");
                return false;
            }
        }

        public static SaveRead Read<T>(string fileName, out T data) where T : class
        {
            data = null;

            string path = PathFor(fileName);
            SaveRead fileRead = ReadText(path, out string text);

            if (fileRead != SaveRead.Loaded)
                return fileRead;

            if (string.IsNullOrWhiteSpace(text))
                return SaveRead.Missing;

            data = ParseJson<T>(path, text);

            if (data != null)
                return SaveRead.Loaded;

            Quarantine(path);
            return SaveRead.Missing;
        }

        public static bool Write<T>(string fileName, T data) where T : class
        {
            if (data == null)
            {
                Debug.LogWarning($"Refusing to write nothing to '{fileName}'. Something asked to " +
                                 "save before it had anything to save, which would have emptied the file.");
                return false;
            }

            string path = PathFor(fileName);
            string temporaryPath = path + TemporaryExtension;

            try
            {
                WriteThroughTemporaryFile(data, temporaryPath, path);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not save to {path}, so anything earned this session will " +
                                 $"be gone when the game closes. {error.Message}");
                TryDelete(temporaryPath);
                return false;
            }
        }

        public static bool Delete(string fileName)
        {
            string path = PathFor(fileName);

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not delete the save file at {path}, so the progress it " +
                                 $"holds will come back on the next launch. {error.Message}");
                return false;
            }
        }

        static SaveRead ReadText(string path, out string text)
        {
            text = null;

            try
            {
                if (!File.Exists(path))
                    return SaveRead.Missing;

                text = File.ReadAllText(path);
                return SaveRead.Loaded;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"The save file at {path} exists but could not be opened, so " +
                                 $"nothing will be written over it this session and the game has " +
                                 $"started from a blank save. Close whatever is holding the file " +
                                 $"and relaunch. {error.Message}");
                return SaveRead.Unreadable;
            }
        }

        static T ParseJson<T>(string path, string text) where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(text);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"The save file at {path} is not readable JSON. {error.Message}");
                return null;
            }
        }

        static void Quarantine(string path)
        {
            string kept = path + CorruptExtension;

            try
            {
                TryDelete(kept);
                File.Move(path, kept);

                Debug.LogWarning($"The save file at {path} could not be understood, so it has been " +
                                 $"set aside as {kept} and the game has started from a blank save. " +
                                 "Open that file if you want to see what went wrong, then delete it.");
            }
            catch (Exception error)
            {
                Debug.LogWarning($"The save file at {path} could not be understood and could not " +
                                 $"be moved aside either. It will be written over. {error.Message}");
            }
        }

        static void WriteThroughTemporaryFile(object data, string temporaryPath, string path)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));

            if (File.Exists(path))
                ReplaceOrMove(temporaryPath, path);
            else
                File.Move(temporaryPath, path);
        }

        static void ReplaceOrMove(string temporaryPath, string path)
        {
            if (TryReplace(temporaryPath, path))
                return;

            MoveWithBackup(temporaryPath, path);
        }

        static bool TryReplace(string temporaryPath, string path)
        {
            try
            {
                File.Replace(temporaryPath, path, null);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static void MoveWithBackup(string temporaryPath, string path)
        {
            string backup = path + BackupExtension;

            TryDelete(backup);
            File.Move(path, backup);

            try
            {
                File.Move(temporaryPath, path);
            }
            catch (Exception)
            {
                File.Move(backup, path);
                throw;
            }

            TryDelete(backup);
        }

        static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        static string ChooseFolder()
        {
#if UNITY_EDITOR
            return GetFolderBesideTheGame();
#else
            if (Application.platform != RuntimePlatform.OSXPlayer)
            {
                string beside = GetFolderBesideTheGame();

                if (IsWritable(beside))
                    return beside;

                Debug.LogWarning($"Cannot write to {beside} - the game is installed somewhere " +
                                 "read-only, which usually means Program Files. Saving to the " +
                                 "operating system's save folder instead.");
            }

            return Path.GetFullPath(Path.Combine(Application.persistentDataPath, FolderName));
#endif
        }

        static string GetFolderBesideTheGame() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName));

        static bool IsWritable(string directory)
        {
            string probe = Path.Combine(directory, ProbeName);

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
