using System;
using System.Reflection;
using Harmony;
using BattleTech;
using BattleTech.UI;
using TMPro;
using Newtonsoft.Json;
using UnityEngine;
using HBS;
using System.Collections.Generic;

namespace SoftExperienceCap
{
    public class SoftExperienceCap
    {
        internal static string ModDirectory;
        internal static Settings Settings;

        // BEN: Debug (0: nothing, 1: errors, 2:all)
        internal static int DebugLevel = 2;

        internal static int xpBonusUnstaffed = 0;
        internal static int xpBonusUnstaffedBase = 100;

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

                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") ExperienceSpent: " + pDef.ExperienceSpent);

                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") AbsoluteExperienceSpent: " + AbsoluteExperienceSpent);
                
                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") TotalXP: " + p.TotalXP);
                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") UnspentXP: " + p.UnspentXP);

                int AbsoluteExperience = AbsoluteExperienceSpent + p.UnspentXP;
                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") AbsoluteExperience: " + AbsoluteExperience);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }

    /*
     * BEN: This is the real shit.
     * For now i'm patching into this ONLY to check that my data injection via UI-Patches actually get called AFTER this...
     * And to verify calculations regarding the actual XP earned.
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


    [HarmonyPatch(typeof(AAR_UnitsResult_Screen), "FillInData")]
    public static class AAR_UnitsResult_Screen_FillInData_Patch
    {
        public static void Prefix(AAR_UnitsResult_Screen __instance, List<AAR_UnitStatusWidget> ___UnitWidgets, Contract ___theContract)
        {
            try
            {
                int ExperienceEarned = ___theContract.ExperienceEarned;
                Logger.LogLine("[AAR_UnitsResult_Screen_FillInData_PREFIX] ExperienceEarned: " + ExperienceEarned);

                /*
                int UnstaffedUnits = 0;
                int BonusMultiplier = 1;

                for (int i = 0; i < 4; i++)
                {
                    if (___UnitWidgets[i] == null)
                    {
                        UnstaffedUnits++;
                        BonusMultiplier = BonusMultiplier * UnstaffedUnits;
                    }
                }
                */

                //TEST
                int UnstaffedUnits = 2;
                int BonusMultiplier = 2;

                if (UnstaffedUnits > 0)
                {
                    SoftExperienceCap.xpBonusUnstaffed = BonusMultiplier * SoftExperienceCap.xpBonusUnstaffedBase;
                }

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
        public static void Prefix(AAR_UnitStatusWidget __instance, ref int xpEarned, SimGameState ___simState, UnitResult ___UnitData, TextMeshProUGUI ___XPText, SGBarracksRosterSlot ___PilotWidget)
        {
            try
            {
                // NOTE that all vanilla getters already include xpEarned in their values! This is already set and done at this point.
                // For correct calculations the param "xpEarned" must be substracted first!

                Pilot p = ___UnitData.pilot;
                PilotDef pDef = p.pilotDef;
                Color red = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.red;
                Color gold = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.gold;
                Color green = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.green;
                string htmlColorTag = "";

                int xpMinimum = SoftExperienceCap.Settings.xpMissionMinimum; // Just for the thrill of it
                int xpHardLimit = 114000; // All skills at 10

                int xpSoftCap = ___simState.GetCurrentExperienceCap();
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] Current xpSoftCap: " + xpSoftCap);

                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") AbsoluteExperienceSpent: " + AbsoluteExperienceSpent);

                int PostMissionUnspentXP = p.UnspentXP;
                int PreMissionUnspentXP = p.UnspentXP - xpEarned;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") PostMissionUnspentXP: " + PostMissionUnspentXP);
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") PreMissionUnspentXP: " + PreMissionUnspentXP);


                int PreMissionAbsoluteExperience = AbsoluteExperienceSpent + PreMissionUnspentXP;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") PreMissionAbsoluteExperience: " + PreMissionAbsoluteExperience);

                int PotentialExperiencePostMission = AbsoluteExperienceSpent + PostMissionUnspentXP;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") PotentialExperiencePostMission: " + PotentialExperiencePostMission);



                int xpTemp = xpEarned;

                // Absolutely no XP when at games hard limit?
                if (PreMissionAbsoluteExperience >= xpHardLimit)
                {
                    xpTemp = 0;
                    htmlColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(red) + ">";

                    Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience was at BTGs absolute maximum. Gaining no XP for the mission.");
                }
                // Only minimum XP if already at XPCap before mission
                else if (PreMissionAbsoluteExperience >= xpSoftCap)
                {
                    xpTemp = xpMinimum;
                    htmlColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(red) + ">";

                    Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience was already above cap. Gaining only minimum XP for the mission.");
                }
                // Not more than XPCap + minimum XP
                else if (PreMissionAbsoluteExperience < xpSoftCap && PotentialExperiencePostMission >= xpSoftCap)
                {
                    xpTemp = (PotentialExperiencePostMission - xpSoftCap) + xpMinimum;
                    htmlColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + ">";

                    Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience is hitting cap. Gaining less XP for the mission.");
                }
                // Normal XP
                else
                {
                    htmlColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(green) + ">";

                    Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience is below cap. Gaining normal XP for the mission.");
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

                // Bonus XP for being understaffed?
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") SoftExperienceCap.xpBonusUnstaffed: " + SoftExperienceCap.xpBonusUnstaffed);

                // Adjust unspent experience 
                int AdjustedUnspentXP = PreMissionUnspentXP + xpTemp;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") AdjustedUnspentXP: " + AdjustedUnspentXP);
                p.StatCollection.Set<int>("ExperienceUnspent", AdjustedUnspentXP);
                pDef.SetUnspentExperience(AdjustedUnspentXP);

                // Modify for UI
                xpEarned = xpTemp;

                // Show additional info about current/maximum XP in UI
                TextMeshProUGUI callsign = (TextMeshProUGUI)AccessTools.Field(typeof(SGBarracksRosterSlot), "callsign").GetValue(___PilotWidget);
                callsign.enableAutoSizing = false;
                callsign.enableWordWrapping = false;
                callsign.fontSize = callsign.fontSize + 1;
                callsign.OverflowMode = TextOverflowModes.Overflow;

                callsign.SetText("{0} " + htmlColorTag + "({1} / {2}XP)</color>", new object[]
                {
                    p.Callsign,
                    //PotentialExperiencePostMission,
                    PreMissionAbsoluteExperience,
                    xpSoftCap
                });

            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
        public static void Postfix(AAR_UnitStatusWidget __instance, UnitResult ___UnitData, SimGameState ___simState)
        {
            try
            {
                Pilot p = ___UnitData.pilot;
                PilotDef pDef = p.pilotDef;

                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                int UnspentXP = p.UnspentXP;
                int AbsoluteExperience = AbsoluteExperienceSpent + UnspentXP;

                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_POSTFIX] CHECK (" + p.Name + ") UnspentXP: " + p.UnspentXP);
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_POSTFIX] CHECK (" + p.Name + ") AbsoluteExperience: " + AbsoluteExperience);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
}

