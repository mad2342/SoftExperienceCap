using System.Collections.Generic;
using BattleTech;
using BattleTech.UI;
using Harmony;
using HBS;
using UnityEngine;

namespace SoftExperienceCap
{
    public static class Utilities
    {
        public static Dictionary<string, Color> Colors = new Dictionary<string, Color>()
        {
            { "medGray", LazySingletonBehavior<UIManager>.Instance.UIColorRefs.medGray },
            { "white", LazySingletonBehavior<UIManager>.Instance.UIColorRefs.white },
            { "gold", LazySingletonBehavior<UIManager>.Instance.UIColorRefs.gold },
            { "green", LazySingletonBehavior<UIManager>.Instance.UIColorRefs.green },
            { "blue", LazySingletonBehavior<UIManager>.Instance.UIColorRefs.blue },
            { "goldHalf", LazySingletonBehavior<UIManager>.Instance.UIColorRefs.goldHalf },
            { "greenHalf", LazySingletonBehavior<UIManager>.Instance.UIColorRefs.greenHalf }
        };

        public static string WrapWithColor(string value, string color)
        {
            Color clr = Colors[color];
            return "<color=#" + ColorUtility.ToHtmlStringRGBA(clr) + ">" + value + "</color>";
        }

        public static string WrapWithColor(int value, string color)
        {
            Color clr = Colors[color];
            return "<color=#" + ColorUtility.ToHtmlStringRGBA(clr) + ">" + value.ToString() + "</color>";
        }

        public static BaseDescriptionDef BuildRankTooltipOverrideDef(SimGameState simGameState, Pilot p, int xpAbs, int xpCap)
        {
            int num = simGameState.GetPilotRank(p) - 1;
            string id = string.Format("RankMechWarrior{0}", num);

            BaseDescriptionDef def = UnityGameInstance.BattleTechGame.DataManager.BaseDescriptionDefs.Get(id);
            BaseDescriptionDef overrideDef = new BaseDescriptionDef(def);
            string overrideDetails = "";
            string overrideDetailsAppendix = "\n\n";
            string xpAbsStr = "";
            string xpCapStr = Utilities.WrapWithColor(xpCap, "medGray");

            if (xpAbs >= xpCap)
            {
                xpAbsStr = WrapWithColor(xpAbs, "gold");
                string experienceState = xpAbsStr + "/" + xpCapStr + " XP";
                experienceState = Utilities.WrapWithColor(experienceState, "medGray");

                overrideDetailsAppendix += "<b>EXPERIENCE: " + experienceState + "</b>\n\n";
                overrideDetailsAppendix += "This pilot has exhausted the Argos current training potential and cannot utilize experience from standard mission procedures anymore.";
            }
            else
            {
                xpAbsStr = WrapWithColor(xpAbs, "green");
                string experienceState = xpAbsStr + "/" + xpCapStr + " XP";
                experienceState = Utilities.WrapWithColor(experienceState, "medGray");

                overrideDetailsAppendix += "<b>EXPERIENCE: " + experienceState + "</b>\n\n";
                overrideDetailsAppendix += "This pilot is not yet fully trained and can still utilize mission experience thanks to the current development stage of the Argo Training Modules.";
            }

            if (simGameState.HasShipUpgrade("argoUpgrade_trainingModule3"))
            {
                ShipModuleUpgrade upgrade = simGameState.DataManager.ShipUpgradeDefs.Get("argoUpgrade_trainingModule3");
                string upgradeState = "Fully upgraded (" + upgrade.Description.Name + ")";
                upgradeState = Utilities.WrapWithColor(upgradeState, "gold");

                overrideDetailsAppendix += "\n\n";
                overrideDetailsAppendix += "<b>ARGO: "+ upgradeState + "</b>\n\n";
                overrideDetailsAppendix += "The Argo has incorporated everything possible to support your Mechwarriors training potential.";
            }
            else
            {
                string upgradeState = "Upgradable";

                if (simGameState.HasShipUpgrade("argoUpgrade_trainingModule2"))
                {
                    ShipModuleUpgrade upgrade = simGameState.DataManager.ShipUpgradeDefs.Get("argoUpgrade_trainingModule2");
                    upgradeState += " (" + upgrade.Description.Name + ")";
                }
                else if (simGameState.HasShipUpgrade("argoUpgrade_trainingModule1"))
                {
                    ShipModuleUpgrade upgrade = simGameState.DataManager.ShipUpgradeDefs.Get("argoUpgrade_trainingModule1");
                    upgradeState += " (" + upgrade.Description.Name + ")";
                }
                else
                {
                    upgradeState += " (No Training Module)";
                }

                upgradeState = Utilities.WrapWithColor(upgradeState, "green");

                overrideDetailsAppendix += "\n\n";
                overrideDetailsAppendix += "<b>ARGO: " + upgradeState + "</b>\n\n";
                overrideDetailsAppendix += "The Argo has further options to support your Mechwarriors training potential. Build additional Training Modules for your Mechwarriors.";
            }

            // Final note
            if (xpAbs >= xpCap && simGameState.HasShipUpgrade("argoUpgrade_trainingModule3"))
            {
                string pilotCallsign = p.Callsign.ToUpper();
                pilotCallsign = Utilities.WrapWithColor(pilotCallsign, "white");
                string progressionNote = "At this point " + pilotCallsign + " can only receive XP through extraordinary fighting performance.";
                progressionNote = Utilities.WrapWithColor(progressionNote, "medGray");

                overrideDetailsAppendix += "\n\n";
                overrideDetailsAppendix += progressionNote;
            }

            overrideDetails = overrideDef.Details + overrideDetailsAppendix;
            new Traverse(overrideDef).Property("Details").SetValue(overrideDetails);

            return overrideDef;   
        }

        public static int GetAbsoluteExperienceSpent(PilotDef pDef, SimGameState simGameState)
        {
            // BEN: LevelCosts: 0, 100, 400, 900, 1600, 2500, 3600, 4900, 6400, 8100
            int result = 0;
            for (int i = pDef.SkillGunnery - 1; i > 0; i--)
            {
                result += simGameState.GetLevelCost(i);
            }
            for (int i = pDef.SkillPiloting - 1; i > 0; i--)
            {
                result += simGameState.GetLevelCost(i);
            }
            for (int i = pDef.SkillGuts - 1; i > 0; i--)
            {
                result += simGameState.GetLevelCost(i);
            }
            for (int i = pDef.SkillTactics - 1; i > 0; i--)
            {
                result += simGameState.GetLevelCost(i);
            }
            return result;
        }
    }
}
