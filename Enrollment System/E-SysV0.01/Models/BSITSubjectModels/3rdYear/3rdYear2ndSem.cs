using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSITSubjectModels._3rdYear
{
    public static class _3rdYear2ndSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "AL101",
                Title = "Algorithms and Complexity",
                Units = 3,
                PreRequisite = "MS101",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "CC106",
                Title = "Application Development and Emerging Technologies",
                Units = 3,
                PreRequisite = "SE101",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "HUM2",
                Title = "Ethics",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "IAS101",
                Title = "Fundamentals of Information Assurance and Security 1",
                Units = 3,
                PreRequisite = "SIA101",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "MS102",
                Title = "Quantitative Methods",
                Units = 3,
                PreRequisite = "MS101",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "SIA102",
                Title = "Systems Integration and Architecture 2 (Electives)",
                Units = 3,
                PreRequisite = "SIA101",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
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