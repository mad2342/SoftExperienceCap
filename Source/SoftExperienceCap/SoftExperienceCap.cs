using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Harmony;
using BattleTech;
using BattleTech.UI;
using System.IO;
using Localize;



namespace SoftExperienceCap
{
    public class SoftExperienceCap
    {
        public static string ModDirectory;

        // BEN: Debug (0: nothing, 1: errors, 2:all)
        internal static int DebugLevel = 2;

        internal static int[] xpCapByArgoState = new int[] { 25000, 30000, 40000, 60000 };
        internal static string xpCapByArgoStateEffectString = "• Mission experience can be fully utilized up to a total of {0} points.";

        public static void Init(string directory, string settingsJSON)
        {
            ModDirectory = directory;
            var harmony = HarmonyInstance.Create("de.ben.SoftExperienceCap");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
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
                    Localize.Text additionalEntry = new Localize.Text(SoftExperienceCap.xpCapByArgoStateEffectString, new object[] { SoftExperienceCap.xpCapByArgoState[1] });
                    __result.Add(additionalEntry);
                }
                else if (___CurrentUpgrade.Description.Id == "argoUpgrade_trainingModule2")
                {
                    Localize.Text additionalEntry = new Localize.Text(SoftExperienceCap.xpCapByArgoStateEffectString, new object[] { SoftExperienceCap.xpCapByArgoState[2] });
                    __result.Add(additionalEntry);
                }
                else if (___CurrentUpgrade.Description.Id == "argoUpgrade_trainingModule3")
                {
                    Localize.Text additionalEntry = new Localize.Text(SoftExperienceCap.xpCapByArgoStateEffectString, new object[] { SoftExperienceCap.xpCapByArgoState[3] });
                    __result.Add(additionalEntry);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }

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

                int xpMinimum = 10; // Just for the thrill of it
                int xpLimit = 114000; // All skills at 10

                int xpSoftCap = Utilities.GetCurrentExperienceCap(___simState);
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] Current xpSoftCap: " + xpSoftCap);

                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + ___UnitData.pilot.Name + "s AbsoluteExperienceSpent: " + AbsoluteExperienceSpent);

                int SurplusXP = p.TotalXP - pDef.ExperienceSpent; Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s SurplusXP: " + SurplusXP);
                int AbsoluteExperience = AbsoluteExperienceSpent + SurplusXP;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s AbsoluteExperience: " + AbsoluteExperience);

                int PotentialExperienceAfterMissionReward = AbsoluteExperience + xpEarned;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s PotentialExperienceAfterMissionReward: " + PotentialExperienceAfterMissionReward);



                int xpTemp = xpEarned;

                // Only minimum XP if already at XPCap
                if (AbsoluteExperience > xpSoftCap)
                {
                    xpTemp = xpMinimum;
                    Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s experience is above cap. Gaining only minimum XP.");
                }
                // Not more than XPCap + minimum XP
                if (AbsoluteExperience < xpSoftCap && PotentialExperienceAfterMissionReward > xpSoftCap)
                {
                    xpTemp = (PotentialExperienceAfterMissionReward - xpSoftCap) + xpMinimum;
                    Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] " + p.Name + "s experience is hitting cap. Gaining less XP.");
                }
                // Always get XP for kills
                for (int i = 0; i < p.MechsKilled; i++)
                {
                    xpTemp += 100;
                }
                for (int j = 0; j < p.OthersKilled; j++)
                {
                    xpTemp += 50;
                }
                // Absolutely no XP when at games hard limit?
                if (AbsoluteExperience >= xpLimit)
                {
                    //xpTemp = 0;
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

