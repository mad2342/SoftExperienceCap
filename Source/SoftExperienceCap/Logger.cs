﻿using System;
using System.IO;

namespace SoftExperienceCap
{
    public class Logger
    {
        static string filePath = $"{SoftExperienceCap.ModDirectory}/SoftExperienceCap.log";
        public static void LogError(Exception ex)
        {
            if (SoftExperienceCap.DebugLevel >= 1)
            {
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    var prefix = "[SoftExperienceCap @ " + DateTime.Now.ToString() + "]";
                    writer.WriteLine("Message: " + ex.Message + "<br/>" + Environment.NewLine + "StackTrace: " + ex.StackTrace + "" + Environment.NewLine);
                    writer.WriteLine("----------------------------------------------------------------------------------------------------" + Environment.NewLine);
                }
            }
        }

        public static void LogLine(String line)
        {
            if (SoftExperienceCap.DebugLevel >= 2)
            {
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    var prefix = "[SoftExperienceCap @ " + DateTime.Now.ToString() + "]";
                    writer.WriteLine(prefix + line);
                }
            }
        }
    }
}