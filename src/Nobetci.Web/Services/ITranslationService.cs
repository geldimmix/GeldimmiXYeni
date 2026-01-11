namespace Nobetci.Web.Services;

public interface ITranslationService
{
    /// <summary>
    /// Translate text from Turkish to English using DeepInfra API
    /// </summary>
    Task<string> TranslateToEnglishAsync(string turkishText);
    
    /// <summary>
    /// Translate text from English to Turkish using DeepInfra API
    /// </summary>
    Task<string> TranslateToTurkishAsync(string englishText);
}

