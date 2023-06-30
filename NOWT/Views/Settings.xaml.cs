using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using AutoUpdaterDotNET;
using NOWT.Helpers;
using NOWT.Properties;
using static NOWT.Helpers.Login;
using static NOWT.Helpers.ValApi;

namespace NOWT.Views;

public partial class Settings : UserControl
{
    private readonly List<CultureInfo> _languageList = new();

    public Settings()
    {
        InitializeComponent();
    }

    private async Task CheckAuthAsync()
    {
        AuthStatusBox.Text = Properties.Resources.Refreshing;
        if (!await Checks.CheckLoginAsync().ConfigureAwait(false))
            AuthStatusBox.Text = Properties.Resources.AuthStatusFail;
        else AuthStatusBox.Text = $"{Properties.Resources.AuthStatusAuthAs} {await GetNameServiceGetUsernameAsync(Constants.Ppuuid).ConfigureAwait(false)}";
    }

    private async void Button_Click1Async(object sender, RoutedEventArgs e)
    {
        string ProductVersion = System.Windows.Forms.Application.ProductVersion;
        CurrentVersion.Text = ProductVersion;
        LatestVersion.Text = await GetLatestVersionAsync().ConfigureAwait(false);
        AutoUpdater.InstalledVersion = new Version(ProductVersion);
        AutoUpdater.Start("https://raw.githubusercontent.com/pwall2222/NOWT/main/NOWT/VersionInfo.xml");
        await CheckAndUpdateJsonAsync().ConfigureAwait(false);
    }

    private static Task<string> GetLatestVersionAsync()
    {
        var xml = new XmlDocument();
        xml.Load("https://raw.githubusercontent.com/pwall2222/NOWT/main/NOWT/VersionInfo.xml");
        var result = xml.GetElementsByTagName("version");
        return Task.FromResult(result[0].InnerText);
    }

    private async void Button_Click2Async(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        await CheckAuthAsync().ConfigureAwait(false);
        Mouse.OverrideCursor = Cursors.Arrow;
    }

    private async void Button_Click3Async(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        if (await Checks.CheckLocalAsync().ConfigureAwait(false))
        {
            await LocalLoginAsync().ConfigureAwait(false);
            await LocalRegionAsync().ConfigureAwait(false);
            await CheckAuthAsync().ConfigureAwait(false);
        }
        else
        {
            AuthStatusBox.Text = Properties.Resources.NoValGame;
        }

        Mouse.OverrideCursor = Cursors.Arrow;
    }

    private async void Button_Click4Async(object sender, RoutedEventArgs e)
    {
        await CheckAndUpdateJsonAsync().ConfigureAwait(false);
    }

    private async void Button_Click5Async(object sender, RoutedEventArgs e)
    {
        await UpdateFilesAsync().ConfigureAwait(false);
    }

    private void ListBox_SelectedAsync(object sender, SelectionChangedEventArgs e)
    {
        var combo = (ComboBox) sender;
        var index = combo.SelectedIndex;
        Thread.CurrentThread.CurrentCulture = _languageList[index];
        Thread.CurrentThread.CurrentUICulture = _languageList[index];
        Properties.Settings.Default.Language = _languageList[index].TwoLetterISOLanguageName;
        UpdateFilesAsync().ConfigureAwait(false);
        Application.Current.Shutdown();
        System.Windows.Forms.Application.Restart();
    }

    private static Task<IEnumerable<CultureInfo>> GetAvailableCulturesAsync()
    {
        var result = new List<CultureInfo>();
        var rm = new ResourceManager(typeof(Resources));

        var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
        foreach (var culture in cultures)
            try
            {
                if (culture.Equals(CultureInfo.InvariantCulture)) continue;

                var rs = rm.GetResourceSet(culture, true, false);
                if (rs != null)
                    result.Add(culture);
            }
            catch (CultureNotFoundException)
            {
            }

        rm.ReleaseAllResources();
        return Task.FromResult<IEnumerable<CultureInfo>>(result);
    }

    private async void LanguageList_OnDropDownOpenedAsync(object sender, EventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        if (LanguageCombo.Items.Count == 0)
            foreach (var language in await GetAvailableCulturesAsync().ConfigureAwait(false))
            {
                LanguageCombo.Items.Add(language.NativeName);
                _languageList.Add(language);
            }

        Mouse.OverrideCursor = Cursors.Arrow;
    }
}