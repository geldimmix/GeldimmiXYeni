using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Models;

public class BordroSabitlerViewModel
{
    public List<BordroSabitleri> Sabitler { get; set; } = new();
    public BordroSabitInputModel NewSabit { get; set; } = new();
    public List<UnitType> UnitTypes { get; set; } = new();
    public bool HasSabitTemplateUpdates { get; set; }
    public int SabitTemplateUpdateCount { get; set; }
    public bool HasUnitTypeTemplateUpdates { get; set; }
    public int UnitTypeTemplateUpdateCount { get; set; }
    public List<string> SabitTemplateUpdateDetails { get; set; } = new();
    public List<string> UnitTypeTemplateUpdateDetails { get; set; } = new();
}

public class BordroSabitInputModel
{
    public int? Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string ValueType { get; set; } = "ORAN";
    public string? Description { get; set; }
    public string? CadreType { get; set; }
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string? WorkingUnitIds { get; set; }
}
