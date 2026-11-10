using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ControlzEx.Theming;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using WpfApp1_cmd.Resources;

namespace WpfApp1_cmd.Services;

public sealed class ThemeService : INotifyPropertyChanged, IDisposable
{
    private readonly Application application;
    private readonly ThemeSettingsStore store;
    private bool disposed;

    public ThemeService(Application application, ThemeSettingsStore? store = null)
    {
        this.application = application;
        this.store = store ?? new ThemeSettingsStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Fuji", "UnitSoftTransferTool", "theme.txt"));
    }

    public ThemePreference Preference { get; private set; } = ThemePreference.System;
    public bool IsSystem => Preference == ThemePreference.System;
    public bool IsLight => Preference == ThemePreference.Light;
    public bool IsDark => Preference == ThemePreference.Dark;
    public Color TextColor { get; private set; }
    public Color WarningColor { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Initialize()
    {
        SetBoundBrush("App.TextBrush", nameof(TextColor));
        SetBoundBrush("App.WarningBrush", nameof(WarningColor));
        try
        {
            Preference = store.Load();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException)
        {
            ReportError(ex);
        }

        Apply();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Select(ThemePreference preference)
    {
        try
        {
            store.Save(preference);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException)
        {
            ReportError(ex);
            return;
        }

        Preference = preference;
        Apply();
    }

    private void Apply()
    {
        bool dark = Preference == ThemePreference.Dark;
        if (Preference == ThemePreference.System)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object? value = key?.GetValue("AppsUseLightTheme");
                dark = value switch
                {
                    null or 1 => false,
                    0 => true,
                    _ => throw new InvalidDataException("Invalid Windows AppsUseLightTheme setting.")
                };
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or SecurityException)
            {
                ReportError(ex);
            }
        }

        ThemeManager.Current.ChangeThemeBaseColor(application, dark ? "Dark" : "Light");
        var palette = new PaletteHelper();
        var theme = palette.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        palette.SetTheme(theme);

        TextColor = (Color)ColorConverter.ConvertFromString(dark ? "#E8EAED" : "#202124");
        WarningColor = (Color)ColorConverter.ConvertFromString(dark ? "#FFD166" : "#805500");
        SetBrush("App.AlternatingRowBrush", dark ? "#464646" : "#CCD5DF");
        SetBrush("App.List.HoverBrush", dark ? "#304F68" : "#E0F0FF");
        SetBrush("App.List.SelectedBrush", dark ? "#255F8A" : "#BEDFFF");
        SetBrush("MaterialDesign.Brush.DataGrid.Selected", dark ? "#255F8A" : "#BEDFFF");
        SetBrush("App.List.SelectionBorderBrush", dark ? "#90CAF9" : "#1565C0");
        SetBrush("App.Log.BackgroundBrush", dark ? "#1E1E1E" : "#FAFAFA");
        SetBrush("App.Log.TextBrush", dark ? "#E8EAED" : "#202124");
        SetBrush("App.Log.ErrorBrush", dark ? "#FF6B6B" : "#B71C1C");
        SetBrush("App.Log.WarningBrush", dark ? "#FFD166" : "#805500");
        SetBrush("App.Log.SuccessBrush", dark ? "#80CBC4" : "#00695C");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private void SetBoundBrush(string key, string property)
    {
        // A binding prevents WPF from freezing brushes retained by tree value converters.
        var brush = new SolidColorBrush();
        BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding(property) { Source = this });
        application.Resources[key] = brush;
    }

    private void SetBrush(string key, string color)
    {
        var value = (Color)ColorConverter.ConvertFromString(color);
        if (application.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
            brush.Color = value;
        else
            application.Resources[key] = new SolidColorBrush(value);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        application.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!disposed && IsSystem)
                Apply();
        }));
    }

    private static void ReportError(Exception exception)
    {
        Trace.TraceError("Theme settings: {0}", exception);
        MessageBox.Show(string.Format(Resource.ThemeSettingsError, exception.Message),
            Resource.MENU_Settings, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void Dispose()
    {
        disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
