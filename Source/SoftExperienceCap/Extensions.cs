using Harmony;
using BattleTech;
using BattleTech.UI;
using System.Collections.Generic;

namespace SoftExperienceCap
{
    public static class Extensions
    {
        // Add custom methods to SimGameState
        public static int GetCurrentExperienceCap(this SimGameState simGameState)
        {
            //int result = 22000; // All skills at 6
            int result = SoftExperienceCap.Settings.xpCapByArgoState[0];

            if (simGameState.HasShipUpgrade("argoUpgrade_trainingModule1"))
            {
                //result = 36400; // All skills at 7
                result = SoftExperienceCap.Settings.xpCapByArgoState[1];
            }
            if (simGameState.HasShipUpgrade("argoUpgrade_trainingModule2"))
            {
                //result = 56000; // All skills at 8
                result = SoftExperienceCap.Settings.xpCapByArgoState[2];
            }
            if (simGameState.HasShipUpgrade("argoUpgrade_trainingModule3"))
            {
                //result = 81600; // All skills at 9
                result = SoftExperienceCap.Settings.xpCapByArgoState[3];
            }
            return result;
        }

        public static bool ApplyExperienceCap(this SimGameState simGameState)
        {
            foreach (string tag in simGameState.CompanyTags.ToArray())
            {
                Logger.LogLine("[Extensions.ApplyExperienceCap] SimGameState.CompanyTag: " + tag);
            }

            if (SoftExperienceCap.Settings.ApplyPilotRespec && !simGameState.CompanyTags.Contains(SoftExperienceCap.CampaignCommanderUpdateTag))
            {
                return true;
            }
            if (SoftExperienceCap.Settings.ForcePilotRespec)
            {
                return true;
            }
            return false;
        }

        public static void ResetExperienceForAllPilots(this SimGameState simGameState)
        {
            // Commander
            if (SoftExperienceCap.Settings.CommanderRespec)
            {
                simGameState.ResetExperienceForPilot(simGameState.Commander);
            }

            // Pilots
            foreach (Pilot pilot in simGameState.PilotRoster)
            {
                simGameState.ResetExperienceForPilot(pilot);
            }

            // Notification
            string body = "Mod settings demanded that your pilots had to be reset. Their experience was adjusted according to the state of your current Dropship. You can reallocate in the Barracks.";
            string title = "Soft Experience Cap";
            SimGameInterruptManager interruptQueue = (SimGameInterruptManager)AccessTools.Field(typeof(SimGameState), "interruptQueue").GetValue(simGameState);

            interruptQueue.QueueGenericPopup_NonImmediate(title, body, false, new GenericPopupButtonSettings[0]).AddButton("Continue", null, true, null);
            if (simGameState.CompletedContract == null)
            {
                AccessTools.Field(typeof(SimGameState), "_forceInterruptCheck").SetValue(simGameState, true);
            }
        }

        public static void ResetExperienceForPilot(this SimGameState simGameState, Pilot pilot)
        {
            // @ToDo: Remove unnecessary code/vars
            Logger.LogLine("[Extensions.ResetExperienceForPilot] Will calculate current xp, reset skills and cap reallocated experience as defined in settings");

            PilotDef pDef = pilot.pilotDef.CopyToSim();
            int xpSoftCap = simGameState.GetCurrentExperienceCap();
            int xpBase = 0;
            int xpAboveBase = 0;
            int xpSpent = 0;
            int xpUnspent = pilot.UnspentXP;
            int xpTotal = 0;
            int xpToReallocate = 0;

            if (pDef.BonusPiloting > 0)
            {
                xpBase += simGameState.GetLevelRangeCost(0, pDef.BasePiloting - 1);
                xpAboveBase += simGameState.GetLevelRangeCost(pDef.BasePiloting, pDef.SkillPiloting - 1);
            }
            if (pDef.BonusGunnery > 0)
            {
                xpBase += simGameState.GetLevelRangeCost(0, pDef.BaseGunnery - 1);
                xpAboveBase += simGameState.GetLevelRangeCost(pDef.BaseGunnery, pDef.SkillGunnery - 1);
            }
            if (pDef.BonusGuts > 0)
            {
                xpBase += simGameState.GetLevelRangeCost(0, pDef.BaseGuts - 1);
                xpAboveBase += simGameState.GetLevelRangeCost(pDef.BaseGuts, pDef.SkillGuts - 1);
            }
            if (pDef.BonusTactics > 0)
            {
                xpBase += simGameState.GetLevelRangeCost(0, pDef.BaseTactics - 1);
                xpAboveBase += simGameState.GetLevelRangeCost(pDef.BaseTactics, pDef.SkillTactics - 1);
            }

            // Return early for no xp gained at all
            if (xpAboveBase <= 0 && xpUnspent <= 0)
            {
                return;
            }

            // Calculate
            xpSpent = xpBase + xpAboveBase;
            xpTotal = xpSpent + xpUnspent;

            Logger.LogLine("[Extensions.ResetExperienceForPilot] Current xpSoftCap: " + xpSoftCap);
            Logger.LogLine("[Extensions.ResetExperienceForPilot] " + pilot.Name + "s xpBase: " + xpBase);
            //Logger.LogLine("[Extensions.ResetExperienceForPilot] " + pilot.Name + "s xpAboveBase: " + xpAboveBase);
            //Logger.LogLine("[Extensions.ResetExperienceForPilot] " + pilot.Name + "s xpSpent: " + xpSpent);
            //Logger.LogLine("[Extensions.ResetExperienceForPilot] " + pilot.Name + "s xpUnspent: " + xpUnspent);
            Logger.LogLine("[Extensions.ResetExperienceForPilot] " + pilot.Name + "s xpTotal: " + xpTotal);

            if (xpTotal <= xpSoftCap)
            {
                // Better just return?
                xpToReallocate = xpTotal - xpBase;
            }
            else if (xpTotal > xpSoftCap)
            {
                xpToReallocate = (xpSoftCap - xpBase) >= 0 ? (xpSoftCap - xpBase) : 0;
            }

            // Reset
            pDef.abilityDefNames.Clear();
            List<string> abilities = SimGameState.GetAbilities(pDef.BaseGunnery, pDef.BasePiloting, pDef.BaseGuts, pDef.BaseTactics);
            pDef.abilityDefNames.AddRange(abilities);
            pDef.SetSpentExperience(0);
            pDef.ForceRefreshAbilityDefs();
            pDef.ResetBonusStats();
            pilot.FromPilotDef(pDef);

            // Add xp <= current cap
            pilot.StatCollection.Set<int>("ExperienceUnspent", xpToReallocate);
            pDef.SetUnspentExperience(pilot.StatCollection.GetValue<int>("ExperienceUnspent"));

            Logger.LogLine("[Extensions.ResetExperienceForPilot] CHECK: " + pilot.Name + "s AbsoluteExperienceAfter: " + (Utilities.GetAbsoluteExperienceSpent(pDef, simGameState) + xpToReallocate));

            // Mark this reset as done if not already
            if (!simGameState.CompanyTags.Contains(SoftExperienceCap.CampaignCommanderUpdateTag))
            {
                simGameState.CompanyTags.Add(SoftExperienceCap.CampaignCommanderUpdateTag);
                Logger.LogLine("[Extensions.ResetExperienceForPilot] Added " + SoftExperienceCap.CampaignCommanderUpdateTag + " to CompanyTags");
            }
            // Done.
        }
    }
}
