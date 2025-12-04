using System;
using System.Threading.Tasks;
using E_SysV0._01.Hubs;
using E_SysV0._01.Models;
using E_SysV0._01.Services;
using Microsoft.AspNetCore.SignalR;
namespace E_SysV0._01.Services
{
    public class EnrollmentCycleService
    {
        private readonly MongoDBServices _db; private readonly IHubContext<AdminNotificationsHub> _hub;
        public EnrollmentCycleService(MongoDBServices db, IHubContext<AdminNotificationsHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        public async Task NormalizeAsync()
        {
            var settings = await _db.GetEnrollmentSettingsAsync();
            var now = DateTime.UtcNow;
            var changed = false;

            // Auto-close + auto-start semester at enrollment close
            if (settings.IsOpen && settings.ClosesAtUtc.HasValue && now >= settings.ClosesAtUtc.Value)
            {
                settings.IsOpen = false;
                if (string.Equals(settings.Semester, "1st Semester", StringComparison.OrdinalIgnoreCase))
                {
                    if (settings.Semester1StartedAtUtc == null)
                    {
                        var start = settings.ClosesAtUtc!.Value;
                        settings.Semester1StartedAtUtc = start;
                        var months = Math.Max(0, settings.Semester1PlannedMonths ?? 0);
                        var secs = Math.Max(0L, settings.Semester1PlannedDurationSeconds ?? 0L);
                        settings.Semester1EndsAtUtc = start.AddMonths(months).AddSeconds(secs);
                    }
                }
                else if (string.Equals(settings.Semester, "2nd Semester", StringComparison.OrdinalIgnoreCase))
                {
                    if (settings.Semester2StartedAtUtc == null)
                    {
                        var start = settings.ClosesAtUtc!.Value;
                        settings.Semester2StartedAtUtc = start;
                        var months = Math.Max(0, settings.Semester2PlannedMonths ?? 0);
                        var secs = Math.Max(0L, settings.Semester2PlannedDurationSeconds ?? 0L);
                        settings.Semester2EndsAtUtc = start.AddMonths(months).AddSeconds(secs);
                    }
                }
                changed = true;
            }

            // 1st -> 2nd when 1st semester ends
            if (string.Equals(settings.Semester, "1st Semester", StringComparison.OrdinalIgnoreCase) &&
                settings.Semester1EndsAtUtc.HasValue && now >= settings.Semester1EndsAtUtc.Value)
            {
                settings.Semester = "2nd Semester";
                settings.IsOpen = false;
                settings.OpenedAtUtc = null;
                settings.ClosesAtUtc = null;
                settings.OpenDurationSeconds = null;
                changed = true;
            }

            // 2nd -> AY+1 and back to 1st when 2nd semester ends
            if (string.Equals(settings.Semester, "2nd Semester", StringComparison.OrdinalIgnoreCase) &&
                settings.Semester2EndsAtUtc.HasValue && now >= settings.Semester2EndsAtUtc.Value)
            {
                settings.AcademicYear = IncrementAcademicYear(settings.AcademicYear);
                settings.Semester = "1st Semester";
                settings.IsOpen = false;
                settings.OpenedAtUtc = null;
                settings.ClosesAtUtc = null;
                settings.OpenDurationSeconds = null;

                // Clear runtime and planned values to prepare next cycle
                settings.Semester1StartedAtUtc = null;
                settings.Semester1EndsAtUtc = null;
                settings.Semester2StartedAtUtc = null;
                settings.Semester2EndsAtUtc = null;
                settings.Semester1PlannedMonths = null;
                settings.Semester1PlannedDurationSeconds = null;
                settings.Semester2PlannedMonths = null;
                settings.Semester2PlannedDurationSeconds = null;
                changed = true;
            }

            if (changed)
            {
                await _db.UpsertEnrollmentSettingsAsync(settings);
                await BroadcastChangeAsync(settings);
            }
        }

        public async Task OpenEnrollmentAndPlanSemesterAsync(string semester, long enrollSeconds, int plannedMonths, long plannedSeconds)
        {
            var settings = await _db.GetEnrollmentSettingsAsync();
            if (!string.Equals(settings.Semester, semester, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Cannot configure inactive semester. Active: {settings.Semester}.");

            if ((semester == "1st Semester" && settings.Semester1StartedAtUtc != null) ||
                (semester == "2nd Semester" && settings.Semester2StartedAtUtc != null))
                throw new InvalidOperationException("Semester already started. Cannot change enrollment or semester duration.");

            if (enrollSeconds <= 0)
                throw new InvalidOperationException("Enrollment duration must be greater than zero.");

            if (plannedMonths == 0 && plannedSeconds <= 0)
                throw new InvalidOperationException("Semester duration must be greater than zero.");

            var now = DateTime.UtcNow;
            settings.Semester = semester;
            settings.IsOpen = true;
            settings.OpenedAtUtc = now;
            settings.OpenDurationSeconds = enrollSeconds;
            settings.ClosesAtUtc = now.AddSeconds(enrollSeconds);

            if (semester == "1st Semester")
            {
                settings.Semester1PlannedMonths = plannedMonths;
                settings.Semester1PlannedDurationSeconds = plannedSeconds;
            }
            else
            {
                settings.Semester2PlannedMonths = plannedMonths;
                settings.Semester2PlannedDurationSeconds = plannedSeconds;
            }

            await _db.UpsertEnrollmentSettingsAsync(settings);
            await BroadcastChangeAsync(settings);
        }

        public async Task ResetCycleAsync()
        {
            var settings = await _db.GetEnrollmentSettingsAsync();

            settings.IsOpen = false;
            settings.OpenedAtUtc = null;
            settings.ClosesAtUtc = null;
            settings.OpenDurationSeconds = null;

            settings.Semester = "1st Semester";

            settings.Semester1PlannedMonths = null;
            settings.Semester1PlannedDurationSeconds = null;
            settings.Semester2PlannedMonths = null;
            settings.Semester2PlannedDurationSeconds = null;

            settings.Semester1StartedAtUtc = null;
            settings.Semester1EndsAtUtc = null;
            settings.Semester2StartedAtUtc = null;
            settings.Semester2EndsAtUtc = null;

            await _db.UpsertEnrollmentSettingsAsync(settings);
            await BroadcastChangeAsync(settings);
        }

        private static string IncrementAcademicYear(string ay)
        {
            var t = (ay ?? "").Trim();
            if (t.Length >= 9 && int.TryParse(t[..4], out var y1))
            {
                var y2 = y1 + 1;
                return $"{y2}-{y2 + 1}";
            }
            var now = DateTime.UtcNow.Year;
            return $"{now}-{now + 1}";
        }

        private Task BroadcastChangeAsync(EnrollmentSettings s) =>
            _hub.Clients.Group("Admins").SendAsync("EnrollmentSettingsChanged", new
            {
                semester = s.Semester,
                isOpen = s.IsOpen,
                openedAtUtc = s.OpenedAtUtc?.ToString("o"),
                closesAtUtc = s.ClosesAtUtc?.ToString("o"),
                s1Start = s.Semester1StartedAtUtc?.ToString("o"),
                s1End = s.Semester1EndsAtUtc?.ToString("o"),
                s2Start = s.Semester2StartedAtUtc?.ToString("o"),
                s2End = s.Semester2EndsAtUtc?.ToString("o"),
                ay = s.AcademicYear
            });
    }
}