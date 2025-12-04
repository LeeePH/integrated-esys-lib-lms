using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSITSubjectModels._4thYear
{
    public static class _4thYear2ndSem
    {
        // Internal canonical list; expose as read-only to avoid accidental mutations.
        private static readonly List<SubjectRow> _subjects = new()
        {
            new() {
    Code = "CAP102",
    Title = "Capstone Project and Research 2",
    Units = 3,
    PreRequisite = "CAP101",
    AvailableInSemesters = "2nd Semester",
    AvailableForYearLevels = "4th Year",
    AllowRetake = true
},
            new() {
    Code = "PRC102",
    Title = "Practicum 2",
    Units = 3,
    PreRequisite = "PRC101",
    AvailableInSemesters = "2nd Semester",
    AvailableForYearLevels = "4th Year",
    AllowRetake = true
},
                        new() {
    Code = "SAM101",
    Title = "Systems Administration and Maintenance",
    Units = 3,
    PreRequisite = "IAS102",
    AvailableInSemesters = "2nd Semester",
    AvailableForYearLevels = "4th Year",
    AllowRetake = true
}
           
        };

        // Read-only view if you need to inspect without creating a ViewModel.
        public static ReadOnlyCollection<SubjectRow> Subjects => _subjects.AsReadOnly();

        // Helper to build the senior VM for this semester.
        public static FreshmenSubjectsViewModel Build(FreshmenInfoModel info)
        {
            // Clone to keep the canonical list immutable in the view.
            var list = _subjects
                .Select(s => new SubjectRow
                {
                    Code = s.Code,
                    Title = s.Title,
                    Units = s.Units
                })
                .ToList();

            return new FreshmenSubjectsViewModel
            {
                Info = info,
                Subjects = list,
                TotalUnits = list.Sum(s => s.Units),
                YearLevel = "4th Year",
                Semester = "2nd Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}
