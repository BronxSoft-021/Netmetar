// Language of everything the user sees: Serbian (default) or English, chosen in the settings.
namespace Netmetar;

static class Jez
{
    public static bool En;

    public static string T(string sr, string en) => En ? en : sr;

    /// <summary>"srpski | english" gives the part for the current language; text without " | " is the same in both.</summary>
    public static string Izaberi(string s)
    {
        int i = s.IndexOf(" | ", StringComparison.Ordinal);
        return i < 0 ? s : En ? s[(i + 3)..] : s[..i];
    }
}
