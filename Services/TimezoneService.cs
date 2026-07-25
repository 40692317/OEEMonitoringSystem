using OEEMonitoringSystem.Services.Interfaces;

namespace OEEMonitoringSystem.Services
{
    public class TimezoneService : ITimezoneService
    {
        public DateTime ConvertUtcToUserTime(DateTime utcDateTime)
        {
            if (utcDateTime.Kind == DateTimeKind.Unspecified)
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            return utcDateTime.ToUniversalTime();
        }

        public DateTime ConvertUserTimeToUtc(DateTime userDateTime)
        {
            if (userDateTime.Kind == DateTimeKind.Unspecified)
                return DateTime.SpecifyKind(userDateTime, DateTimeKind.Utc);
            return userDateTime.ToUniversalTime();
        }

        public string GetUserTimezoneId() => "UTC";

        public TimeZoneInfo GetUserTimezone() => TimeZoneInfo.Utc;

        public string FormatDateTimeForDisplay(DateTime dateTime) =>
            ConvertUtcToUserTime(dateTime).ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        public string FormatDateForDisplay(DateTime date) =>
            ConvertUtcToUserTime(date).ToString("yyyy-MM-dd");

        public string FormatTimeForDisplay(DateTime time) =>
            ConvertUtcToUserTime(time).ToString("HH:mm:ss") + " UTC";
    }
}
