using System.IO;
using UnityEngine;

namespace PoingMort.Core
{
    /// <summary>
    /// Local save location. The prototype P01 does not write saves yet (planned for P02),
    /// so "Continuer" stays disabled until a save file exists.
    /// </summary>
    public static class SaveSystem
    {
        public const string FileName = "sauvegarde.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave => File.Exists(SavePath);
    }
}
