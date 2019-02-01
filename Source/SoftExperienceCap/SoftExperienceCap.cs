using System;
using System.Reflection;
using Harmony;
using BattleTech;
using BattleTech.UI;
using TMPro;
using Newtonsoft.Json;
using UnityEngine;
using HBS;



namespace SoftExperienceCap
{
    public class SoftExperienceCap
    {
        internal static string ModDirectory;
        internal static Settings Settings;

        // BEN: Debug (0: nothing, 1: errors, 2:all)
        internal static int DebugLevel = 2;

        internal static string xpCapByArgoStateEffectString = "• Mission experience can be fully utilized up to a total of {0} points.";
        internal static string CampaignCommanderUpdateTag = "soft_experience_cap_applied";



        public static void Init(string directory, string settings)
        {
            var harmony = HarmonyInstance.Create("de.mad.SoftExperienceCap");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            ModDirectory = directory;
            try
            {
                Settings = JsonConvert.DeserializeObject<Settings>(settings);
            }
            catch (Exception)
            {
                Settings = new Settings();
            }
        }
    }



    [HarmonyPatch(typeof(SimGameState), "_OnAttachUXComplete")]
    public static class SimGameState__OnAttachUXComplete_Patch
    {
        public static void Prefix(SimGameState __instance)

        {
            // TEST
            //__instance.CompanyTags.Remove("patch_1_2_abilities");
        }

        public static void Postfix(SimGameState __instance)

        {
            if (__instance.ApplyExperienceCap())
            {
                __instance.ResetExperienceForAllPilots();
            }
        }
    }



    [HarmonyPatch(typeof(SGShipModuleUpgradeViewPopulator), "BuildEffectsString")]
    public static class SGShipModuleUpgradeViewPopulator_BuildEffectsString_Patch
    {
        public static void Postfix(SGShipModuleUpgradeViewPopulator __instance, Localize.Text __result, SimGameState ___simState, ShipModuleUpgrade ___CurrentUpgrade)
        {
            try
            {
                Logger.LogLine("[SGShipModuleUpgradeViewPopulator_BuildEffectsString_POSTFIX] CurrentUpgrade.Description.Id: " + ___CurrentUpgrade.Description.Id);

                // @ToDo: Store numbers in vars/settings
                if (___CurrentUpgrade.Description.Id == "argoUpgrade_trainingModule1")
                {
                    Localize.Text additionalEntry = new Localize.Text(SoftExperienceCap.xpCapByArgoStateEffectString, new object[] { SoftExperienceCap.Settings.xpCapByArgoState[1] });
                    __result.Add(additionalEntry);
                }
                else if (___CurrentUpgrade.Description.Id == "argoUpgrade_trainingModule2")
                {
                    Localize.Text additionalEntry = new Localize.Text(SoftExperienceCap.xpCapByArgoStateEffectString, new object[] { SoftExperienceCap.Settings.xpCapByArgoState[2] });
                    __result.Add(additionalEntry);
                }
                else if (___CurrentUpgrade.Description.Id == "argoUpgrade_trainingModule3")
                {
                    Localize.Text additionalEntry = new Localize.Text(SoftExperienceCap.xpCapByArgoStateEffectString, new object[] { SoftExperienceCap.Settings.xpCapByArgoState[3] });
                    __result.Add(additionalEntry);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }



    // Info
    [HarmonyPatch(typeof(SGBarracksWidget), "OnPilotSelected")]
    public static class SGBarracksWidget_OnPilotSelected_Patch
    {
        public static void Postfix(ref SGBarracksWidget __instance, Pilot p, SimGameState ___simState)
        {
            try
            {
                PilotDef pDef = p.pilotDef;

                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") AbsoluteExperienceSpent: " + AbsoluteExperienceSpent);
                //Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") ExperienceSpent: " + pDef.ExperienceSpent);
                //Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") TotalXP: " + p.TotalXP);
                int SurplusXP = p.TotalXP - pDef.ExperienceSpent;
                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") SurplusXP: " + SurplusXP);
                int AbsoluteExperience = AbsoluteExperienceSpent + SurplusXP;
                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") AbsoluteExperience: " + AbsoluteExperience);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }

    /*
     * BEN: This is the real shit. For now i'm patching into this ONLY to check that my data injection via UI-Patches actually get called AFTER this.
     */
    [HarmonyPatch(typeof(Contract), "CompleteContract")]
    public static class Contract_CompleteContract_Patch
    {
        public static void Postfix(Contract __instance, MissionResult result, bool isGoodFaithEffort)
        {
            try
            {
                // Same for ALL pilots
                int ExperienceEarned = (int)AccessTools.Property(typeof(Contract), "ExperienceEarned").GetValue(__instance, null);
                Logger.LogLine("[Contract_CompleteContract_POSTFIX] ExperienceEarned: " + ExperienceEarned);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }


    [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInData")]
    public static class AAR_UnitStatusWidget_FillInData_Patch
    {
        public static void Prefix(AAR_UnitStatusWidget __instance, ref int xpEarned, SimGameState ___simState, UnitResult ___UnitData)
        {
            try
            {
                // NOTE that all vanilla getters already include xpEarned in their values! This is already set and done at this point.
                // For correct calculations the param "xpEarned" must be substracted first!

                Pilot p = ___UnitData.pilot;
                PilotDef pDef = p.pilotDef;

                int xpMinimum = SoftExperienceCap.Settings.xpMissionMinimum; // Just for the thrill of it
                int xpLimit = 114000; // All skills at 10

                int xpSoftCap = ___simState.GetCurrentExperienceCap();
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] Current xpSoftCap: " + xpSoftCap);

                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") AbsoluteExperienceSpent: " + AbsoluteExperienceSpent);

                int SurplusXP = p.TotalXP - pDef.ExperienceSpent - xpEarned;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") SurplusXP: " + SurplusXP + "(already substracted xpEarned of: " + xpEarned + ")");
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") UnspentXP: " + p.UnspentXP + "(still includes uncorrected xpEarned of: " + xpEarned + ")");

                int AbsoluteExperience = AbsoluteExperienceSpent + SurplusXP;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") AbsoluteExperience: " + AbsoluteExperience);

                int PotentialExperienceAfterMissionReward = AbsoluteExperience + xpEarned;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") PotentialExperienceAfterMissionReward: " + PotentialExperienceAfterMissionReward);



                int xpTemp = xpEarned;

                // Only minimum XP if already at XPCap
                if (AbsoluteExperience >= xpSoftCap)
                {
                    xpTemp = xpMinimum;
                    Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") Experience is above cap. Gaining only minimum XP.");
                }
                // Not more than XPCap + minimum XP
                if (AbsoluteExperience < xpSoftCap && PotentialExperienceAfterMissionReward >= xpSoftCap)
                {
                    xpTemp = (PotentialExperienceAfterMissionReward - xpSoftCap) + xpMinimum;
                    Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") Experience is hitting cap. Gaining less XP.");
                }
                // Absolutely no XP when at games hard limit?
                if (AbsoluteExperience >= xpLimit)
                {
                    xpTemp = 0;
                }
                // Always get XP for kills
                for (int i = 0; i < p.MechsKilled; i++)
                {
                    xpTemp += SoftExperienceCap.Settings.xpMissionMechKilled;
                }
                for (int j = 0; j < p.OthersKilled; j++)
                {
                    xpTemp += SoftExperienceCap.Settings.xpMissionOtherKilled;
                }

