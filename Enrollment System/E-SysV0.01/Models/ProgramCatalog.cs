using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    public class ProgramOption
    {
        public string Code { get; }
        public string Display { get; }

        public ProgramOption(string code, string display)
        {
            Code = code;
            Display = display;
        }
    }

    public static class ProgramCatalog
    {
        // Extend here when you add more programs
        private static readonly List<ProgramOption> _programs = new()
        {
            new("BSIT",  "BSIT - Bachelor of Science in Information Technology"),
            new("BSENT", "BSENT - Bachelor of Science in Entrepreneurship")
        };

        public static IReadOnlyList<ProgramOption> All => _programs;

        public static List<SelectListItem> GetSelectList(string? selected = null)
        {
            var items = new List<SelectListItem>();
            foreach (var p in _programs)
            {
                items.Add(new SelectListItem
                {
                    Value = p.Code,
                    Text = p.Display,
                    Selected = !string.IsNullOrWhiteSpace(selected) &&
                               selected.Equals(p.Code, System.StringComparison.OrdinalIgnoreCase)
                });
            }
            return items;
        }

        public static bool IsSupported(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            foreach (var p in _programs)
            {
                if (p.Code.Equals(code, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}