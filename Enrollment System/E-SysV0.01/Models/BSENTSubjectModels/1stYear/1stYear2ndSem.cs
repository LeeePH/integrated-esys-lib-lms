using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSENTSubjectModels._1stYear
{
    public static class _1stYear2ndSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "HUM2",
                Title = "Ethics",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "EC2",
                Title = "Financial Accounting and Reporting 1",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ECC3",
                Title = "Innovation Management",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ECC2",
                Title = "Microeconomics",
                Units = 2,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "NSTP2",
                Title = "National Service Training Program 2",
                Units = 3,
                PreRequisite = "NSTP1",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ECC4",
                Title = "Programs and Policies on Enterprise Decelopment",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ENG1",
                Title = "Purposive Communication",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            }, 
            new() {
                Code = "PE2",
                Title = "Rhythmic Activities",
                Units = 2,
                PreRequisite = "PE1",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            }, 
            new() {
                Code = "SOCSCI3",
                Title = "The Contemporary World",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
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
                YearLevel = "1st Year",
                Semester = "2nd Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}