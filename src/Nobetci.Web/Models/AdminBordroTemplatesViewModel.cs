using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Models;

public class AdminBordroTemplatesViewModel
{
    public List<BordroSabitleriTemplate> SabitTemplates { get; set; } = new();
    public List<UnitTypeTemplate> UnitTypeTemplates { get; set; } = new();
}
