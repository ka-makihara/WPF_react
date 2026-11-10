using System.IO;

namespace WpfApp1_cmd.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public sealed class ThemeSettingsStore(string filePath)
{
    public ThemePreference Load()
    {
        string value;
        try
        {
            value = File.ReadAllText(filePath).Trim();
        }
        catch (FileNotFoundException)
        {
            return ThemePreference.System;
        }
        catch (DirectoryNotFoundException)
        {
            return ThemePreference.System;
        }
        if (!Enum.TryParse(value, out ThemePreference preference) || !Enum.IsDefined(preference))
            throw new InvalidDataException($"Invalid theme setting: {value}");

        return preference;
    }

    public void Save(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference))
            throw new ArgumentOutOfRangeException(nameof(preference));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        File.WriteAllText(filePath, preference.ToString());
    }
}
