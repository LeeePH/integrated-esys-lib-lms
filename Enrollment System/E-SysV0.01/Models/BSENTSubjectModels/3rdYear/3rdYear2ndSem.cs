using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSENTSubjectModels._3rdYear
{
    public static class _3rdYear2ndSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "ECC15",
                Title = "Social Enterprise",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "3rd Year",
                AllowRetake = true
            },
            new() {
                Code = "CBMEC2",
                Title = "Strategic Management",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "3rd Year",
                AllowRetake = true
            },
            new() {
                Code = "EC101",
                Title = "Wholesale and Retail Sales Management",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "3rd Year",
                AllowRetake = true
            }
        };

        public static ReadOnlyCollection<SubjectRow> Subjects => _subjects.AsReadOnly();

        public static FreshmenSubjectsViewModel Build(FreshmenInfoModel info)
        {
            var list = _subjects
                .Select(s => new SubjectRow
                {
                    Code = s.Code,
                    Title = s.Title,
                    Units = s.Units,
                    PreRequisite = s.PreRequisite,
                    AvailableInSemesters = s.AvailableInSemesters,
                    AvailableForYearLevels = s.AvailableForYearLevels,
                    AllowRetake = s.AllowRetake
                })
                .ToList();

            return new FreshmenSubjectsViewModel
            {
                Info = info,
                Subjects = list,
                TotalUnits = list.Sum(s => s.Units),
                YearLevel = "3rd Year",
                Semester = "2nd Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}