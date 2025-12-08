using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSENTSubjectModels._2ndYear
{
    public static class _2ndYear2ndSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "ECC10",
                Title = "Business Plan Preparation",
                Units = 3,
                PreRequisite = "ECC8",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "EC3",
                Title = "E-Commerce ",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "EC5",
                Title = "Events Management",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ECC9",
                Title = "Human Resource Management",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ECC11",
                Title = "International Business and Trade",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "MATH1",
                Title = "Mathematics in the Modern World",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "CBMEC1",
                Title = "Operations Management (TQM)",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ECC12",
                Title = "Pricing and Costing",
                Units = 3,
                PreRequisite = "EC2",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "PE4 ",
                Title = "Team Sports",
                Units = 3,
                PreRequisite = "PE1",
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