using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSENTSubjectModels._3rdYear
{
    public static class _3rdYear1stSem
    {
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
                Code = "ECC13",
                Title = "Business Plan Implementation 1: Product Development and Market Analysis",
                Units = 5,
                PreRequisite = "ECC10",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "3rd Year",
                AllowRetake = true
            },
            new() {
                Code = "EC6",
                Title = "Franchising",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "3rd Year",
                AllowRetake = true
            },
            new() {
                Code = "EC9",
                Title = "Managing a Manufacturing Enterprise",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "3rd Year",
                AllowRetake = true
            },
            new() {
                Code = "EC8",
                Title = "Micro-financing",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "3rd Year",
                AllowRetake = true
            },
            new() {
                Code = "EC7",
                Title = "Supply Chain Management",
                Units = 3,
                PreRequisite = "ECC3 ",
                AvailableInSemesters = "1st Semester",
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
                Semester = "1st Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}