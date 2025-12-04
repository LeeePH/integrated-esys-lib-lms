using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSITSubjectModels._2ndYear
{
    public static class _2ndYear2ndSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "IM101",
                Title = "Advanced Database Systems",
                Units = 3,
                PreRequisite = "CC105,PF101,IS104",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "IPT101",
                Title = "Integrative Programming and Technologies 1",
                Units = 3,
                PreRequisite = "PT101,PF101",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "HCI101",
                Title = "Introduction to Human Computer Interaction",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "SOCSCI2",
                Title = "Readings in Philippine History",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "SE101",
                Title = "Software Engineering",
                Units = 3,
                PreRequisite = "CC105,PF101,IS104",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "PE4",
                Title = "Team Sports",
                Units = 2,
                PreRequisite = "PE1",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "SOCSCI1",
                Title = "Understanding the Self",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
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
                YearLevel = "2nd Year",
                Semester = "2nd Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}