namespace Nobetci.Web.Models;

/// <summary>
/// Shared model for the App sidebar used on Index, Payroll, Attendance pages.
/// </summary>
public class AppSidebarViewModel
{
    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
    public int? SelectedUnitId { get; set; }
    public int EmployeeCount { get; set; }
    public int HolidayCount { get; set; }
    public int UnitCount { get; set; }
    public bool CanAccessPayroll { get; set; }
    public bool CanAccessAttendance { get; set; }
    public bool CanManageUnits { get; set; }
    public bool IsRegistered { get; set; }
    public bool IsPremium { get; set; }
    public int EmployeeLimit { get; set; }
    public int UnitLimit { get; set; }
    public int TotalEmployeeCount { get; set; }
    /// <summary>Current page: "Index", "Payroll", or "Attendance" - for active state</summary>
    public string CurrentPage { get; set; } = "Index";
    /// <summary>When true, modal buttons work (Index). When false, they become links to Index (Payroll/Attendance).</summary>
    public bool HasModals => CurrentPage == "Index";
}
