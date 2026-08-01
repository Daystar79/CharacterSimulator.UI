using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CharacterSimulator.Logic.Data.Db;
using CharacterSimulator.Logic.Services;

namespace CharacterSimulator.GUI;

public class ProfileListItemViewModel
{
    public UserProfile Profile { get; }
    public string DisplayName => Profile.DisplayName;
    public string AgeText => $"Age {Profile.CalculateAge()}";
    public string AdultStatusText => Profile.IsAdultEligible() ? (Profile.IsAdultAttested ? "🔞 Adult Mode ON" : "🔞 Adult Eligible") : "🔒 Minor (PG-13 Only)";
    public string LastPlayedText => $"Last active: {Profile.LastOpenedAt.ToLocalTime():yyyy-MM-dd HH:mm}";

    public ProfileListItemViewModel(UserProfile profile)
    {
        Profile = profile;
    }
}

public partial class ProfilePickerWindow : Window
{
    public UserProfile? SelectedProfile { get; private set; }

    public ProfilePickerWindow()
    {
        InitializeComponent();
        LoadProfiles();
    }

    private void LoadProfiles()
    {
        var profiles = ProfileService.Instance.GetAllProfiles();
        var items = profiles.Select(p => new ProfileListItemViewModel(p)).ToList();
        ListProfiles.ItemsSource = items;

        if (items.Count > 0)
        {
            ListProfiles.SelectedIndex = 0;
        }
    }

    private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ListProfiles.SelectedItem is ProfileListItemViewModel vm)
        {
            bool hasPin = !string.IsNullOrEmpty(vm.Profile.PinHash);
            PanelPinPrompt.IsVisible = hasPin;
        }
    }

    private async void OnCreateProfileClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new CreateProfileWindow();
        var result = await dialog.ShowDialog<bool>(this);
        if (result && dialog.CreatedProfile != null)
        {
            SelectedProfile = dialog.CreatedProfile;
            Close(true);
        }
        else
        {
            LoadProfiles();
        }
    }

    private void OnSelectProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (ListProfiles.SelectedItem is ProfileListItemViewModel vm)
        {
            string pin = TxtPinInput.Text?.Trim() ?? "";
            bool ok = ProfileService.Instance.SwitchProfile(vm.Profile.Id, pin);
            if (ok)
            {
                SelectedProfile = ProfileService.Instance.ActiveProfile;
                Close(true);
            }
            else
            {
                TxtPinInput.Text = "";
            }
        }
    }
}
