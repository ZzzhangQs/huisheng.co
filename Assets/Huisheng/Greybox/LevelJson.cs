using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Huisheng.Greybox
{
    [Serializable]
    public sealed class LevelDocument
    {
        public int Version;
        public SeedLayout Layout;
        public DemoSettings Settings;

        public LevelDocument(SeedLayout layout, DemoSettings settings)
        {
            Version = 1;
            Layout = layout;
            Settings = settings;
        }
    }

    public static class LevelJson
    {
        public static string SavePath => Path.Combine(Application.persistentDataPath, "HuishengGreyboxLevel.json");

        public static bool TrySave(LevelDocument level, out string error)
        {
            if (!Validate(level, out error)) return false;
            string path = SavePath;
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, JsonUtility.ToJson(level, true), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Debug.LogWarning(ex.Message);
                error = "保存失败，请检查存档目录权限和可用空间。";
                return false;
            }
            finally
            {
                try { File.Delete(temporary); }
                catch (Exception ex) when (IsFileError(ex)) { }
            }
        }

        public static bool TryLoad(out LevelDocument level, out string error)
        {
            level = null;
            try
            {
                string json = File.ReadAllText(SavePath, Encoding.UTF8);
                return TryParse(json, out level, out error);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                Debug.LogWarning(ex.Message);
                error = ex is FileNotFoundException or DirectoryNotFoundException ? "还没有保存的谱面。" : "读取失败，请检查存档文件和目录权限。";
                return false;
            }
        }

        public static bool TryParse(string json, out LevelDocument level, out string error)
        {
            level = null;
            try
            {
                var candidate = JsonUtility.FromJson<LevelDocument>(json);
                if (!Validate(candidate, out error)) return false;
                level = candidate;
                return true;
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning(ex.Message);
                error = "存档的 JSON 格式无效。";
                return false;
            }
        }

        static bool Validate(LevelDocument level, out string error)
        {
            if (level == null || level.Version != 1)
            {
                error = "存档缺少版本号，或版本不受支持。";
                return false;
            }

            return LevelChartGenerator.TryGenerate(level.Layout, level.Settings, out _, out error);
        }

        static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException or NotSupportedException;
    }
}
