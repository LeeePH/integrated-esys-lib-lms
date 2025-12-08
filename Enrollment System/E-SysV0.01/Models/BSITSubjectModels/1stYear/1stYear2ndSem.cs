using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace E_SysV0._01.Models.BSITSubjectModels._1stYear
{
    public static class _1stYear2ndSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "CC103",
                Title = "Intermediate Programming",
                Units = 3,
                PreRequisite = "CC101, CC102",
                AvailableInSemesters = "2nd Semester",
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
                Code = "NET101",
                Title = "Networking 1",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "2nd Semester",
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
                Code = "PT101",
                Title = "Platform Technologies (Electives)",
                Units = 3,
                PreRequisite = "CC101, CC102",
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
                Code = "SCI1",
                Title = "Science, Technology and Society",
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
                    Room = s.Room,
                    Schedule = s.Schedule,
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