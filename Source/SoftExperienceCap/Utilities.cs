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
    }
}
