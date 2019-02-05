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
using BattleTech.UI.Tooltips;
using System.IO;



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
            // Empty log at startup
            File.CreateText($"{SoftExperienceCap.ModDirectory}/SoftExperienceCap.log");

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
        public static void Postfix(Contract __instance)
        {
            try
            {
                // Same for ALL pilots
                Logger.LogLine("[Contract_CompleteContract_POSTFIX] ExperienceEarned: " + __instance.ExperienceEarned);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }

    /*
    [HarmonyPatch(typeof(AAR_UnitsResult_Screen), "FillInData")]
    public static class AAR_UnitsResult_Screen_FillInData_Patch
    {
        public static void Prefix(AAR_UnitsResult_Screen __instance, List<AAR_UnitStatusWidget> ___UnitWidgets, List<UnitResult> ___UnitResults, int ___numUnits, Contract ___theContract)
        {
            try
            {

            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
    */

    [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
    public static class AAR_UnitStatusWidget_FillInPilotData_Patch
    {
        public static void Prefix(AAR_UnitStatusWidget __instance, ref int xpEarned, SimGameState ___simState, Contract ___contract, UnitResult ___UnitData, TextMeshProUGUI ___XPText, SGBarracksRosterSlot ___PilotWidget)
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
                string xpCapInfoColorTag = "";

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


                int xpOriginal = xpEarned;
                int xpTemp = 0;



                int xpMission = 0;
                // Absolutely no XP when at games hard limit?
                if (PreMissionAbsoluteExperience >= xpHardLimit)
                {
                    xpMission = 0;
                    xpCapInfoColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(red) + ">";

                    Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience was at BTGs absolute maximum. Gaining no XP for the mission.");
                }
                // Only minimum XP if already at XPCap before mission
                else if (PreMissionAbsoluteExperience >= xpSoftCap)
                {
                    xpMission = xpMinimum;
                    xpCapInfoColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + ">";

                    Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience was already above cap. Gaining only minimum XP for the mission.");
                }
                // Not more than XPCap + minimum XP
                else if (PreMissionAbsoluteExperience < xpSoftCap && PotentialExperiencePostMission >= xpSoftCap)
                {
                    xpMission = (PotentialExperiencePostMission - xpSoftCap) + xpMinimum;
                    xpCapInfoColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + ">";

                    Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience is hitting cap. Gaining less XP for the mission.");
                }
                // Normal XP
                else
                {
                    xpMission = xpOriginal;
                    xpCapInfoColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(green) + ">";

                    Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience is below cap. Gaining normal XP for the mission.");
                }
                xpTemp += xpMission;



                // Always get XP for kills
                int xpBonusFromKills = 0;
                for (int i = 0; i < p.MechsKilled; i++)
                {
                    xpBonusFromKills += SoftExperienceCap.Settings.xpMissionMechKilled;
                }
                for (int j = 0; j < p.OthersKilled; j++)
                {
                    xpBonusFromKills += SoftExperienceCap.Settings.xpMissionOtherKilled;
                }
                xpTemp += xpBonusFromKills;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") xpBonusFromKills: " + xpBonusFromKills);



                // Bonus XP for being understaffed?
                int xpBonusUnderstaffed = 0;
                int playerUnitCount = ___contract.PlayerUnitResults.Count;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] playerUnitCount: " + playerUnitCount);
                switch (playerUnitCount)
                {
                    case 3:
                        xpBonusUnderstaffed = 200;
                        break;
                    case 2:
                        xpBonusUnderstaffed = 400;
                        break;
                    case 1:
                        xpBonusUnderstaffed = 1200;
                        break;
                    default:
                        xpBonusUnderstaffed = 0;
                        break;
                }
                xpTemp += xpBonusUnderstaffed;



                // Bonus XP for lance being underweight?
                // @ToDo: Handle this as a general modifier, which can also substract XP from mission XP? -> Overweight player lances get less XP?
                int xpBonusUnderweight = 0;
                int contractDifficulty = ___contract.Difficulty;
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] contractDifficulty: " + contractDifficulty);
                float combinedTonnage = 0f;
                int lanceTonnageRating = 0;
                List<UnitResult> playerUnitResults = ___contract.PlayerUnitResults;
                foreach (UnitResult unitResult in playerUnitResults)
                {
                    combinedTonnage += unitResult.mech.Chassis.Tonnage;
                }
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] combinedTonnage: " + combinedTonnage);
                for (int i = 0;  i < ___simState.Constants.MechLab.LanceDropTonnageBrackets.Length; i++)
                {
                    if (combinedTonnage >= (float)___simState.Constants.MechLab.LanceDropTonnageBrackets[i])
                    {
                        lanceTonnageRating = i + 1;
                    }
                }
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] lanceTonnageRating: " + lanceTonnageRating);
                if (lanceTonnageRating < contractDifficulty)
                {
                    xpBonusUnderweight = (contractDifficulty - lanceTonnageRating) * 100;
                }
                Logger.LogLine("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] xpBonusUnderweight: " + xpBonusUnderweight);
                xpTemp += xpBonusUnderweight;



                // Bonus XP for getting through mission undamaged?
                int xpBonusUndamaged = ___UnitData.mech.IsDamaged ? 0 : 100;
                xpTemp += xpBonusUndamaged;



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

                callsign.SetText("{0} " + xpCapInfoColorTag + "({1} / {2}XP)</color>", new object[]
                {
                    p.Callsign,
                    //PotentialExperiencePostMission,
                    PreMissionAbsoluteExperience,
                    xpSoftCap
                });

                // Generate tooltip for details on gained XP
                string Name = p.Callsign + " gained <color=#" + ColorUtility.ToHtmlStringRGBA(gold) + ">" + xpEarned + "XP</color>";
                string Details = "";
                
                if (xpMission > 0)
                {
                    Details += "<b>MISSION:<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + "> +" + xpMission + "XP</color></b>\n\n";
                    Details += "This pilot is not yet fully trained and can still utilize mission experience thanks to current state of the Argo Training Modules.";
                }
                else
                {
                    Details += "<b>MISSION: +" + xpMission + "XP</b>\n\n";
                    Details += "This pilot has exhausted the Argos current training potential and cannot utilize experience from standard mission procedures anymore.";
                }
                Details += "\n\n";

                if (xpBonusFromKills > 0)
                {
                    Details += "<b>KILLS:<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + "> +" + xpBonusFromKills + "XP</color></b>\n\n";
                    Details += "This pilot has successfully destroyed " + (p.MechsKilled + p.OthersKilled) + " hostile units on the battlefield.";
                }
                else
                {
                    Details += "<b>KILLS: +" + xpBonusFromKills + "XP</b>\n\n";
                    Details += "This pilot didn't destroy any hostile units on the battlefield.";
                }
                Details += "\n\n";

                if (xpBonusUnderstaffed > 0)
                {
                    Details += "<b>UNDERSTAFFED:<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + "> +" + xpBonusUnderstaffed + "XP</color></b>\n\n";
                    Details += "This pilot had to contribute extraordinarily to the mission outcome as part of an incomplete lance.";
                }
                else
                {
                    Details += "<b>UNDERSTAFFED: +" + xpBonusUnderstaffed + "XP</b>\n\n";
                    Details += "This pilot was deployed as part of a full lance and contributed normally to the outcome of the mission.";
                }
                Details += "\n\n";

                if (xpBonusUnderweight > 0)
                {
                    Details += "<b>UNDERWEIGHT:<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + "> +" + xpBonusUnderweight + "XP</color></b>\n\n";
                    Details += "This pilots lance had to cope with the extra stress of facing overwhelming enemy forces.";
                }
                else
                {
                    Details += "<b>UNDERWEIGHT: +" + xpBonusUnderweight + "XP</b>\n\n";
                    Details += "This pilots lance was appropriately sized for the encountered enemy forces.";
                }
                Details += "\n\n";

                if (xpBonusUndamaged > 0)
                {
                    Details += "<b>UNDAMAGED:<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + "> +" + xpBonusUndamaged + "XP</color></b>\n\n";
                    Details += "This pilots 'Mech remained undamaged over the course of the mission.";
                }
                else
                {
                    Details += "<b>UNDAMAGED: +" + xpBonusUndamaged + "XP</b>\n\n";
                    Details += "This pilots 'Mech suffered structural damage over the course of the mission.";
                }

                GameObject XPTextGameObject = ___XPText.gameObject;
                HBSTooltip Tooltip = XPTextGameObject.AddComponent<HBSTooltip>();
                HBSTooltipStateData StateData = new HBSTooltipStateData();
                BaseDescriptionDef TooltipContent = new BaseDescriptionDef("", Name, Details, "");

                StateData.SetObject(TooltipContent);
                Tooltip.SetDefaultStateData(StateData);

            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
}

