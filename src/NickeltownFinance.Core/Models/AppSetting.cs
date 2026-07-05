namespace NickeltownFinance.Core.Models;

public class AppSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
