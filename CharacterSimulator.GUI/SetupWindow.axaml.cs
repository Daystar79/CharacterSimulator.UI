using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CharacterSimulator.Logic;

namespace CharacterSimulator.GUI;

public partial class SetupWindow : Window
{
    public string SelectedCharA { get; private set; } = "serena.md";
    public string SelectedCharB { get; private set; } = "None (Solo Roleplay)";
    public string SelectedLlmA { get; private set; } = "Mock";
    public string SelectedLlmB { get; private set; } = "Mock";
    public string SelectedGenre { get; private set; } = SceneGenreCatalog.DefaultGenreId;
    public string ScenePrompt { get; private set; } = SceneGenreCatalog.DefaultSceneFor(SceneGenreCatalog.DefaultGenreId);
    public int MaxTurns { get; private set; } = 10;
    public bool IsApplied { get; private set; } = false;

    private bool _suppressScenePresetWrite;
    private readonly List<SceneGenre> _genres = SceneGenreCatalog.All.ToList();

    public SetupWindow()
    {
        InitializeComponent();
        PopulateDropdowns();
    }

    public SetupWindow(string charA, string charB, string llmA, string llmB, string genre, string scene, int maxTurns) : this()
    {
        SelectedCharA = charA;
        SelectedCharB = charB;
        SelectedLlmA = llmA;
        SelectedLlmB = llmB;
        SelectedGenre = SceneGenreCatalog.GetById(genre).Id;
        ScenePrompt = scene;
        MaxTurns = maxTurns;

        if (ComboCharA.Items.Cast<object?>().Any(i => string.Equals(i?.ToString(), charA, StringComparison.OrdinalIgnoreCase)))
            ComboCharA.SelectedItem = charA;
        if (ComboCharB.Items.Cast<object?>().Any(i => string.Equals(i?.ToString(), charB, StringComparison.OrdinalIgnoreCase)))
            ComboCharB.SelectedItem = charB;
        ComboLlmA.SelectedItem = ResolveProviderDisplayName(llmA, ComboLlmA.Items.Cast<object?>().Select(i => i?.ToString() ?? ""));
        ComboLlmB.SelectedItem = ResolveProviderDisplayName(llmB, ComboLlmB.Items.Cast<object?>().Select(i => i?.ToString() ?? ""));

        SelectGenreById(SelectedGenre);
        TxtScene.Text = scene;
        NumTurns.Value = maxTurns;
    }

    private void PopulateDropdowns()
    {
        var charFiles = CharacterCatalog.ListCardFileNames();
        if (charFiles.Count == 0)
        {
            charFiles.Add("serena.md");
            charFiles.Add("kira.md");
        }

        ComboCharA.ItemsSource = charFiles;

        var charBOptions = new List<string> { "None (Solo Roleplay)" };
        charBOptions.AddRange(charFiles);
        ComboCharB.ItemsSource = charBOptions;

        ComboCharA.SelectedItem = charFiles.Contains("serena.md") ? "serena.md" : charFiles[0];
        ComboCharB.SelectedIndex = 0;

        var llmList = LlmDiscoveryService.GetAvailableProviderNames();
        ComboLlmA.ItemsSource = llmList;
        ComboLlmB.ItemsSource = llmList;
        ComboLlmA.SelectedIndex = 0;
        ComboLlmB.SelectedIndex = 0;

        ComboGenre.ItemsSource = _genres.Select(g => g.DisplayName).ToList();
        SelectGenreById(SceneGenreCatalog.DefaultGenreId);
    }

    private void SelectGenreById(string genreId)
    {
        var genre = SceneGenreCatalog.GetById(genreId);
        int idx = _genres.FindIndex(g => g.Id == genre.Id);
        if (idx < 0) idx = 0;

        _suppressScenePresetWrite = true;
        ComboGenre.SelectedIndex = idx;
        ApplyGenreUi(genre, writeSceneIfEmpty: TxtScene == null || string.IsNullOrWhiteSpace(TxtScene.Text));
        _suppressScenePresetWrite = false;
    }

