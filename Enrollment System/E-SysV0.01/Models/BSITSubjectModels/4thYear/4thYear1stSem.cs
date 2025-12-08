using System.Collections.ObjectModel;

namespace E_SysV0._01.Models.BSITSubjectModels._4thYear
{
    public static class _4thYear1stSem
    {
        // Internal canonical list; expose as read-only to avoid accidental mutations.
        private static readonly List<SubjectRow> _subjects = new()
        {
            new () {
    Code = "AL102",
    Title = "Automata Theory and Formal Languagee",
    Units = 3,
    PreRequisite = "AL101",
    AvailableInSemesters = "1st Semester",
    AvailableForYearLevels = "4th Year",
    AllowRetake = true
},
new() {
    Code = "CAP101",
    Title = "Capstone Project and Research 1",
    Units = 3,
    PreRequisite = "IAS101, MS102",
    AvailableInSemesters = "1st Semester",
    AvailableForYearLevels = "4th Year",
    AllowRetake = true
},
new() {
    Code = "IAS102",
    Title = "Information Assurance and Security 2",
    Units = 3,
    PreRequisite = "IAS101",
    AvailableInSemesters = "1st Semester",
    AvailableForYearLevels = "4th Year",
    AllowRetake = true
},

        new () {
    Code = "PRC101",
    Title = "Practicum 1",
    Units = 3,
    PreRequisite = "IAS101,CC106",
    AvailableInSemesters = "1st Semester",
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
                Semester = "1st Semester",
                Documents = new FreshmenDocumentStatus()
            };
        }
    }
}
