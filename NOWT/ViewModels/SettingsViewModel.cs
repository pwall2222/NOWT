using Microsoft.Toolkit.Mvvm.ComponentModel;
using NOWT.Properties;

namespace NOWT.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private bool _alwaysShowTrackerProfile;

    public bool AlwaysShowTrackerProfile
    {
        get => _alwaysShowTrackerProfile;
        set
        {
            if (SetProperty(ref _alwaysShowTrackerProfile, value))
            {
                Settings.Default.AlwaysShowTrackerProfile = value;
                Settings.Default.Save();
            }
        }
    }

    public SettingsViewModel()
    {
        _alwaysShowTrackerProfile = Settings.Default.AlwaysShowTrackerProfile;
    }
}