                // Set/Adjust unspent experience 
                int unspentXP = p.UnspentXP; // At this point UnspentXP still includes the originally earned XP so unmodified xpEarned needs to be substracted first
                int adjustedUnspentXP = unspentXP - xpEarned + xpTemp;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") adjustedUnspentXP: " + adjustedUnspentXP);
                p.StatCollection.Set<int>("ExperienceUnspent", adjustedUnspentXP);
                pDef.SetUnspentExperience(adjustedUnspentXP);

                // Modify for UI
                xpEarned = xpTemp;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
        public static void Postfix(AAR_UnitStatusWidget __instance, UnitResult ___UnitData)
        {
            try
            {
                Pilot p = ___UnitData.pilot;

                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_POSTFIX] CHECK (" + p.Name + ") UnspentXP: " + p.UnspentXP);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
    public static class AAR_UnitStatusWidget_FillInPilotData_Patch
    {
        public static void Postfix(AAR_UnitStatusWidget __instance, int xpEarned, SimGameState ___simState, UnitResult ___UnitData, TextMeshProUGUI ___XPText, SGBarracksRosterSlot ___PilotWidget)
        {
            try
            {
                Pilot p = ___UnitData.pilot;
                PilotDef pDef = p.pilotDef;

                int xpSoftCap = ___simState.GetCurrentExperienceCap();
                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                int UnspentXP = p.UnspentXP;
                int AbsoluteExperience = AbsoluteExperienceSpent + UnspentXP;
                
                // NOTE that xpEarned is already adjusted at this point
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_POSTFIX] (" + p.Name + ") xpEarned: " + xpEarned);
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_POSTFIX] (" + p.Name + ") AbsoluteExperience: " + AbsoluteExperience);

                /*
                ___XPText.SetText("+{0}XP ({1} / {2}XP)", new object[]
                {
                    xpEarned,
                    AbsoluteExperience,
                    xpSoftCap
                });
                */

                Color red = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.red;
                Color gold = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.gold;
                Color green = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.green;
                string htmlColorTag = "";
                if (AbsoluteExperience > xpSoftCap)
                {
                    htmlColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(red) + ">";
                }
                //else if ((AbsoluteExperience + xpEarned) > xpSoftCap)
                //{
                //    htmlColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + ">";
                //}
                else
                {
                    htmlColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(green) + ">";
                }

                TextMeshProUGUI callsign = (TextMeshProUGUI)AccessTools.Field(typeof(SGBarracksRosterSlot), "callsign").GetValue(___PilotWidget);
                callsign.enableAutoSizing = false;
                callsign.enableWordWrapping = false;
                callsign.fontSize = callsign.fontSize + 1;
                callsign.OverflowMode = TextOverflowModes.Overflow;

                callsign.SetText("{0} " + htmlColorTag + "({1} / {2}XP)</color>", new object[]
                {
                    p.Callsign,
                    AbsoluteExperience,
                    xpSoftCap
                });

            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
}

