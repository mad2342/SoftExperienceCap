using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Harmony;
using BattleTech;



namespace SoftExperienceCap
{
    class Utilities
    {
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

        public static int GetCurrentExperienceCap(SimGameState simGameState)
        {
            //int result = 22000; // All skills at 6
            int result = SoftExperienceCap.xpCapByArgoState[0];

            if (simGameState.HasShipUpgrade("argoUpgrade_trainingModule1"))
            {
                //result = 36400; // All skills at 7
                result = SoftExperienceCap.xpCapByArgoState[1];
            }
            if (simGameState.HasShipUpgrade("argoUpgrade_trainingModule2"))
            {
                //result = 56000; // All skills at 8
                result = SoftExperienceCap.xpCapByArgoState[2];
            }
            if (simGameState.HasShipUpgrade("argoUpgrade_trainingModule3"))
            {
                //result = 81600; // All skills at 9
                result = SoftExperienceCap.xpCapByArgoState[3];
            }
            return result;
        }
    }
}
