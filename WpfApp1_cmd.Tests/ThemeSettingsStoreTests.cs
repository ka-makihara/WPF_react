using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ThemeSettingsStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ThemeSettingsTests", Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(directory, "theme.txt");

    [Fact]
    public void MissingSettingsUseSystemTheme()
    {
        Assert.Equal(ThemePreference.System, new ThemeSettingsStore(SettingsPath).Load());
    }

    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    [InlineData(ThemePreference.System)]
    public void SelectionPersistsAcrossStoreInstances(ThemePreference preference)
    {
        new ThemeSettingsStore(SettingsPath).Save(preference);
        Assert.Equal(preference, new ThemeSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void SavingReplacesPreviousSelection()
    {
        var store = new ThemeSettingsStore(SettingsPath);
        store.Save(ThemePreference.Dark);
        store.Save(ThemePreference.Light);
        Assert.Equal(ThemePreference.Light, store.Load());
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("99")]
    public void CorruptSettingsAreReported(string value)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, value);
        Assert.Throws<InvalidDataException>(() => new ThemeSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void InvalidPreferenceIsNotSaved()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThemeSettingsStore(SettingsPath).Save((ThemePreference)99));
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void UnwritableSettingsAreReported()
    {
        Directory.CreateDirectory(SettingsPath);
        Assert.Throws<UnauthorizedAccessException>(() => new ThemeSettingsStore(SettingsPath).Save(ThemePreference.Dark));
        Assert.Throws<UnauthorizedAccessException>(() => new ThemeSettingsStore(SettingsPath).Load());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
