using System;
using System.Reflection;
using Harmony;
using BattleTech;
using BattleTech.UI;
using TMPro;
using Newtonsoft.Json;

namespace SoftExperienceCap
{
    public class SoftExperienceCap
    {
        internal static string ModDirectory;
        internal static Settings Settings;

        // BEN: Debug (0: nothing, 1: errors, 2:all)
        internal static int DebugLevel = 1;

        internal static string xpCapByArgoStateEffectString = "• Mission experience can be fully utilized up to a total of {0} points.";
        internal static string CampaignCommanderUpdateTag = "soft_experience_cap_applied";



        public static void Init(string directory, string settings)
        {
            var harmony = HarmonyInstance.Create("de.ben.SoftExperienceCap");
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
                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] " + p.Name + "s AbsoluteExperienceSpent: " + AbsoluteExperienceSpent);
                //Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] " + p.Name + " ExperienceSpent: " + pDef.ExperienceSpent);
                //Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] " + p.Name + " TotalXP: " + p.TotalXP);
                int SurplusXP = p.TotalXP - pDef.ExperienceSpent; Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] " + p.Name + "s SurplusXP: " + SurplusXP);
                int AbsoluteExperience = AbsoluteExperienceSpent + SurplusXP;
                Logger.LogLine("[SGBarracksWidget_OnPilotSelected_POSTFIX] " + p.Name + "s AbsoluteExperience: " + AbsoluteExperience);
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
                Pilot p = ___UnitData.pilot;
                PilotDef pDef = p.pilotDef;

                int xpMinimum = SoftExperienceCap.Settings.xpMissionMinimum; // Just for the thrill of it
                int xpLimit = 114000; // All skills at 10

                int xpSoftCap = ___simState.GetCurrentExperienceCap();
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] Current xpSoftCap: " + xpSoftCap);

                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s AbsoluteExperienceSpent: " + AbsoluteExperienceSpent);

                int SurplusXP = p.TotalXP - pDef.ExperienceSpent; Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s SurplusXP: " + SurplusXP);
                int AbsoluteExperience = AbsoluteExperienceSpent + SurplusXP;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s AbsoluteExperience: " + AbsoluteExperience);

                int PotentialExperienceAfterMissionReward = AbsoluteExperience + xpEarned;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s PotentialExperienceAfterMissionReward: " + PotentialExperienceAfterMissionReward);



                int xpTemp = xpEarned;

                // Only minimum XP if already at XPCap
                if (AbsoluteExperience >= xpSoftCap)
                {
                    xpTemp = xpMinimum;
                    Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s experience is above cap. Gaining only minimum XP.");
                }
                // Not more than XPCap + minimum XP
                if (AbsoluteExperience < xpSoftCap && PotentialExperienceAfterMissionReward >= xpSoftCap)
                {
                    xpTemp = (PotentialExperienceAfterMissionReward - xpSoftCap) + xpMinimum;
                    Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s experience is hitting cap. Gaining less XP.");
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

                // Set
                xpEarned = xpTemp;
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
}

