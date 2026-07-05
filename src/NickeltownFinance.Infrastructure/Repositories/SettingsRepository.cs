using System.Text.Json;
using NickeltownFinance.Core.Interfaces;
using NickeltownFinance.Core.Models;
using NickeltownFinance.Infrastructure.Database;

namespace NickeltownFinance.Infrastructure.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly LiteDbContext _context;

    public SettingsRepository(LiteDbContext context) => _context = context;

    public string? GetValue(string key) =>
        _context.Settings.FindOne(x => x.Key == key)?.Value;

    public void SetValue(string key, string value)
    {
        var existing = _context.Settings.FindOne(x => x.Key == key);
        if (existing is null)
        {
            _context.Settings.Insert(new AppSetting
            {
                Key = key,
                Value = value,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            });
            return;
        }

        existing.Value = value;
        existing.ModifiedDate = DateTime.UtcNow;
        _context.Settings.Update(existing);
    }

    public T? GetValue<T>(string key)
    {
        var value = GetValue(key);
        if (string.IsNullOrEmpty(value)) return default;
        return JsonSerializer.Deserialize<T>(value);
    }

    public void SetValue<T>(string key, T value) =>
        SetValue(key, JsonSerializer.Serialize(value));
}
