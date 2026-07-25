using System.Globalization;

namespace OEEMonitoringSystem.Services.Interfaces
{
    public interface ITimezoneService
    {
        DateTime ConvertUtcToUserTime(DateTime utcDateTime);
        DateTime ConvertUserTimeToUtc(DateTime userDateTime);
        string GetUserTimezoneId();
        TimeZoneInfo GetUserTimezone();
        string FormatDateTimeForDisplay(DateTime dateTime);
        string FormatDateForDisplay(DateTime date);
        string FormatTimeForDisplay(DateTime time);
    }
}