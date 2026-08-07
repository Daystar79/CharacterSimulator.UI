using System;
using System.Threading;
using System.Threading.Tasks;

namespace CharacterSimulator.Logic;

public enum SimulationState
{
    Ready,
    Running,
    Paused,
    Stopped
}

public class TurnControlContext
{
    private CancellationTokenSource _cts = new();
    private TaskCompletionSource<bool> _stepSignal = new();

    public SimulationState State { get; private set; } = SimulationState.Ready;
    public int DelayMs { get; set; } = 600;

    public AppSettings CurrentSettings { get; private set; } = AppConfigService.LoadSettings();

    public event Action<SimulationState>? OnStateChanged;
    public event Action? OnUIUpdated;
    public event Action<AppSettings>? OnSettingsChanged;

    public void UpdateSettings(AppSettings settings)
    {
        CurrentSettings = settings;
        AppConfigService.SaveSettings(settings);
        OnSettingsChanged?.Invoke(settings);
        OnUIUpdated?.Invoke();
    }

    /// <summary>
    /// Update genre + freeform location without a full UI catalog refresh or log spam.
    /// Used by the left-panel scene editor (this is the live roleplay scene).
    /// </summary>
    public void PatchScene(string? genreId, string? scenePrompt)
    {
        CurrentSettings ??= new AppSettings();
        string genre = SceneGenreCatalog.GetById(genreId).Id;
        string place = string.IsNullOrWhiteSpace(scenePrompt)
            ? SceneGenreCatalog.DefaultSceneFor(genre)
            : scenePrompt.Trim();

        bool changed = !string.Equals(CurrentSettings.SelectedGenre, genre, StringComparison.Ordinal)
                       || !string.Equals(CurrentSettings.ScenePrompt, place, StringComparison.Ordinal);
        CurrentSettings.SelectedGenre = genre;
        CurrentSettings.ScenePrompt = place;
        if (changed)
            AppConfigService.SaveSettings(CurrentSettings);
        // No OnSettingsChanged / OnUIUpdated — Index already holds the live fields;
        // Setup modal re-reads CurrentSettings when opened.
    }

    /// <summary>
    /// Persist last-selected primary card without a full UI catalog refresh.
    /// Must not raise OnUIUpdated — Index's refresh handler would re-enter SelectCharacter
    /// and cause an infinite load / auto-play storm.
    /// </summary>
    public void PatchSelectedCharacter(string? cardFileName)
    {
        CurrentSettings ??= new AppSettings();
        string file = string.IsNullOrWhiteSpace(cardFileName) ? "" : cardFileName.Trim();
        // Treat legacy "None (...)" placeholders as empty
        if (file.StartsWith("None", StringComparison.OrdinalIgnoreCase)
            || file.StartsWith('(')
            || file.Contains("No Character", StringComparison.OrdinalIgnoreCase)
            || file.Contains("Not Selected", StringComparison.OrdinalIgnoreCase))
        {
            file = "";
        }

        bool changed = !string.Equals(CurrentSettings.SelectedCharA, file, StringComparison.Ordinal);
        CurrentSettings.SelectedCharA = file;
        if (changed)
            AppConfigService.SaveSettings(CurrentSettings);
        // No OnSettingsChanged / OnUIUpdated — selection is already applied in the right panel.
    }

    public void RefreshUI()
    {
        CurrentSettings = AppConfigService.LoadSettings();
        // Cheap mtime reconcile so selectors pick up new/edited cards without full re-parse of unchanged files
        try { CharacterCatalog.ReconcileFromDisk(); }
        catch { /* index optional */ }
        OnSettingsChanged?.Invoke(CurrentSettings);
        OnUIUpdated?.Invoke();
    }

    public void NotifyUIUpdate()
    {
        OnUIUpdated?.Invoke();
    }

    public CancellationToken CancellationToken => _cts.Token;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        State = SimulationState.Running;
        OnStateChanged?.Invoke(State);
    }

    public void Pause()
    {
        if (State == SimulationState.Running)
        {
            State = SimulationState.Paused;
            OnStateChanged?.Invoke(State);
        }
    }

    public void Resume()
    {
        if (State == SimulationState.Paused || State == SimulationState.Ready)
        {
            State = SimulationState.Running;
            _stepSignal.TrySetResult(true);
            OnStateChanged?.Invoke(State);
        }
    }

    public void Step()
    {
        State = SimulationState.Paused;
        _stepSignal.TrySetResult(true);
        OnStateChanged?.Invoke(State);
    }

    public void Stop()
    {
        State = SimulationState.Stopped;
        _cts.Cancel();
        _stepSignal.TrySetResult(false);
        OnStateChanged?.Invoke(State);
    }

    public async Task WaitTurnAsync()
    {
        if (_cts.IsCancellationRequested) return;

        if (State == SimulationState.Paused)
        {
            _stepSignal = new TaskCompletionSource<bool>();
            try
            {
                await _stepSignal.Task;
            }
            catch (TaskCanceledException) { }
        }
        else if (DelayMs > 0)
        {
            try
            {
                await Task.Delay(DelayMs, _cts.Token);
            }
            catch (TaskCanceledException) { }
        }
    }
}
