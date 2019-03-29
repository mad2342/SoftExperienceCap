﻿namespace SoftExperienceCap
{
    internal class Settings
    {
        public int[] xpCapByArgoState = new int[] { 25000, 30000, 40000, 60000 };
        public int xpMissionMinimum = 0;
        public int xpMissionMaximum = 4000;
        public int xpMissionMechKilled = 100;
        public int xpMissionOtherKilled = 50;

        public bool ApplyPilotRespec = true;
        public bool ForcePilotRespec = false;
        public bool CommanderRespec = true;
    }
}
