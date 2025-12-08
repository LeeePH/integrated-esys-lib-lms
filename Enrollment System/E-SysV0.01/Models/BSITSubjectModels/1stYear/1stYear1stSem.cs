using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSITSubjectModels._1stYear
{
    public static class _1stYear1stSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "CC102",
                Title = "Fundamentals of Programming",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "GEE1",
                Title = "Gender and Society",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "CC101",
                Title = "Introduction to Computing",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "MATH1",
                Title = "Mathematics in the Modern World",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "NSTP1",
                Title = "National Service Training Program 1",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "GEE2",
                Title = "People and the Earth's Ecosystems",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "PE1",
                Title = "Physical Fitness and Wellness",
                Units = 2,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "WS101",
                Title = "Web Systems and Technologies 1 (Electives)",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
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
                YearLevel = "1st Year",
                Semester = "1st Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}