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

namespace SprintMod
{
    [BepInPlugin("aedenthorn.SprintMod", "SprintMod", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> toggleSprint;
        public static ConfigEntry<bool> sprinting;
        public static InputAction action;
        public static ConfigEntry<float> sprintSpeedMult;
        //public static ConfigEntry<int> sprintCost;
        public static ConfigEntry<string> sprintKey;

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
            sprintKey = Config.Bind<string>("General", "SprintButton", "<Keyboard>/leftShift", "Use this key to sprint");
            sprintSpeedMult = Config.Bind<float>("General", "SprintSpeedMult", 2f, "Sprint speed multiplier");
            toggleSprint = Config.Bind<bool>("General", "ToggleSprint", false, "Toggle sprint");
            //sprintCost = Config.Bind<int>("General", "sprintCost", 1, "Energy cost per second of sprinting");
            sprinting = Config.Bind<bool>("ZZ_Auto", "sprinting", false, "Is sprinting");

            action = new InputAction(binding: sprintKey.Value);
            action.Enable();

            harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");
        }

        [HarmonyPatch(typeof(ExampleCharacterController), nameof(ExampleCharacterController.UpdateVelocity))]
        static class ExampleCharacterController_UpdateVelocity_Patch
        {
            public static void Prefix()
            {
                if (!modEnabled.Value || !toggleSprint.Value)
                    return;

                if (action.WasPressedThisFrame())
                {
                    sprinting.Value = !sprinting.Value;
                    Dbgl($"Pressed sprint key");
                }
            }
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl($"Transpiling ExampleCharacterController.UpdateVelocity");

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo && ((FieldInfo)codes[i].operand == AccessTools.Field(typeof(ExampleCharacterController), nameof(ExampleCharacterController.MaxStableMoveSpeed)) || (FieldInfo)codes[i].operand == AccessTools.Field(typeof(ExampleCharacterController), nameof(ExampleCharacterController.MaxAirMoveSpeed))))
                    {
                        Dbgl("adding method to check for sprinting");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BepInExPlugin), nameof(SprintCheck))));
                    }
                }

                return codes.AsEnumerable();
            }

        }
        private static float SprintCheck(float movementSpeed)
        {

            if (modEnabled.Value && ((!toggleSprint.Value && action.IsPressed()) || (toggleSprint.Value && sprinting.Value)))
            {
                /*
                if (sprintCost.Value > 0)
                {
                    timePassed += Time.deltaTime;
                    if (timePassed > 1)
                    {
                        FindObjectOfType<playerStats>().gainEnergy(-1, false);
                        timePassed = 0;
                    }
                }
                */
                return movementSpeed * sprintSpeedMult.Value;
            }
            return movementSpeed;
        }
    }
}
