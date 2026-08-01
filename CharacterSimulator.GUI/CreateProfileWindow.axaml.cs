using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CharacterSimulator.Logic.Data.Db;
using CharacterSimulator.Logic.Services;

namespace CharacterSimulator.GUI;

public partial class CreateProfileWindow : Window
{
    public UserProfile? CreatedProfile { get; private set; }

    public CreateProfileWindow()
    {
        InitializeComponent();
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        string name = TxtName.Text?.Trim() ?? "Player";
        if (string.IsNullOrWhiteSpace(name))
            name = "Player";

        int year = Convert.ToInt32(NumYear.Value ?? 2000);
        int month = Convert.ToInt32(NumMonth.Value ?? 1);
        int day = Convert.ToInt32(NumDay.Value ?? 1);
        string? pin = TxtPin.Text?.Trim();
        bool attest = ChkAdultAttest.IsChecked ?? false;

        CreatedProfile = ProfileService.Instance.CreateProfile(name, year, month, day, pin, attest);
        Close(true);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        CreatedProfile = null;
        Close(false);
    }
}
