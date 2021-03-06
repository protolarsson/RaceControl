﻿using System.IO;

namespace RaceControl.Common.Utils
{
    public static class StringUtils
    {
        public static string RemoveInvalidFileNameChars(this string filename, string replaceWith = "")
        {
            return string.Join(replaceWith, filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}