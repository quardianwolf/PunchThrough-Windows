using System.Globalization;

namespace PunchThrough.Models;

public enum AppLanguage
{
    System,
    English,
    Turkish,
    French
}

public static class AppLanguageExtensions
{
    public static string GetCode(this AppLanguage lang) => lang switch
    {
        AppLanguage.English => "en",
        AppLanguage.Turkish => "tr",
        AppLanguage.French => "fr",
        _ => ""
    };

    public static string GetDisplayName(this AppLanguage lang) => lang switch
    {
        AppLanguage.System => Strings.Get("SystemDefault"),
        AppLanguage.English => "English",
        AppLanguage.Turkish => "Turkce",
        AppLanguage.French => "Francais",
        _ => Strings.Get("SystemDefault")
    };

    public static CultureInfo GetCulture(this AppLanguage lang)
    {
        var code = lang.GetCode();
        return string.IsNullOrEmpty(code)
            ? CultureInfo.InstalledUICulture
            : new CultureInfo(code);
    }

    public static void Apply(this AppLanguage lang)
    {
        var culture = lang.GetCulture();
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Strings.SetCulture(culture);
    }

    public static AppLanguage Current()
    {
        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return code switch
        {
            "en" => AppLanguage.English,
            "tr" => AppLanguage.Turkish,
            "fr" => AppLanguage.French,
            _ => AppLanguage.System
        };
    }
}
