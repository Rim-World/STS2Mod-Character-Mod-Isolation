using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils;

namespace CharacterModIsolation;

internal static class Localization
{
    private const string ResourceFolder = "CharacterModIsolation.localization";

    private static readonly I18N? Texts = Create();

    public static ModSettingsText Text(string key, string fallback) =>
        Texts is null ? ModSettingsText.Literal(fallback) : ModSettingsText.I18N(Texts, key, fallback);

    public static string Resolve(string key, string fallback) =>
        Texts is null ? fallback : Texts.Get(key, fallback);

    private static I18N? Create()
    {
        try
        {
            return new I18N(
                "CharacterModIsolation.texts",
                null,
                [ResourceFolder],
                null,
                typeof(Localization).Assembly,
                null);
        }
        catch (Exception ex)
        {
            if (Plugin.Logger is not null)
                Plugin.Logger.Error($"[i18n] failed to load UI text; using literal fallback: {ex}");
            return null;
        }
    }
}
