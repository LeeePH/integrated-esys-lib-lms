using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSENTSubjectModels._1stYear
{
    public static class _1stYear1stSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "ECC1",
                Title = "Entrepreneurial Behavior",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "EC1",
                Title = "Entrepreneurial Marketing Strategies",
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
                AvailableInSemesters = "1st Semester",
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
                Code = "GEE3",
                Title = "Philippine Popular Culture",
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
                Code = "SOCSCI2",
                Title = "Readings in Philippine History",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },            new() {
                Code = "SCI1",
                Title = "Science, Technology and Society",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "1st Year, 2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },            new() {
                Code = "SOCSCI1",
                Title = "Understanding the Self",
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