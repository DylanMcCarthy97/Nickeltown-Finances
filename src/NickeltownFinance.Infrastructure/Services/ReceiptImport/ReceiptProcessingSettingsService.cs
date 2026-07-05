using NickeltownFinance.Core.Constants;
using NickeltownFinance.Core.DTOs;
using NickeltownFinance.Core.Interfaces;

namespace NickeltownFinance.Infrastructure.Services.ReceiptImport;

public sealed class ReceiptProcessingSettingsService : IReceiptProcessingSettingsService
{
    private readonly ISettingsRepository _settings;

    public ReceiptProcessingSettingsService(ISettingsRepository settings) => _settings = settings;

    public ReceiptProcessingSettingsInfo GetSettings() => new()
    {
        AutoImageEnhancement = GetBool(SettingKeys.ReceiptAutoEnhancement, defaultValue: true),
        OcrEnabled = GetBool(SettingKeys.ReceiptOcrEnabled, defaultValue: true),
        AiCategorisation = GetBool(SettingKeys.ReceiptAiCategorisation, defaultValue: true),
        BankMatching = GetBool(SettingKeys.ReceiptBankMatching, defaultValue: true),
        DuplicateDetection = GetBool(SettingKeys.ReceiptDuplicateDetection, defaultValue: true),
        ThumbnailGeneration = GetBool(SettingKeys.ReceiptThumbnailGeneration, defaultValue: true)
    };

    public void SaveSettings(ReceiptProcessingSettingsInfo settings)
    {
        _settings.SetValue(SettingKeys.ReceiptAutoEnhancement, settings.AutoImageEnhancement);
        _settings.SetValue(SettingKeys.ReceiptOcrEnabled, settings.OcrEnabled);
        _settings.SetValue(SettingKeys.ReceiptAiCategorisation, settings.AiCategorisation);
        _settings.SetValue(SettingKeys.ReceiptBankMatching, settings.BankMatching);
        _settings.SetValue(SettingKeys.ReceiptDuplicateDetection, settings.DuplicateDetection);
        _settings.SetValue(SettingKeys.ReceiptThumbnailGeneration, settings.ThumbnailGeneration);
    }

    private bool GetBool(string key, bool defaultValue) =>
        _settings.GetValue<bool?>(key) ?? defaultValue;
}
