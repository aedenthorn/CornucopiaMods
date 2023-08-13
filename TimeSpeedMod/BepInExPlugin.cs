using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KinematicCharacterController.Examples;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimeSpeedMod
{
    [BepInPlugin("aedenthorn.TimeSpeedMod", "TimeSpeedMod", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> timePaused;
        public static ConfigEntry<float> currentSpeedMult;
        public static ConfigEntry<string> speedUpKey;
        public static ConfigEntry<string> slowDownKey;
        public static ConfigEntry<string> speedResetKey;
        public static ConfigEntry<string> speedPauseToggleKey;
        public static InputAction actionUp;
        public static InputAction actionDown;
        public static InputAction actionReset;
        public static InputAction actionPause;

        public static float timePassed;

        private Harmony harmony;

        public static void Dbgl(string str = "", bool pref = true)
        {
            Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        private void Awake()
        {

            context = this; 
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            speedUpKey = Config.Bind<string>("General", "SpeedUpKey", "<Keyboard>/equals", "Use this key to speed up time");
            slowDownKey = Config.Bind<string>("General", "SlowDownKey", "<Keyboard>/minus", "Use this key to slow down time");
            speedResetKey = Config.Bind<string>("General", "SpeedResetKey", "<Keyboard>/backspace", "Use this key to reset time speed");
            speedPauseToggleKey = Config.Bind<string>("General", "SpeedPauseToggleKey", "<Keyboard>/pause", "Use this key to toggle time pause");
            currentSpeedMult = Config.Bind<float>("ZZ_Auto", "CurrentSpeedMult", 1, "Current speed multiplier");
            timePaused = Config.Bind<bool>("ZZ_Auto", "TimePaused", false, "Time currently paused");

            actionUp = new InputAction(binding: speedUpKey.Value);
            actionDown = new InputAction(binding: slowDownKey.Value);
            actionReset = new InputAction(binding: speedResetKey.Value);
            actionPause = new InputAction(binding: speedPauseToggleKey.Value);
            actionUp.Enable();
            actionDown.Enable();
            actionReset.Enable();
            actionPause.Enable();

            harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");
            return;
        }


        [HarmonyPatch(typeof(Inventory), "Update")]
        static class Inventory_Update_Patch
        {
            public static void Prefix(Inventory __instance)
            {
                if (!modEnabled.Value)
                    return;
                if (actionUp.WasPressedThisFrame())
                {
                    if (currentSpeedMult.Value < 10)
                    {
                        if(currentSpeedMult.Value >= 2)
                        {
                            currentSpeedMult.Value = Mathf.FloorToInt(currentSpeedMult.Value + 1);
                        }
                        else
                        {
                            currentSpeedMult.Value = (float)Math.Round(currentSpeedMult.Value + 0.1f, 1);
                        }
                    }
                }
                else if (actionDown.WasPressedThisFrame())
                {
                    if (currentSpeedMult.Value > 0)
                    {
                        if(currentSpeedMult.Value >= 3)
                        {
                            currentSpeedMult.Value = Mathf.FloorToInt(currentSpeedMult.Value - 1);
                        }
                        else
                        {
                            currentSpeedMult.Value = (float)Math.Round(currentSpeedMult.Value - 0.1f, 1);
                        }
                    }
                }
                else if (actionPause.WasPressedThisFrame())
                {
                    timePaused.Value = !timePaused.Value;
                }
                else if (actionReset.WasPressedThisFrame())
                {
                    currentSpeedMult.Value = 1;
                }
                else
                    return;
                __instance.addPopUpToList("Player Face", 1, true, $"Time speed: {(timePaused.Value ? "Paused" : currentSpeedMult.Value + "x")}", true, 215, false);
            }
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl($"Transpiling Inventory.Update");

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(Inventory), "rawTimeSpeedMultiplier"))
                    {
                        Dbgl("adding method to adjust time speed");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BepInExPlugin), nameof(AdjustTimeSpeed))));
                    }
                }

                return codes.AsEnumerable();
            }

        }
        private static float AdjustTimeSpeed(float rawTimeSpeedMultiplier)
        {

            if (!modEnabled.Value)
                return rawTimeSpeedMultiplier;
            return timePaused.Value ? 0 : rawTimeSpeedMultiplier * currentSpeedMult.Value;
        }
    }
}
