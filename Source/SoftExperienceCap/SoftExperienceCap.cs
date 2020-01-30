﻿using System;
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
using BattleTech.UI.TMProWrapper;

namespace SoftExperienceCap
{
    public class SoftExperienceCap
    {
        internal static string LogPath;
        internal static string ModDirectory;
        internal static Settings Settings;
        // BEN: DebugLevel (0: nothing, 1: error, 2: debug, 3: info)
        internal static int DebugLevel = 2;

        internal static string xpCapByArgoStateEffectString = "• Mission experience can be fully utilized up to a total of {0} points.";
        internal static string CampaignCommanderUpdateTag = "soft_experience_cap_applied";

        public static void Init(string directory, string settings)
        {
            ModDirectory = directory;
            LogPath = Path.Combine(ModDirectory, "SoftExperienceCap.log");

            Logger.Initialize(LogPath, DebugLevel, ModDirectory, nameof(SoftExperienceCap));

            try
            {
                Settings = JsonConvert.DeserializeObject<Settings>(settings);
            }
            catch (Exception e)
            {
                Settings = new Settings();
                Logger.Error(e);
            }

            // Harmony calls need to go last here because their Prepare() methods directly check Settings...
            HarmonyInstance harmony = HarmonyInstance.Create("de.mad.SoftExperienceCap");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
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
            if (__instance.CompletedContract != null)
            {
                return;
            }

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
                Logger.Debug("[SGShipModuleUpgradeViewPopulator_BuildEffectsString_POSTFIX] CurrentUpgrade.Description.Id: " + ___CurrentUpgrade.Description.Id);

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
                Logger.Error(e);
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

                Logger.Info("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") ExperienceSpent: " + pDef.ExperienceSpent);

                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                Logger.Info("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") AbsoluteExperienceSpent: " + AbsoluteExperienceSpent);
                
                Logger.Info("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") TotalXP: " + p.TotalXP);
                Logger.Info("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") UnspentXP: " + p.UnspentXP);

                int AbsoluteExperience = AbsoluteExperienceSpent + p.UnspentXP;
                Logger.Info("[SGBarracksWidget_OnPilotSelected_POSTFIX] (" + p.Name + ") AbsoluteExperience: " + AbsoluteExperience);
            }
            catch (Exception e)
            {
                Logger.Error(e);
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
                // Check XP beforehand
                foreach(UnitResult unitResult in __instance.PlayerUnitResults)
                {
                    Logger.Debug("[Contract_CompleteContract_POSTFIX] (" + unitResult.pilot.Callsign + ") UnspentXP: " + unitResult.pilot.UnspentXP);
                }

                // Same for ALL pilots by default
                Logger.Debug("[Contract_CompleteContract_POSTFIX] ExperienceEarned: " + __instance.ExperienceEarned);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }

