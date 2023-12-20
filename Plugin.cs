using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using System.IO;
using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using VASoundTool;

namespace VA_CustomSounds
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private const string PLUGIN_GUID = "CustomSounds";
        private const string PLUGIN_NAME = "Custom Sounds";
        private const string PLUGIN_VERSION = "1.0.0";

        public static Plugin Instance;
        internal ManualLogSource logger;
        private Harmony harmony;

        public HashSet<string> currentSounds = new HashSet<string>();
        public HashSet<string> oldSounds = new HashSet<string>();
        public HashSet<string> modifiedSounds = new HashSet<string>();
        public Dictionary<string, string> soundHashes = new Dictionary<string, string>();
        public Dictionary<string, string> soundPacks = new Dictionary<string, string>();
        public static bool Initialized { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                logger = BepInEx.Logging.Logger.CreateLogSource(PLUGIN_GUID);
                logger.LogInfo($"Plugin {PLUGIN_GUID} is loaded!");

                harmony = new Harmony(PLUGIN_GUID);
                harmony.PatchAll();

                modifiedSounds = new HashSet<string>();

                string customSoundsPath = GetCustomSoundsFolderPath();
                if (!Directory.Exists(customSoundsPath))
                {
                    logger.LogInfo("\"CustomSounds\" folder not found. Creating it now.");
                    Directory.CreateDirectory(customSoundsPath);
                }
                
                string configPath = Path.Combine(Paths.BepInExConfigPath);

                try
                {
                    var lines = File.ReadAllLines(configPath).ToList();

                    int index = lines.FindIndex(line => line.StartsWith("HideManagerGameObject"));
                    if (index != -1)
                    {
                        logger.LogInfo("\"hideManagerGameObject\" value not correctly set. Fixing it now.");
                        lines[index] = "HideManagerGameObject = true";
                    }

                    File.WriteAllLines(configPath, lines);
                }
                catch (Exception e)
                {
                    logger.LogError($"Error modifying config file: {e.Message}");
                }

                var types = Assembly.GetExecutingAssembly().GetTypes();
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                        if (attributes.Length > 0)
                        {
                            method.Invoke(null, null);
                        }
                    }
                }
            }
        }


        internal void Start() => Initialize();

        internal void OnDestroy() => Initialize();

        internal void Initialize()
        {
            if (Initialized)
                return;
            Initialized = true;
            GameObject obj = new GameObject("CustomSounds");
            obj.AddComponent<Page>();
            DontDestroyOnLoad(obj);
            ReloadSounds();
        }

        public string GetCustomSoundsFolderPath()
        {
            return Path.Combine(Path.GetDirectoryName(Plugin.Instance.Info.Location), "CustomSounds");
        }

        public string GetCustomSoundsTempFolderPath()
        {
            return Path.Combine(GetCustomSoundsFolderPath(), "Temp");
        }

        public static byte[] SerializeWavToBytes(string filePath)
        {
            try
            {
                return File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return null;
            }
        }

        public void RevertSounds()
        {
            foreach (string soundName in currentSounds)
            {
                logger.LogInfo($"{soundName} restored.");
                SoundTool.RestoreAudioClip(soundName);
            }

            logger.LogInfo("Original game sounds restored.");
        }
        public static string CalculateMD5(string filename)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public void ReloadSounds()
        {
            foreach (string soundName in currentSounds)
            {
                SoundTool.RestoreAudioClip(soundName);
            }

            oldSounds = new HashSet<string>(currentSounds);
            modifiedSounds.Clear();

            string pluginPath = Path.GetDirectoryName(Paths.PluginPath);
            currentSounds.Clear();


            string customSoundsPath = GetCustomSoundsTempFolderPath();

            logger.LogInfo($"Temporary folder: {customSoundsPath}");
            if (Directory.Exists(customSoundsPath))
            {
                ProcessDirectory(customSoundsPath);
            }

            ProcessDirectory(pluginPath);
        }

        private void ProcessDirectory(string directoryPath)
        {
            foreach (var subDirectory in Directory.GetDirectories(directoryPath, "CustomSounds", SearchOption.AllDirectories))
            {
                string packName = Path.GetFileName(Path.GetDirectoryName(subDirectory));
                ProcessSoundFiles(subDirectory, packName);

                foreach (var subDirectory2 in Directory.GetDirectories(subDirectory))
                {
                    string packName2 = Path.GetFileName(subDirectory2);
                    ProcessSoundFiles(subDirectory2, packName2);
                }
            }
        }

        private void ProcessSoundFiles(string directoryPath, string packName)
        {
            foreach (string file in Directory.GetFiles(directoryPath, "*.wav"))
            {
                string soundName = Path.GetFileNameWithoutExtension(file);
                string newHash = CalculateMD5(file);

                if (soundHashes.TryGetValue(soundName, out var oldHash) && oldHash != newHash)
                {
                    modifiedSounds.Add(soundName);
                }

                AudioClip customSound = SoundTool.GetAudioClip(directoryPath, "", file);
                SoundTool.ReplaceAudioClip(soundName, customSound);

                soundHashes[soundName] = newHash;
                currentSounds.Add(soundName);
                soundPacks[soundName] = packName;
                logger.LogInfo($"[{packName}] {soundName} sound replaced!");
            }
        }

        public string GetSoundChanges()
        {
            StringBuilder sb = new StringBuilder("Customsounds reloaded.\n\n");

            var newSoundsSet = new HashSet<string>(currentSounds.Except(oldSounds));
            var deletedSoundsSet = new HashSet<string>(oldSounds.Except(currentSounds));
            var existingSoundsSet = new HashSet<string>(oldSounds.Intersect(currentSounds).Except(modifiedSounds));
            var modifiedSoundsSet = new HashSet<string>(modifiedSounds);

            var soundsByPack = new Dictionary<string, List<string>>();

            Action<HashSet<string>, string> addSoundsToPack = (soundsSet, status) =>
            {
                foreach (var sound in soundsSet)
                {
                    string packName = soundPacks[sound];
                    if (!soundsByPack.ContainsKey(packName))
                    {
                        soundsByPack[packName] = new List<string>();
                    }
                    soundsByPack[packName].Add($"{sound} ({status})");
                }
            };

            addSoundsToPack(newSoundsSet, "New");
            addSoundsToPack(deletedSoundsSet, "Deleted");
            addSoundsToPack(modifiedSoundsSet, "Modified");
            addSoundsToPack(existingSoundsSet, "Already Existed");

            foreach (var pack in soundsByPack.Keys)
            {
                sb.AppendLine($"{pack} :");
                foreach (var sound in soundsByPack[pack])
                {
                    sb.AppendLine($"- {sound}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public string ListAllSounds()
        {
            StringBuilder sb = new StringBuilder("Listing all currently loaded custom sounds:\n\n");

            var soundsByPack = new Dictionary<string, List<string>>();

            foreach (var sound in currentSounds)
            {
                string packName = soundPacks[sound];
                if (!soundsByPack.ContainsKey(packName))
                {
                    soundsByPack[packName] = new List<string>();
                }
                soundsByPack[packName].Add(sound);
            }

            foreach (var pack in soundsByPack.Keys)
            {
                sb.AppendLine($"{pack} :");
                foreach (var sound in soundsByPack[pack])
                {
                    sb.AppendLine($"- {sound}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    /*[HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
    public static class TerminalParsePlayerSentencePatch
    {
        public static bool Prefix(Terminal __instance, ref TerminalNode __result)
        {
            string[] inputLines = __instance.screenText.text.Split('\n');
            if (inputLines.Length == 0)
            {
                return true;
            }

            string[] commandWords = inputLines.Last().Trim().ToLower().Split(' ');
            if (commandWords.Length == 0 || commandWords[0] != "customsounds")
            {
                return true;
            }

            Plugin.Instance.logger.LogInfo($"Received terminal command: {string.Join(" ", commandWords)}");

            if (commandWords.Length > 1 && commandWords[0] == "customsounds")
            {
                switch (commandWords[1])
                {
                    case "reload":
                        Plugin.Instance.ReloadSounds(false, false);
                        __result = CreateTerminalNode(Plugin.Instance.GetSoundChanges());
                        return false;

                    case "revert":
                        Plugin.Instance.RevertSounds();
                        __result = CreateTerminalNode("Game sounds reverted to original.");
                        return false;

                    case "list":
                        __result = CreateTerminalNode(Plugin.Instance.ListAllSounds());
                        return false;
                }
            }

            return true;
        }
    }*/
}