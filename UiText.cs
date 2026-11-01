namespace LedControl;

internal enum AppLanguage
{
    English,
    Russian
}

internal static class UiText
{
    public static AppLanguage Language { get; private set; } = AppLanguage.Russian;

    public static void Use(AppLanguage language) => Language = language;

    public static string T(string english, string russian) =>
        Language == AppLanguage.English ? english : russian;
}