    // Disable normal XP distribution
    [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
    public static class SimGameState_ResolveCompleteContract_Patch
    {
        // Must be prefix as CompletedContract may be set to null in original method
        public static void Prefix(SimGameState __instance)
        {
            try
            {
                Logger.Debug("[SimGameState_ResolveCompleteContract_PREFIX] __instance.CompletedContract.ExperienceEarned BEFORE: " + __instance.CompletedContract.ExperienceEarned);

                // BEN: Setting XP has already happened in patched AAR_UnitStatusWidget.FillInPilotData() -> Nullifying XP here.
                Contract CompletedContract = (Contract)AccessTools.Property(typeof(SimGameState), "CompletedContract").GetValue(__instance, null);
                int ExperienceEarned = (int)AccessTools.Property(typeof(Contract), "ExperienceEarned").GetValue(CompletedContract, null);

                // Set
                new Traverse(CompletedContract).Property("ExperienceEarned").SetValue(0);
                
                Logger.Debug("[SimGameState_ResolveCompleteContract_PREFIX] __instance.CompletedContract.ExperienceEarned AFTER: " + __instance.CompletedContract.ExperienceEarned);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }

    // Check
    [HarmonyPatch(typeof(PilotDef), "SetUnspentExperience")]
    public static class PilotDef_SetUnspentExperience_Patch
    {
        public static void Prefix(PilotDef __instance, int value)
        {
            try
            {
                Logger.Debug("[PilotDef_SetUnspentExperience_PREFIX] (" + __instance.Description.Callsign + ") value: " + value);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }



    [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
    public static class AAR_UnitStatusWidget_FillInPilotData_Patch
    {
        public static void Prefix(AAR_UnitStatusWidget __instance, ref int xpEarned, SimGameState ___simState, Contract ___contract, UnitResult ___UnitData, TextMeshProUGUI ___XPText, SGBarracksRosterSlot ___PilotWidget)
        {
            try
            {
                // Check mission state
                Contract.ContractState state = ___contract.State;
                bool missionComplete = state == Contract.ContractState.Complete;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] Mission complete: " + missionComplete);
                bool missionFailed = state == Contract.ContractState.Failed;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] Mission failed: " + missionFailed);
                bool missionRetreatedGoodFaith = state == Contract.ContractState.Retreated && ___contract.IsGoodFaithEffort;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] Withdrawed (Good Faith): " + missionRetreatedGoodFaith);
                bool missionRetreatedBadFaith = state == Contract.ContractState.Retreated && !___contract.IsGoodFaithEffort;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] Withdrawed (Bad Faith): " + missionRetreatedBadFaith);

                // RETURN EARLY when absolutely no XP were earned
                if (xpEarned <= 0)
                {
                    return;
                }

                // Determine modifier for partial mission completion via XP comparison
                int xpPotentialMission = Mathf.FloorToInt((float)___contract.Override.finalDifficulty * ___simState.Constants.Pilot.BaseXPGainPerMission);
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] xpPotentialMission: " + xpPotentialMission);
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] xpEarned: " + xpEarned);
                double xpBonusModifier = (double)xpEarned / xpPotentialMission;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] xpBonusModifier: " + xpBonusModifier.ToString());

                // Early limit if applicable
                if (SoftExperienceCap.Settings.xpMissionMaximum > 0 && xpEarned > SoftExperienceCap.Settings.xpMissionMaximum)
                {
                    xpEarned = SoftExperienceCap.Settings.xpMissionMaximum;
                    Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] xpEarned exceeded configured maximum. Limited to: " + xpEarned);
                }

                Pilot p = ___UnitData.pilot;
                PilotDef pDef = p.pilotDef;
                Color red = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.red;
                Color gold = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.gold;
                Color green = LazySingletonBehavior<UIManager>.Instance.UIColorRefs.green;
                string xpCapInfoColorTag = "";

                int xpMinimum = SoftExperienceCap.Settings.xpMissionMinimum; // Just for the thrill of it
                int xpHardLimit = 114000; // All skills at 10

                int xpSoftCap = ___simState.GetCurrentExperienceCap();
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] Current xpSoftCap: " + xpSoftCap);

                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, ___simState);
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") AbsoluteExperienceSpent: " + AbsoluteExperienceSpent);

                int PreMissionUnspentXP = p.UnspentXP;
                int PostMissionUnspentXP = p.UnspentXP + xpEarned;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") PreMissionUnspentXP: " + PreMissionUnspentXP);
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") PostMissionUnspentXP: " + PostMissionUnspentXP);

                int PreMissionAbsoluteExperience = AbsoluteExperienceSpent + PreMissionUnspentXP;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") PreMissionAbsoluteExperience: " + PreMissionAbsoluteExperience);

                int PotentialExperiencePostMission = AbsoluteExperienceSpent + PostMissionUnspentXP;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") PotentialExperiencePostMission: " + PotentialExperiencePostMission);



                // Start calculations
                int xpOriginal = xpEarned;
                int xpTemp = 0;

                // Mission XP
                int xpMission = 0;

                // Absolutely no XP when at games hard limit?
                if (PreMissionAbsoluteExperience >= xpHardLimit)
                {
                    xpMission = 0;
                    xpCapInfoColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(red) + ">";

                    Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience was at BTGs absolute maximum. Gaining no XP for the mission.");
                }
                // Only minimum XP if already at XPCap before mission
                else if (PreMissionAbsoluteExperience >= xpSoftCap)
                {
                    xpMission = xpMinimum;
                    xpCapInfoColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + ">";

                    Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience was already above cap. Gaining only minimum XP for the mission.");
                }
                // Not more than XPCap + minimum XP
                else if (PreMissionAbsoluteExperience < xpSoftCap && PotentialExperiencePostMission >= xpSoftCap)
                {
                    xpMission = (xpSoftCap - PreMissionAbsoluteExperience) + xpMinimum;
                    xpCapInfoColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + ">";

                    Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience is hitting cap. Gaining less XP for the mission.");
                }
                // Normal XP
                else
                {
                    xpMission = xpOriginal;
                    xpCapInfoColorTag = "<color=#" + ColorUtility.ToHtmlStringRGBA(green) + ">";

                    Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") Experience is below cap. Gaining normal XP for the mission.");
                }
                xpTemp += xpMission;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") xpMission: " + xpMission);



                // Bonus XP for kills
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
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") xpBonusFromKills: " + xpBonusFromKills);



                // Bonus XP for being understaffed?
                int xpBonusUnderstaffedBase = (int)(200 * xpBonusModifier);
                int xpBonusUnderstaffed = 0;
                int playerUnitCount = ___contract.PlayerUnitResults.Count;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] playerUnitCount: " + playerUnitCount);
                switch (playerUnitCount)
                {
                    case 3:
                        xpBonusUnderstaffed = 1 * xpBonusUnderstaffedBase;
                        break;
                    case 2:
                        xpBonusUnderstaffed = 2 * xpBonusUnderstaffedBase;
                        break;
                    case 1:
                        xpBonusUnderstaffed = 6 * xpBonusUnderstaffedBase;
                        break;
                    default:
                        xpBonusUnderstaffed = 0;
                        break;
                }
                xpTemp += xpBonusUnderstaffed;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") xpBonusUnderstaffed: " + xpBonusUnderstaffed);



                // Bonus XP for getting through mission undamaged?
                int xpBonusUndamagedBase = (int)(100 * xpBonusModifier);
                int xpBonusUndamaged = ___UnitData.mech.IsDamaged ? 0 : xpBonusUndamagedBase;
                xpTemp += xpBonusUndamaged;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") xpBonusUndamaged: " + xpBonusUndamaged);



                // Modify XP depending on lance weight?
                int xpModifierLanceWeightBase = (int)(100 * xpBonusModifier);
                int xpModifierLanceWeight = 0;

                //@ToDo: Check _finalDifficulty from ContractOverride?
                int contractDifficulty = ___contract.Difficulty;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] contractDifficulty: " + contractDifficulty);

                float combinedTonnage = 0f;
                int lanceTonnageRating = 0;
                List<UnitResult> playerUnitResults = ___contract.PlayerUnitResults;
                foreach (UnitResult unitResult in playerUnitResults)
                {
                    combinedTonnage += unitResult.mech.Chassis.Tonnage;
                }
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] combinedTonnage: " + combinedTonnage);
                for (int i = 0; i < ___simState.Constants.MechLab.LanceDropTonnageBrackets.Length; i++)
                {
                    if (combinedTonnage >= (float)___simState.Constants.MechLab.LanceDropTonnageBrackets[i])
                    {
                        lanceTonnageRating = i + 1;
                    }
                }
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] lanceTonnageRating: " + lanceTonnageRating);
                //if (lanceTonnageRating < contractDifficulty)
                //{
                    xpModifierLanceWeight = (contractDifficulty - lanceTonnageRating) * xpModifierLanceWeightBase;
                //}
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] xpModifierLanceWeight: " + xpModifierLanceWeight);
                xpTemp += xpModifierLanceWeight;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") xpModifierLanceWeight: " + xpModifierLanceWeight);



                // Bonus XP for completing mission even though friendly units were lost?
                // NOTE that due to a vanilla bug sometimes ejected pilots don't get reported as that (and the head of their Mech won't get destroyed)
                // Commenting out until this is fixed.
                /*
                int xpBonusLastOnesStandingBase = (int)(200 * xpBonusModifier);
                int xpBonusLastOnesStanding = 0;
                int alliesLost = 0;
                foreach (UnitResult unitResult in playerUnitResults)
                {
                    alliesLost += unitResult.pilot.IsIncapacitated ? 1 : 0;
                    alliesLost += unitResult.pilot.HasEjected ? 1 : 0;
                }
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") IsIncapacitated: " + p.IsIncapacitated);
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") HasEjected: " + p.HasEjected);
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] playerUnitResults.Count: " + playerUnitResults.Count);
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] alliesLost: " + alliesLost);

                // Only if not part of alliesLost themselves AND only if mission was completed
                if (alliesLost > 0 && !p.IsIncapacitated && !p.HasEjected && missionComplete)
                {
                    xpBonusLastOnesStanding = alliesLost * xpBonusLastOnesStandingBase;
                }
                xpTemp += xpBonusLastOnesStanding;
                Logger.Debug("[AAR_UnitStatusWidget_FillInPilotData_PREFIX] (" + p.Name + ") xpBonusLastOnesStanding: " + xpBonusLastOnesStanding);
                */



                // Sanitize
                if (xpTemp < 0)
                {
                    xpTemp = 0;
                }



                // Adjust unspent experience 
                int AdjustedUnspentXP = PreMissionUnspentXP + xpTemp;
                Logger.Debug("[AAR_UnitStatusWidget_FillInData_PREFIX] (" + p.Name + ") AdjustedUnspentXP: " + AdjustedUnspentXP);
                p.StatCollection.Set<int>("ExperienceUnspent", AdjustedUnspentXP);
                pDef.SetUnspentExperience(AdjustedUnspentXP);

                // Modify for UI
                xpEarned = xpTemp;



                // Show additional info about current/maximum XP in UI
                // This was generalized in SGBarracksRosterSlot.Refresh()
                /*
                TextMeshProUGUI callsign = (TextMeshProUGUI)AccessTools.Field(typeof(SGBarracksRosterSlot), "callsign").GetValue(___PilotWidget);
                callsign.enableAutoSizing = false;
                callsign.enableWordWrapping = false;
                callsign.fontSize = callsign.fontSize + 1;
                callsign.OverflowMode = TextOverflowModes.Overflow;

                callsign.SetText("{0} (" + xpCapInfoColorTag + "{1}</color> / {2}XP)", new object[]
                {
                    p.Callsign,
                    //PotentialExperiencePostMission,
                    PreMissionAbsoluteExperience,
                    xpSoftCap
                });
                */



                // Generate tooltip for details on gained XP
                string Name = p.Callsign + " gained <color=#" + ColorUtility.ToHtmlStringRGBA(gold) + ">" + xpEarned + "XP</color>";
                string Details = "";

                // Mission state details
                if (missionComplete)
                {
                    Details += "The mission was <b><color=#" + ColorUtility.ToHtmlStringRGBA(green) + ">successful</color></b>! The following XP modifiers apply:\n\n";
                }
                else if (missionRetreatedGoodFaith)
                {
                    Details += "The mission <b><color=#" + ColorUtility.ToHtmlStringRGBA(red) + ">failed</color></b> but you managed to leave the battlefield in <b><color=#" + ColorUtility.ToHtmlStringRGBA(green) + ">good faith</color></b>. The following XP modifiers apply:\n\n";
                }
                else if (missionRetreatedBadFaith)
                {
                    Details += "The mission <b><color=#" + ColorUtility.ToHtmlStringRGBA(red) + ">failed</color></b>. You retreated from the battlefield in <b><color=#" + ColorUtility.ToHtmlStringRGBA(red) + ">bad faith</color></b>. The following XP modifiers apply:\n\n";
                }
                else if (missionFailed)
                {
                    Details += "The mission <b><color=#" + ColorUtility.ToHtmlStringRGBA(red) + ">failed</color></b>. At least the survivors can learn something from from it. The following XP modifiers apply:\n\n";
                }
                else
                {
                    Logger.Debug("[AAR_UnitStatusWidget_FillInData_PREFIX] Unknown mission state.");
                }

                // Bonus XP details
                if (xpMission > 0)
                {
                    Details += "<b>MISSION:<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + "> +" + xpMission + "XP</color></b>\n\n";
                    Details += "This pilot is not yet fully trained and can still utilize mission experience thanks to the current development stage of the Argo Training Modules.";
                }
                else
                {
                    Details += "<b>MISSION: " + xpMission + "XP</b>\n\n";
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
                    Details += "<b>KILLS: " + xpBonusFromKills + "XP</b>\n\n";
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
                    Details += "<b>UNDERSTAFFED: " + xpBonusUnderstaffed + "XP</b>\n\n";
                    Details += "This pilot was deployed as part of a full lance and contributed normally to the outcome of the mission.";
                }
                Details += "\n\n";

                if (xpBonusUndamaged > 0)
                {
                    Details += "<b>UNDAMAGED:<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + "> +" + xpBonusUndamaged + "XP</color></b>\n\n";
                    Details += "This pilots 'Mech remained undamaged over the course of the mission.";
                }
                else
                {
                    Details += "<b>UNDAMAGED: " + xpBonusUndamaged + "XP</b>\n\n";
                    Details += "This pilots 'Mech suffered structural damage over the course of the mission.";
                }
                Details += "\n\n";

                if (xpModifierLanceWeight > 0)
                {
                    Details += "<b>LANCE RATING:<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + "> +" + xpModifierLanceWeight + "XP</color></b>\n\n";
                    Details += "This pilots lance had to cope with the extra stress of facing overwhelming enemy forces.";
                }
                else if (xpModifierLanceWeight < 0)
                {
                    Details += "<b>LANCE RATING:<color=#" + ColorUtility.ToHtmlStringRGBA(red) + "> " + xpModifierLanceWeight + "XP</color></b>\n\n";
                    Details += "This pilots lance outmatched the enemy forces resulting in less valuable combat experience.";
                }
                else
                {
                    Details += "<b>LANCE RATING: " + xpModifierLanceWeight + "XP</b>\n\n";
                    Details += "This pilots lance was en par with the encountered enemy forces.";
                }
                /*
                Details += "\n\n";

                if (xpBonusLastOnesStanding > 0)
                {
                    Details += "<b>LAST ONES STANDING:<color=#" + ColorUtility.ToHtmlStringRGBA(gold) + "> +" + xpBonusLastOnesStanding + "XP</color></b>\n\n";
                    Details += "This pilot has lost lance mates during the course of the mission but could make up for it.";
                }
                */



                GameObject XPTextGameObject = ___XPText.gameObject;
                HBSTooltip Tooltip = XPTextGameObject.AddComponent<HBSTooltip>();
                HBSTooltipStateData StateData = new HBSTooltipStateData();
                BaseDescriptionDef TooltipContent = new BaseDescriptionDef("", Name, Details, "");

                StateData.SetObject(TooltipContent);
                Tooltip.SetDefaultStateData(StateData);

            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }

    // Display XP next to pilots callsign in Roster Slots
    [HarmonyPatch(typeof(SGBarracksRosterSlot), "Refresh")]
    public static class SGBarracksRosterSlot_Refresh_Patch
    {
        public static void Postfix(SGBarracksRosterSlot __instance, SimGameState ___simState, bool ___showCost, Pilot ___pilot, LocalizableText ___callsign, HBSTooltip ___TypeTooltip)
        {
            try
            {
                // Disable for Hiring Halls?
                if (___showCost && ___simState != null)
                {
                    return;
                }

                SimGameState simGameState = UnityGameInstance.BattleTechGame.Simulation;
                PilotDef pDef = ___pilot.pilotDef;
                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, simGameState);
                int AbsoluteExperience = AbsoluteExperienceSpent + ___pilot.UnspentXP;
                int xpSoftCap = simGameState.GetCurrentExperienceCap();


                Logger.Debug("[SGBarracksRosterSlot_Refresh_POSTFIX] (" + ___pilot.Name + ") AbsoluteExperience: " + AbsoluteExperience);

                string AbsoluteExperienceString = "";
                string xpSoftCapString = Utilities.WrapWithColor(xpSoftCap, "medGray");
                string callsignCurrentText = ___callsign.text;
                string xpInfo = "";

                if (AbsoluteExperience >= xpSoftCap)
                {
                    AbsoluteExperienceString = Utilities.WrapWithColor(AbsoluteExperience, "goldHalf");
                }
                else
                {
                    AbsoluteExperienceString = Utilities.WrapWithColor(AbsoluteExperience, "greenHalf");
                }

                //___callsign.faceColor = white;
                ___callsign.enableAutoSizing = false;
                ___callsign.enableWordWrapping = false;
                ___callsign.overflowMode = TextOverflowModes.Overflow;
 
                xpInfo = "("+ AbsoluteExperienceString + "/"+ xpSoftCapString + " XP)";
                xpInfo = Utilities.WrapWithColor(xpInfo, "medGray");

                ___callsign.SetText("{0}   {1}", new object[]
                {
                    callsignCurrentText,
                    xpInfo
                });

                // Override Rank Tooltip
                if (___TypeTooltip != null)
                {
                    BaseDescriptionDef overrideDef = Utilities.BuildRankTooltipOverrideDef(simGameState, ___pilot, AbsoluteExperience, xpSoftCap);
                    ___TypeTooltip.SetDefaultStateData(TooltipUtilities.GetStateDataFromObject(overrideDef)); 
                }

            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }

    // Display XP next to pilots callsign in Dossier Panel
    [HarmonyPatch(typeof(SGBarracksDossierPanel), "SetPilot")]
    public static class SGBarracksDossierPanel_SetPilot_Patch
    {
        public static void Postfix(SGBarracksDossierPanel __instance, Pilot p, LocalizableText ___callsign, HBSTooltip ___VeteranReference)
        {
            try
            {
                SimGameState simGameState = UnityGameInstance.BattleTechGame.Simulation;
                PilotDef pDef = p.pilotDef;
                int AbsoluteExperienceSpent = Utilities.GetAbsoluteExperienceSpent(pDef, simGameState);
                int AbsoluteExperience = AbsoluteExperienceSpent + p.UnspentXP;
                int xpSoftCap = simGameState.GetCurrentExperienceCap();

                string AbsoluteExperienceString = "";
                string xpSoftCapString = Utilities.WrapWithColor(xpSoftCap, "medGray");
                string callsignCurrentText = ___callsign.text;
                string xpInfo = "";

                if (AbsoluteExperience >= xpSoftCap)
                {
                    AbsoluteExperienceString = Utilities.WrapWithColor(AbsoluteExperience, "gold");
                }
                else
                {
                    AbsoluteExperienceString = Utilities.WrapWithColor(AbsoluteExperience, "green");
                }

                //___callsign.enableAutoSizing = false;
                //___callsign.enableWordWrapping = false;
                //___callsign.OverflowMode = TextOverflowModes.Overflow;

                xpInfo = "(" + AbsoluteExperienceString + "/" + xpSoftCapString + " XP)";
                xpInfo = Utilities.WrapWithColor(xpInfo, "medGray");

                ___callsign.SetText("{0}   <size=15>{1}</size>", new object[]
                {
                    callsignCurrentText,
                    xpInfo
                });

                // Override Rank Tooltip
                if (___VeteranReference != null)
                {
                    BaseDescriptionDef overrideDef = Utilities.BuildRankTooltipOverrideDef(simGameState, p, AbsoluteExperience, xpSoftCap);
                    ___VeteranReference.SetDefaultStateData(TooltipUtilities.GetStateDataFromObject(overrideDef));
                }

            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }
}

