using System.Collections.ObjectModel;
using System.Linq;

namespace E_SysV0._01.Models.BSITSubjectModels._2ndYear
{
    public static class _2ndYear1stSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "HUM1",
                Title = "Art Appreciation",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "CC104",
                Title = "Data Structures and Algorithms",
                Units = 3,
                PreRequisite = "CC103",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "PE3",
                Title = "Individual and Dual Sports",
                Units = 2,
                PreRequisite = "PE1",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "CC105",
                Title = "Information Management",
                Units = 3,
                PreRequisite = "CC103",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "NET102",
                Title = "Networking 2",
                Units = 3,
                PreRequisite = "NET101, PT101",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "PF101",
                Title = "Object Oriented Programming",
                Units = 3,
                PreRequisite = "CC103, PT101",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "IS104",
                Title = "System Analysis and Design",
                Units = 3,
                PreRequisite = "CC103",
                AvailableInSemesters = "1st Semester",
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
                Semester = "1st Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}