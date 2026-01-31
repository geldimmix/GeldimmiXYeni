namespace Nobetci.Web.Models;

public class PayrollOptions
{
    public decimal BaseHourlyRate { get; set; } = 1.0m;
    public decimal OvertimeCoefficient { get; set; } = 1.5m;
    public decimal HolidayCoefficient { get; set; } = 2.0m;
    public decimal WeekendCoefficient { get; set; } = 1.5m;
    public decimal NightCoefficient { get; set; } = 1.25m;
    public decimal OvertimeLimitHours { get; set; } = 130m;
}
