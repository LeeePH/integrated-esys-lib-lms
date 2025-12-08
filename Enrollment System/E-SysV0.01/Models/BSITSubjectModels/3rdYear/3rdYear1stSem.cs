using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSITSubjectModels._3rdYear
{
    public static class _3rdYear1stSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "AR101",
                Title = "Architecture and Organization",
                Units = 3,
                PreRequisite = "CC103",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "MS101",
                Title = "Discrete Mathematics",
                Units = 3,
                PreRequisite = "CC104,PF101",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "IPT102",
                Title = "Integrative Programming and Technologies 2 (Electives)",
                Units = 3,
                PreRequisite = "IPT101",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "SPI101",
                Title = "Social Professional Issues 1",
                Units = 3,
                PreRequisite = "SE101",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "SIA101",
                Title = "Systems Integration and Architecture 1",
                Units = 3,
                PreRequisite = "SE101",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "SOCSCI3",
                Title = "The Contemporary World",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "RIZAL",
                Title = "The Life and Works of Rizal",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
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
                Semester = "1st Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}