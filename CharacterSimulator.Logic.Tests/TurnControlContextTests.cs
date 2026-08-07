using System;
using CharacterSimulator.Logic;
using Xunit;

namespace CharacterSimulator.Logic.Tests;

public class TurnControlContextTests
{
    [Fact]
    public void PatchSelectedCharacter_DoesNotRaiseOnUIUpdated()
    {
        var control = new TurnControlContext();
        int uiUpdates = 0;
        int settingsChanges = 0;
        control.OnUIUpdated += () => uiUpdates++;
        control.OnSettingsChanged += _ => settingsChanges++;

        control.PatchSelectedCharacter("fc00cc76541bdec0.json");
        control.PatchSelectedCharacter("fc00cc76541bdec0.json"); // no-op same value still quiet
        control.PatchSelectedCharacter("");

        Assert.Equal(0, uiUpdates);
        Assert.Equal(0, settingsChanges);
        Assert.Equal("", control.CurrentSettings.SelectedCharA);
    }

    [Fact]
    public void PatchSelectedCharacter_DoesNotReenterViaOnUIUpdatedHandler()
    {
        var control = new TurnControlContext();
        int patchCount = 0;
        const int max = 25;

        control.OnUIUpdated += () =>
        {
            // Mirrors the old Index bug: save selection inside a UI-refresh handler.
            if (patchCount < max)
            {
                patchCount++;
                control.PatchSelectedCharacter("fc00cc76541bdec0.json");
            }
        };

        // Quiet patch must not fire OnUIUpdated, so the handler never runs.
        control.PatchSelectedCharacter("fc00cc76541bdec0.json");

        Assert.Equal(0, patchCount);
        Assert.Equal("fc00cc76541bdec0.json", control.CurrentSettings.SelectedCharA);
    }

    [Fact]
    public void UpdateSettings_StillRaisesOnUIUpdated()
    {
        var control = new TurnControlContext();
        int uiUpdates = 0;
        control.OnUIUpdated += () => uiUpdates++;

        var settings = control.CurrentSettings ?? new AppSettings();
        settings.MaxTurns = Math.Max(4, settings.MaxTurns);
        control.UpdateSettings(settings);

        Assert.Equal(1, uiUpdates);
    }

    [Fact]
    public void PatchSelectedCharacter_NormalizesNonePlaceholders()
    {
        var control = new TurnControlContext();
        control.PatchSelectedCharacter("None (Solo Roleplay)");
        Assert.Equal("", control.CurrentSettings.SelectedCharA);
    }
}
