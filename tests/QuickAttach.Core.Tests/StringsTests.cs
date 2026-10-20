using System.Globalization;

namespace QuickAttach.Core.Tests;

public class StringsTests
{
    [Theory]
    [InlineData("de-DE", "Keine Dateien gefunden")]
    [InlineData("de-AT", "Keine Dateien gefunden")]
    [InlineData("en-US", "No files found")]
    [InlineData("fr-FR", "No files found")]
    public void FollowsUiCulture(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Assert.Equal(expected, Strings.NoFilesFound);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