    private void OnGenreSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ComboGenre.SelectedIndex < 0 || ComboGenre.SelectedIndex >= _genres.Count) return;
        var genre = _genres[ComboGenre.SelectedIndex];
        ApplyGenreUi(genre, writeSceneIfEmpty: false);
        // When user picks a genre, load its first preset into the scene box
        if (!_suppressScenePresetWrite && genre.ScenePresets.Count > 0)
            TxtScene.Text = genre.ScenePresets[0];
    }

    private void ApplyGenreUi(SceneGenre genre, bool writeSceneIfEmpty)
    {
        TxtGenreHint.Text = $"{genre.Description} · {genre.EnvironmentTone}";

        _suppressScenePresetWrite = true;
        ComboScenePreset.ItemsSource = genre.ScenePresets.ToList();
        if (genre.ScenePresets.Count > 0)
            ComboScenePreset.SelectedIndex = 0;
        _suppressScenePresetWrite = false;

        if (writeSceneIfEmpty && genre.ScenePresets.Count > 0)
            TxtScene.Text = genre.ScenePresets[0];
    }

    private void OnScenePresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressScenePresetWrite) return;
        string? preset = ComboScenePreset.SelectedItem?.ToString();
        if (!string.IsNullOrWhiteSpace(preset))
            TxtScene.Text = preset;
    }

    private void OnApplyClicked(object? sender, RoutedEventArgs e)
    {
        SelectedCharA = ComboCharA.SelectedItem?.ToString() ?? "serena.md";
        SelectedCharB = ComboCharB.SelectedItem?.ToString() ?? "None (Solo Roleplay)";
        SelectedLlmA = ComboLlmA.SelectedItem?.ToString() ?? "Mock";
        SelectedLlmB = ComboLlmB.SelectedItem?.ToString() ?? "Mock";

        if (ComboGenre.SelectedIndex >= 0 && ComboGenre.SelectedIndex < _genres.Count)
            SelectedGenre = _genres[ComboGenre.SelectedIndex].Id;
        else
            SelectedGenre = SceneGenreCatalog.DefaultGenreId;

        ScenePrompt = string.IsNullOrWhiteSpace(TxtScene.Text)
            ? SceneGenreCatalog.DefaultSceneFor(SelectedGenre)
            : TxtScene.Text.Trim();
        MaxTurns = Convert.ToInt32(NumTurns.Value ?? 10);
        IsApplied = true;

        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        IsApplied = false;
        Close();
    }

    /// <summary>
    /// Map legacy saved names (e.g. "Vibe CLI") onto the current dropdown labels.
    /// </summary>
    private static string ResolveProviderDisplayName(string saved, IEnumerable<string> available)
    {
        var list = available.Where(s => !string.IsNullOrEmpty(s)).ToList();
        if (list.Count == 0) return saved;

        var exact = list.FirstOrDefault(s => s.Equals(saved, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Legacy / alias mapping into current display names
        if (saved.Contains("Vibe", StringComparison.OrdinalIgnoreCase) ||
            saved.Contains("Mistral", StringComparison.OrdinalIgnoreCase))
        {
            var mistral = list.FirstOrDefault(s => s.Contains("Mistral", StringComparison.OrdinalIgnoreCase)
                                                   || s.Contains("Vibe", StringComparison.OrdinalIgnoreCase));
            if (mistral != null) return mistral;
        }

        if (saved.Contains("Agy", StringComparison.OrdinalIgnoreCase) ||
            saved.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            var agy = list.FirstOrDefault(s => s.Contains("Agy", StringComparison.OrdinalIgnoreCase)
                                               || s.Contains("Gemini", StringComparison.OrdinalIgnoreCase));
            if (agy != null) return agy;
        }

        if (saved.Contains("Mock", StringComparison.OrdinalIgnoreCase))
        {
            var mock = list.FirstOrDefault(s => s.Contains("Mock", StringComparison.OrdinalIgnoreCase));
            if (mock != null) return mock;
        }

        return list[0];
    }
}
