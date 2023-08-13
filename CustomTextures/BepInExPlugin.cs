using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KinematicCharacterController.Examples;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VNEngine;

namespace CustomTextures
{
    [BepInPlugin("aedenthorn.CustomTextures", "CustomTextures", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> dumpTextureNames;
        public static ConfigEntry<string> reloadKey;
        public static InputAction action;
        public static Harmony harmony;
        public static string modFolder;
        public static Dictionary<string, Texture2D> customTextureDict = new Dictionary<string, Texture2D>();
        public static void Dbgl(string str = "", bool pref = true)
        {
            Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        public static string GetAssetPath(object obj, bool create = false)
        {
            return GetAssetPath(obj.GetType().Namespace, create);
        }
        public static string GetAssetPath(string name, bool create = false)
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), name);
            if (create && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        private void Awake()
        {

            context = this;
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            dumpTextureNames = Config.Bind<bool>("General", "DumpTextureNames", false, "Dump texture names to the Player.log file and BepInEx console");
            reloadKey = Config.Bind<string>("General", "ReloadKey", "<Keyboard>/end", "Use this key to reload textures");

            action = new InputAction(binding: reloadKey.Value);
            action.Enable();

            harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);

            modFolder = GetAssetPath(this, true);
            foreach (var f in Directory.GetFiles(modFolder, "*.png", SearchOption.AllDirectories))
            {
                Texture2D tex = new Texture2D(1, 1);
                tex.LoadImage(File.ReadAllBytes(f));
                customTextureDict[Path.GetFileNameWithoutExtension(f).ToLower()] = tex;
            }
            if(dumpTextureNames.Value)
            {
                File.WriteAllText(Path.Combine(modFolder, "dump.txt"), "");
            }

            SceneManager.sceneLoaded += SceneManager_sceneLoaded; 


            Dbgl("Plugin awake");
        }

        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            var rlist = Resources.FindObjectsOfTypeAll<MeshRenderer>();
            foreach(var r in rlist)
            {
                try
                {
                    if (r.materials?.Any() == true) {
                        foreach (var m in r.materials)
                        {
                            if (!m.HasProperty("_MainTex"))
                                continue;
                            var name = r.name + "_" + m.mainTexture.name;
                            File.AppendAllText(Path.Combine(modFolder, "dump.txt"), name + "\r\n");
                            if(customTextureDict.TryGetValue(name.ToLower(), out var tex))
                            {
                                Dbgl($"Replacing {name}");
                                m.mainTexture = tex;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private void Update()
        {
            if (action.WasPressedThisFrame())
            {
                Dbgl("Reloading textures");
                customTextureDict.Clear();
                ((Dictionary<string, Object>)AccessTools.Field(typeof(Spawn), "resourceObjects").GetValue(null)).Clear(); 
            }
        }

        [HarmonyPatch(typeof(Spawn), nameof(Spawn.Load))]
        static class Spawn_Load_Patch
        {
            public static bool Prefix(string path, ref Object __result)
            {
                if (!modEnabled.Value)
                    return true;
                string pathName = path.Replace("/", "_");
                if (dumpTextureNames.Value)
                {
                    File.AppendAllText(Path.Combine(modFolder, "dump.txt"), pathName + "\r\n" );
                }
                if(customTextureDict.TryGetValue(pathName.ToLower(), out var tex))
                {
                    Dbgl($"Replacing {path}");
                    __result = tex;
                    __result.name = pathName;
                    return false;
                }
                return true;
            }
            public static void Postfix(string path, Object __result)
            {
                if (!modEnabled.Value || !dumpTextureNames.Value || !(__result is Texture2D))
                    return;
                string pathName = path.Replace("/", "_");
                File.AppendAllText(Path.Combine(modFolder, "dump.txt"), pathName + "\r\n" );
            }
        }
    }
}
