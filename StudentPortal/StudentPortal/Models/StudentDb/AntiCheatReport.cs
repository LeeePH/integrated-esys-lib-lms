using System.Collections.Generic;

namespace StudentPortal.Models.StudentDbLegacy
{
    public class AntiCheatReport
    {
        public string Source { get; set; } = string.Empty;
        public List<string>? Events { get; set; }
    }
}