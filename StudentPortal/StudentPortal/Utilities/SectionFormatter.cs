using System.Linq;

namespace StudentPortal.Utilities
{
    public static class SectionFormatter
    {
        public static string Format(string course, string year, string section)
        {
            var coursePart = NormalizeCourse(course);
            var yearPart = ExtractYearNumber(year);
            var sectionPart = NormalizeSection(section);

            var combined = $"{coursePart}{yearPart}{sectionPart}";
            return string.IsNullOrWhiteSpace(combined) ? string.Empty : combined;
        }

        private static string NormalizeCourse(string course)
        {
            return string.IsNullOrWhiteSpace(course)
                ? string.Empty
                : new string(course.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        private static string ExtractYearNumber(string year)
        {
            if (string.IsNullOrWhiteSpace(year))
                return string.Empty;

            var digits = new string(year.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digits))
                return digits;

            // fallback for textual years
            return year.Trim().ToUpperInvariant() switch
            {
                "FIRST" => "1",
                "SECOND" => "2",
                "THIRD" => "3",
                "FOURTH" => "4",
                _ => string.Empty
            };
        }

        private static string NormalizeSection(string section)
        {
            return string.IsNullOrWhiteSpace(section)
                ? string.Empty
                : new string(section.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }
    }
}

