
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using E_SysV0._01.Models;
using E_SysV0._01.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace E_SysV0._01.Services
{
    public class RegistrationSlipPdfService
    {
        private readonly MongoDBServices _db;

        public RegistrationSlipPdfService(MongoDBServices db)
        {
            _db = db;
        }

        // allow caller to choose whether to include the "Generated" footer (default preserves existing behavior)
        public async Task<byte[]> GenerateForRequestAsync(string requestId, bool includeGeneratedFooter = true)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException("requestId is required.", nameof(requestId));

            var vm = await BuildViewModelAsync(requestId);
            return GeneratePdf(vm, includeGeneratedFooter);
        }

        private async Task<AdminRegistrationSlipViewModel> BuildViewModelAsync(string requestId)
        {
            var req = await _db.GetEnrollmentRequestByIdAsync(requestId)
                      ?? throw new InvalidOperationException("Request not found.");

            var student = await _db.GetStudentByEmailAsync(req.Email)
                         ?? throw new InvalidOperationException("Student not found for this request.");

            var sse = await _db.GetStudentSectionEnrollmentAsync(student.Username)
                      ?? throw new InvalidOperationException("Section enrollment not found.");

            var section = await _db.GetSectionByIdAsync(sse.SectionId);

            // Defensive: ensure meetings list is non-null before using LINQ on it
            var meetings = (await _db.GetStudentScheduleAsync(student.Username)) ?? new List<ClassMeeting>();

            var roomNames = await _db.GetRoomNamesByIdsAsync(
                (meetings.Select(m => m.RoomId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct()) // avoid nulls
            );

            // Determine program to use for subject lookup.
            // Prefer the section program, then the request.Program, then fall back to BSIT.
            var program = (section?.Program ?? req.Program ?? "").Trim();
            if (string.IsNullOrWhiteSpace(program)) program = "BSIT";

            // Determine year/semester for correct subject model (falls back to 1st Year / 1st Semester)
            var extra = req.ExtraFields ?? new Dictionary<string, string>();
            var yearLevel = extra.TryGetValue("Academic.YearLevel", out var yl) && !string.IsNullOrWhiteSpace(yl) ? yl : "1st Year";
            var semester = extra.TryGetValue("Academic.Semester", out var sem) && !string.IsNullOrWhiteSpace(sem) ? sem : "1st Semester";

            // build subject lookup for the chosen program and year/semester (defensive)
            var subjectDict = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var subjectsForLookup = GetSubjectsForYearAndSemester(program, yearLevel, semester);
                foreach (var s in subjectsForLookup)
                {
                    if (!string.IsNullOrWhiteSpace(s.Code))
                        subjectDict[s.Code] = (s.Title, s.Units);
                }
            }
            catch
            {
                // Defensive: if subject model lookup fails for any reason, continue with empty dictionary.
            }

            static string DayName(int d) => d switch
            {
                0 => "Sun",
                1 => "Mon",
                2 => "Tue",
                3 => "Wed",
                4 => "Thu",
                5 => "Fri",
                6 => "Sat",
                _ => "Day"
            };

            var subjects = new List<AdminRegistrationSlipSubject>();
            foreach (var m in meetings)
            {
                var code = m.CourseCode ?? "";
                subjectDict.TryGetValue(code, out var meta);
                subjects.Add(new AdminRegistrationSlipSubject
                {
                    Code = code,
                    Title = string.IsNullOrWhiteSpace(meta.Title) ? code : meta.Title,
                    Units = meta.Units,
                    Room = roomNames.TryGetValue(m.RoomId ?? "", out var rn) ? rn : (m.RoomId ?? ""),
                    Schedule = $"{DayName(m.DayOfWeek)} {(string.IsNullOrWhiteSpace(m.DisplayTime) ? "" : m.DisplayTime)}".Trim()
                });
            }

            string G(string p, string n)
            {
                var key = string.IsNullOrEmpty(p) ? n : $"{p}.{n}";
                return extra.TryGetValue(key, out var v) ? v : "";
            }

            // Treat Transferee or any request type containing "Irregular" as Irregular on slip
            bool isIrregular = req.Type != null &&
                                (req.Type.Contains("Irregular", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(req.Type, "Transferee", StringComparison.OrdinalIgnoreCase));

            // Build and return view model (include StudentNumber)
            var vm = new AdminRegistrationSlipViewModel
            {
                Program = section?.Program ?? (req.Program ?? ""),
                YearLevel = G("Academic", "YearLevel"),
                Semester = G("Academic", "Semester"),
                SectionName = section?.Name ?? "",
                Regularity = isIrregular ? "Irregular" : "Regular",
                GraduatingStatus = "Not Graduating",
                LastName = G("Student", "LastName"),
                FirstName = G("Student", "FirstName"),
                MiddleName = G("Student", "MiddleName"),
                StudentNumber = student.Username ?? "",
                DateEnrolledUtc = sse.EnrolledAt,
                Subjects = subjects,
                DeanName = "Engr. Juan Dela Cruz",
                RegistrationDateUtc = DateTime.UtcNow,
                RequestId = req.Id
            };

            return vm;
        }

        // Copied/adapted subject selection logic so PDF titles match the correct year/semester models
        private List<SubjectRow> GetSubjectsForYearAndSemester(string program, string yearLevel, string semester)
        {
            var prog = (program ?? "BSIT").Trim();
            prog = prog.ToUpperInvariant();
            if (!prog.Contains("BSENT", StringComparison.OrdinalIgnoreCase) && !prog.Contains("BSIT", StringComparison.OrdinalIgnoreCase))
            {
                // fallback to catalog check
                if (ProgramCatalog.All.Any(x => string.Equals(x.Code, prog, StringComparison.OrdinalIgnoreCase)))
                    prog = ProgramCatalog.All.First(x => string.Equals(x.Code, prog, StringComparison.OrdinalIgnoreCase)).Code;
                else
                    prog = "BSIT";
            }

            var year = EnrollmentRules.ParseYearLevel(yearLevel ?? "1st Year");
            var isBSENT = string.Equals(prog, "BSENT", StringComparison.OrdinalIgnoreCase);
            var is1stSem = (semester ?? "").Contains("1st", StringComparison.OrdinalIgnoreCase);

            if (!isBSENT)
            {
                return year switch
                {
                    1 when is1stSem => E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects.ToList(),
                    1 => E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList(),
                    2 when is1stSem => E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList(),
                    2 => E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList(),
                    3 when is1stSem => E_SysV0._01.Models.BSITSubjectModels._3rdYear._3rdYear1stSem.Subjects.ToList(),
                    3 => E_SysV0._01.Models.BSITSubjectModels._3rdYear._3rdYear2ndSem.Subjects.ToList(),
                    4 when is1stSem => E_SysV0._01.Models.BSITSubjectModels._4thYear._4thYear1stSem.Subjects.ToList(),
                    4 => E_SysV0._01.Models.BSITSubjectModels._4thYear._4thYear2ndSem.Subjects.ToList(),
                    _ => E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects.ToList()
                };
            }

            return year switch
            {
                1 when is1stSem => E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects.ToList(),
                1 => E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList(),
                2 when is1stSem => E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList(),
                2 => E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList(),
                _ => E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects.ToList()
            };
        }

        private static byte[] GeneratePdf(AdminRegistrationSlipViewModel m, bool includeGeneratedFooter)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(t => t.FontSize(10));

                    page.Header().Element(header =>
                    {
                        header.Column(col =>
                        {
                            col.Item().AlignCenter().Text("Registration Form").SemiBold().FontSize(14);
                            col.Item().AlignCenter().Text($"Academic: {m.YearLevel} • {m.Semester}").Light();
                        });
                    });

                    page.Content().PaddingTop(5).Column(col =>
                    {
                        col.Item().Grid(grid =>
                        {
                            grid.Spacing(4);
                            grid.Columns(4);
                            grid.Item(1).Text(text =>
                            {
                                text.Span("Course/Program: ").Light();
                                text.Span(m.Program).SemiBold();
                            });
                            grid.Item(1).Text(text => { text.Span("Year Level: ").Light(); text.Span(m.YearLevel).SemiBold(); });
                            grid.Item(1).Text(text => { text.Span("Section: ").Light(); text.Span(m.SectionName).SemiBold(); });
                            grid.Item(1).Text(text => { text.Span("Regularity: ").Light(); text.Span(m.Regularity).SemiBold(); });

                            grid.Item(1).Text(text => { text.Span("Graduating: ").Light(); text.Span(m.GraduatingStatus).SemiBold(); });
                            grid.Item(1).Text(text => { text.Span("Date Enrolled: ").Light(); text.Span(m.DateEnrolledLocal.ToString("MMM dd, yyyy")).SemiBold(); });
                            grid.Item(1).Text(text => { text.Span("Date of Registration: ").Light(); text.Span(m.RegistrationDateLocal.ToString("MMM dd, yyyy")).SemiBold(); });
                        });

                        col.Item().PaddingTop(6).Grid(grid =>
                        {
                            grid.Spacing(4);
                            grid.Columns(4);
                            grid.Item(1).Text(text => { text.Span("Last Name: ").Light(); text.Span(m.LastName).SemiBold(); });
                            grid.Item(1).Text(text => { text.Span("First Name: ").Light(); text.Span(m.FirstName).SemiBold(); });
                            grid.Item(1).Text(text => { text.Span("Middle Name: ").Light(); text.Span(m.MiddleName).SemiBold(); });
                            grid.Item(1).Text(text => { text.Span("Student Number: ").Light(); text.Span(m.StudentNumber).SemiBold(); });
                        });

                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(12);   // code
                                cols.RelativeColumn(40);   // title
                                cols.RelativeColumn(8);    // units
                                cols.RelativeColumn(20);   // room
                                cols.RelativeColumn(20);   // schedule
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(CellHeader).Text("Subject Code");
                                h.Cell().Element(CellHeader).Text("Subject Name");
                                h.Cell().Element(CellHeader).Text("Units");
                                h.Cell().Element(CellHeader).Text("Room");
                                h.Cell().Element(CellHeader).Text("Time/Schedule");
                                static IContainer CellHeader(IContainer c) => c
                                    .DefaultTextStyle(t => t.SemiBold().FontSize(9))
                                    .PaddingVertical(4).PaddingHorizontal(6)
                                    .Background(Colors.Grey.Lighten3)
                                    .Border(1).BorderColor(Colors.Grey.Lighten1);
                            });

                            var rows = (m.Subjects != null && m.Subjects.Count > 0)
                                ? m.Subjects
                                : new List<AdminRegistrationSlipSubject>
                                  {
                                      new AdminRegistrationSlipSubject
                                      {
                                          Code = "",
                                          Title = "No classes scheduled.",
                                          Units = 0,
                                          Room = "",
                                          Schedule = ""
                                      }
                                  };

                            foreach (var s in rows)
                            {
                                table.Cell().Element(CellBody).Text(s.Code);
                                table.Cell().Element(CellBody).Text(s.Title);
                                table.Cell().Element(CellBody).Text(s.Units == 0 ? "" : s.Units.ToString());
                                table.Cell().Element(CellBody).Text(s.Room);
                                table.Cell().Element(CellBody).Text(s.Schedule);

                                static IContainer CellBody(IContainer c) => c
                                    .PaddingVertical(4).PaddingHorizontal(6)
                                    .Border(1).BorderColor(Colors.Grey.Lighten2);
                            }
                        });

                        col.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem().Column(cc =>
                            {
                                cc.Item().PaddingTop(18).LineHorizontal(1).LineColor(Colors.Grey.Darken3);
                                cc.Item().AlignCenter().Text("Student Signature").SemiBold().FontSize(9);
                                cc.Item().AlignCenter().Text("Sign over printed name").Light().FontSize(8);
                            });
                            row.RelativeItem().Column(cc =>
                            {
                                cc.Item().PaddingTop(18).LineHorizontal(1).LineColor(Colors.Grey.Darken3);
                                cc.Item().AlignCenter().Text(m.DeanName).SemiBold().FontSize(9);
                                cc.Item().AlignCenter().Text("Dean").Light().FontSize(8);
                            });
                        });
                    });

                    // include footer only when requested
                    if (includeGeneratedFooter)
                    {
                        page.Footer().AlignRight().Text(text =>
                        {
                            text.Span("Generated: ").Light();
                            text.Span(DateTime.Now.ToString("g"));
                        });
                    }
                });
            }).GeneratePdf();

            return bytes;
        }
    }
}