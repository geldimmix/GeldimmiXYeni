using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Models;

public class PuantajDetailViewModel
{
    public Employee Employee { get; set; } = null!;
    public EmployeePayroll Payroll { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public string DataSource { get; set; } = "shift";
}
