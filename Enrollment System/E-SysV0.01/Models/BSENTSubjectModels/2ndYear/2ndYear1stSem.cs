using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSENTSubjectModels._2ndYear
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
                Code = "ECC6",
                Title = "Business Law and Taxation, with focus on Laws affecting MSME’s",
                Units = 3,
                PreRequisite = "EC2",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ECC5",
                Title = "Financial Management ",
                Units = 3,
                PreRequisite = "EC2",
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
                Code = "EC4",
                Title = "Managing of a Service Enterprise",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ECC7",
                Title = "Market Research and Consumer Behavior",
                Units = 3,
                PreRequisite = "EC1",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "ECC8",
                Title = "Opportunity Seeking",
                Units = 3,
                PreRequisite = "ECC1 ",
                AvailableInSemesters = "1st Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "GEE2",
                Title = "People and the Earth’s Ecosystems",
                Units = 3,
                PreRequisite = "",
                AvailableInSemesters = "1st Semester, 2nd Semester",
                AvailableForYearLevels = "2nd Year, 3rd Year, 4th Year",
                AllowRetake = true
            },
            new() {
                Code = "RIZAL",
                Title = "The Life and Works of Rizal",
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
                Semester = "1st Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}