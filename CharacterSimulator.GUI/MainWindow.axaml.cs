using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CharacterSimulator.Logic;
using CharacterSimulator.Logic.Logs;
using CharacterSimulator.Logic.Safety;

namespace CharacterSimulator.GUI;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DialogueMessageModel> _dialogueFeed = new();
    private readonly ObservableCollection<GoalViewModel> _charAGoals = new();
    private readonly ObservableCollection<GoalViewModel> _charBGoals = new();

    private Character? _charA;
    private Character? _charB;
    private TurnControlContext _controlContext = new();
    private TurnManager? _activeTurnManager;
    private Task? _simulationTask;
    private List<RpgOption> _currentRpgOptions = new();
    private List<TurnStepEventArgs> _turnHistory = new();

    private string _selectedCharA = "serena.md";
    private string _selectedCharB = "None (Solo Roleplay)";
    private string _selectedLlmA = "Mock";
    private string _selectedLlmB = "Mock";
    private string _selectedGenre = SceneGenreCatalog.DefaultGenreId;
    private string _scenePrompt = SceneGenreCatalog.DefaultSceneFor(SceneGenreCatalog.DefaultGenreId);
    private int _maxTurns = 10;
    private bool _isPlayerGuidedMode = false;
    private bool _isInitialSetupCompleted = false;

    public MainWindow()
    {
        InitializeComponent();
        ItemsDialogueFeed.ItemsSource = _dialogueFeed;
        ItemsCharAGoals.ItemsSource = _charAGoals;
        ItemsCharBGoals.ItemsSource = _charBGoals;

        LoadPersistentSettings();
        UpdateUserRoleDropdown();

        _controlContext.OnStateChanged += OnSimulationStateChanged;
        Opened += OnMainWindowOpened;
    }

    private async void OnMainWindowOpened(object? sender, EventArgs e)
    {
        var settings = AppConfigService.LoadSettings();
        if (!settings.IsConfigured || !AppConfigService.HasConfigFile())
        {
            TxtStatus.Text = "First time launch: Please configure characters and LLM providers.";
            await Task.Delay(200);
            OnOpenSetupClicked(null, null);
        }
    }

    private void LoadPersistentSettings()
    {
        var settings = AppConfigService.LoadSettings();
        _selectedCharA = settings.SelectedCharA;
        _selectedCharB = settings.SelectedCharB;
        _selectedLlmA = settings.SelectedLlmA;
        _selectedLlmB = settings.SelectedLlmB;
        _selectedGenre = SceneGenreCatalog.GetById(settings.SelectedGenre).Id;
        _scenePrompt = settings.ScenePrompt;
        _maxTurns = settings.MaxTurns;
        _isPlayerGuidedMode = settings.RoleplayMode == "PlayerGuided";
        _isInitialSetupCompleted = settings.IsConfigured;

        UpdateActiveSceneLabel();
        TxtActiveMode.Text = _isPlayerGuidedMode ? "🎮 Player-Guided" : "🤖 Auto-Play";

        bool isSolo = _selectedCharB.StartsWith("None", StringComparison.OrdinalIgnoreCase);
        CardCharB.IsVisible = !isSolo;
    }

    private void SavePersistentSettings()
    {
        AppConfigService.SaveSettings(new AppSettings
        {
            IsConfigured = true,
            SelectedCharA = _selectedCharA,
            SelectedCharB = _selectedCharB,
            SelectedLlmA = _selectedLlmA,
            SelectedLlmB = _selectedLlmB,
            SelectedGenre = _selectedGenre,
            ScenePrompt = _scenePrompt,
            MaxTurns = _maxTurns,
            RoleplayMode = _isPlayerGuidedMode ? "PlayerGuided" : "AutoPlay"
        });
        _isInitialSetupCompleted = true;
    }

    private string ComposeActiveSceneContext() =>
        SceneGenreCatalog.ComposeSceneContext(_selectedGenre, _scenePrompt);

    private void UpdateActiveSceneLabel()
    {
        var genre = SceneGenreCatalog.GetById(_selectedGenre);
        TxtActiveScene.Text = $"[{genre.DisplayName}] {_scenePrompt}";
        UpdateActiveLlmLabels();
    }

    private void UpdateActiveLlmLabels()
    {
        bool isSolo = _selectedCharB.StartsWith("None", StringComparison.OrdinalIgnoreCase);
        TxtActiveLlm.Text = isSolo
            ? $"⚡ Agent A: {_selectedLlmA}"
            : $"⚡ Agent A: {_selectedLlmA} | Agent B: {_selectedLlmB}";

        TxtCharALlm.Text = $"🤖 Agent: {_selectedLlmA}";
        TxtCharBLlm.Text = $"🤖 Agent: {_selectedLlmB}";
    }

    private void UpdateUserRoleDropdown()
    {
        var roles = new List<string> { "👤 Player / DM" };

        if (_charA != null && !string.IsNullOrEmpty(_charA.Name))
        {
            roles.Add($"A: {_charA.Name}");
        }

        bool isSoloMode = _selectedCharB.StartsWith("None", StringComparison.OrdinalIgnoreCase);
        if (_charB != null && !isSoloMode && !string.IsNullOrEmpty(_charB.Name) && _charB.Name != "Player")
        {
            roles.Add($"B: {_charB.Name}");
        }

        ComboUserRole.ItemsSource = roles;
        ComboUserRole.SelectedIndex = 0;
    }

    private void OnSetAutoPlayMode(object? sender, RoutedEventArgs e)
    {
        _isPlayerGuidedMode = false;
        TxtActiveMode.Text = "🤖 Auto-Play";
        TxtStatus.Text = "Switched to Auto-Play Mode (continuous AI simulation).";
        SavePersistentSettings();
    }

    private void OnSetPlayerGuidedMode(object? sender, RoutedEventArgs e)
    {
        _isPlayerGuidedMode = true;
        TxtActiveMode.Text = "🎮 Player-Guided";
        TxtStatus.Text = "Switched to Player-Guided Mode (turns pause for player RPG choices).";
        SavePersistentSettings();
    }

    private async void OnOpenSetupClicked(object? sender, RoutedEventArgs? e)
    {
        var setupWindow = new SetupWindow(
            _selectedCharA,
            _selectedCharB,
            _selectedLlmA,
            _selectedLlmB,
            _selectedGenre,
            _scenePrompt,
            _maxTurns
        );

        await setupWindow.ShowDialog(this);

        if (setupWindow.IsApplied)
        {
            _selectedCharA = setupWindow.SelectedCharA;
            _selectedCharB = setupWindow.SelectedCharB;
            _selectedLlmA = setupWindow.SelectedLlmA;
            _selectedLlmB = setupWindow.SelectedLlmB;
            _selectedGenre = setupWindow.SelectedGenre;
            _scenePrompt = setupWindow.ScenePrompt;
            _maxTurns = setupWindow.MaxTurns;

            SavePersistentSettings();

            UpdateActiveSceneLabel();
            bool isSolo = _selectedCharB.StartsWith("None", StringComparison.OrdinalIgnoreCase);
            CardCharB.IsVisible = !isSolo;

            var genre = SceneGenreCatalog.GetById(_selectedGenre);
            TxtStatus.Text = isSolo
                ? $"Setup saved! Solo with {_selectedCharA} · Genre: {genre.DisplayName}"
                : $"Setup saved! {_selectedCharA} & {_selectedCharB} · Genre: {genre.DisplayName}";
        }
    }

    private void OnPlayClicked(object? sender, RoutedEventArgs e)
    {
        if (_controlContext.State == SimulationState.Paused)
        {
            _controlContext.Resume();
            return;
        }

        if (_controlContext.State == SimulationState.Running) return;

        // Start new simulation
        _dialogueFeed.Clear();
        _turnHistory.Clear();
        TxtAgentConsole.Text = "[SYSTEM] Simulation initialized. Piping stdout/stderr...\n";

        string charDir = CharacterCatalog.ResolveCharactersDirectory();

        _charA = CharacterLoader.Load(Path.Combine(charDir, _selectedCharA));

        bool isSoloMode = _selectedCharB.StartsWith("None", StringComparison.OrdinalIgnoreCase);
        if (isSoloMode)
        {
            _charB = new Character { Name = "Player", Emotion = "Neutral", EmotionEmoji = "👤" };
        }
        else
        {
            _charB = CharacterLoader.Load(Path.Combine(charDir, _selectedCharB));
        }

        UpdateCharacterCards();
        UpdateRpgChoiceButtons();
        UpdateUserRoleDropdown();

        ILLMClient clientA = LlmDiscoveryService.CreateClient(_selectedLlmA);
        ILLMClient? clientB = isSoloMode ? null : LlmDiscoveryService.CreateClient(_selectedLlmB);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        string logPath = Path.Combine("Output", $"conversation_gui_{timestamp}.log");
        var logger = new Logger(logPath);
        var sceneManager = new SceneManager();
        _activeTurnManager = new TurnManager(clientA, clientB, sceneManager, logger);

        string sceneContext = ComposeActiveSceneContext();
        UpdateActiveSceneLabel();
        TxtStatus.Text = isSoloMode
            ? $"Solo roleplay running... Speak with {_charA.Name}!"
            : $"Roleplay simulation running... Log saving to {logPath}";

        _activeTurnManager.OnAgentTurnStarted += (speakerName, providerName) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                TxtWaitingLlm.Text = $"Waiting for {speakerName} ({providerName})...";
                BadgeWaitingLlm.IsVisible = true;
                TxtStatus.Text = $"⏳ Dispatching prompt for {speakerName} via {providerName}...";
            });
        };

        _activeTurnManager.OnAgentOutputLogged += (rawLog) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                TxtAgentConsole.Text += rawLog + "\n";
                ScrollConsole.ScrollToEnd();
            });
        };

        _activeTurnManager.OnTurnStep += (turnArgs) =>
        {
            _turnHistory.Add(turnArgs);
            Dispatcher.UIThread.Post(() =>
            {
                BadgeWaitingLlm.IsVisible = false;
                bool isA = turnArgs.SpeakerName.Equals(_charA?.Name, StringComparison.OrdinalIgnoreCase);
                Character? speakerChar = isA ? _charA : _charB;

                Bitmap? speakerBitmap = null;
                if (speakerChar != null && !string.IsNullOrEmpty(speakerChar.AvatarPath) && File.Exists(speakerChar.AvatarPath))
                {
                    try
                    {
                        speakerBitmap = new Bitmap(speakerChar.AvatarPath);
                        ImgCanvasFrame.Source = speakerBitmap;
                        TxtCanvasPlaceholder.IsVisible = false;
                    }
                    catch { }
                }

                if (ImgCanvasFrame.Source == null)
                {
                    TxtCanvasPlaceholder.Text = $"🎭 {turnArgs.SpeakerName} {turnArgs.SpeakerEmotionEmoji}\n[{turnArgs.SpeakerEmotion}]\n\"{TruncateText(turnArgs.Dialogue, 90)}\"";
                    TxtCanvasPlaceholder.IsVisible = true;
                }

                var msg = new DialogueMessageModel
                {
                    TurnIndex = turnArgs.TurnIndex,
                    SpeakerName = turnArgs.SpeakerName,
                    TargetName = turnArgs.TargetName,
                    Dialogue = turnArgs.Dialogue,
                    SomaticText = turnArgs.SomaticZones.Count > 0 ? $"Somatic: {string.Join(", ", turnArgs.SomaticZones)}" : "Somatic: Calm",
                    BondDeltaText = turnArgs.BondDelta >= 0 ? $"Bond +{turnArgs.BondDelta} ({turnArgs.CurrentBond})" : $"Bond {turnArgs.BondDelta} ({turnArgs.CurrentBond})",
                    SpeakerEmotion = turnArgs.SpeakerEmotion,
                    SpeakerEmotionEmoji = turnArgs.SpeakerEmotionEmoji,
                    GoalStatusText = string.IsNullOrEmpty(turnArgs.ActiveGoalType) ? "Goal: Passive" : $"Goal: {turnArgs.ActiveGoalType} ({turnArgs.GoalStatus})",
                    SpeakerColor = isA ? "#38BDF8" : "#F43F5E",
                    SpeakerBg = isA ? "#0F172A" : "#1F1123",
                    IsLeft = isA,
                    ImagePrompt = turnArgs.ImagePrompt,
                    SpeakerBitmap = speakerBitmap
                };

                if (!string.IsNullOrEmpty(turnArgs.ImagePrompt))
                {
                    TxtImagePrompt.Text = turnArgs.ImagePrompt;
                }

                _dialogueFeed.Add(msg);
                UpdateCharacterCards();
                UpdateRpgChoiceButtons();
                ScrollDialogue.ScrollToEnd();

                if (_isPlayerGuidedMode || isSoloMode)
                {
                    _controlContext.Pause();
                    TxtStatus.Text = "Solo / Player-Guided Mode: Select an RPG choice or type custom dialogue to proceed.";
                }
            });
        };

        _activeTurnManager.OnGoalEvaluated += (goalArgs) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                string outcome = goalArgs.IsSuccess ? "★ SUCCESS" : "✖ FAILED";
                TxtStatus.Text = $"Goal Event: {goalArgs.CharacterName} {outcome} on goal {goalArgs.GoalType}!";
            });
        };

        _simulationTask = Task.Run(async () =>
        {
            await _activeTurnManager.RunConversationAsync(_charA, _charB, sceneContext, _maxTurns, _controlContext);
            Dispatcher.UIThread.Post(() =>
            {
                if (_controlContext.State != SimulationState.Stopped)
                {
                    _controlContext.Stop();
                    TxtStatus.Text = $"Simulation completed! Log saved to {logPath}";
                }
            });
        });
    }

    private void UpdateRpgChoiceButtons()
    {
        if (_charA == null) return;
        Character target = _charB ?? new Character { Name = "Player" };

        _currentRpgOptions = RpgChoiceService.GenerateOptions(_charA, target, _scenePrompt);

        if (_currentRpgOptions.Count >= 1) BtnRpgOpt1.Content = $"{_currentRpgOptions[0].CategoryEmoji} {_currentRpgOptions[0].Category}: {_currentRpgOptions[0].Text}";
        if (_currentRpgOptions.Count >= 2) BtnRpgOpt2.Content = $"{_currentRpgOptions[1].CategoryEmoji} {_currentRpgOptions[1].Category}: {_currentRpgOptions[1].Text}";
        if (_currentRpgOptions.Count >= 3) BtnRpgOpt3.Content = $"{_currentRpgOptions[2].CategoryEmoji} {_currentRpgOptions[2].Category}: {_currentRpgOptions[2].Text}";
        if (_currentRpgOptions.Count >= 4) BtnRpgOpt4.Content = $"{_currentRpgOptions[3].CategoryEmoji} {_currentRpgOptions[3].Category}: {_currentRpgOptions[3].Text}";
    }

    private void OnRpgOpt1Clicked(object? sender, RoutedEventArgs e) => ExecuteRpgOption(0);
    private void OnRpgOpt2Clicked(object? sender, RoutedEventArgs e) => ExecuteRpgOption(1);
    private void OnRpgOpt3Clicked(object? sender, RoutedEventArgs e) => ExecuteRpgOption(2);
    private void OnRpgOpt4Clicked(object? sender, RoutedEventArgs e) => ExecuteRpgOption(3);

    private void ExecuteRpgOption(int index)
    {
        if (index < 0 || index >= _currentRpgOptions.Count) return;
        var opt = _currentRpgOptions[index];

        var msg = new DialogueMessageModel
        {
            TurnIndex = _dialogueFeed.Count + 1,
            SpeakerName = "👤 Player",
            TargetName = opt.TargetCharacter,
            Dialogue = opt.Text,
            SomaticText = opt.Category,
            BondDeltaText = "Bond ±0",
            SpeakerEmotionEmoji = "🎲",
            GoalStatusText = opt.Category,
            SpeakerColor = "#F59E0B",
            SpeakerBg = "#2E1065",
            IsLeft = true
        };

        _dialogueFeed.Add(msg);
        ScrollDialogue.ScrollToEnd();

        if (_controlContext.State == SimulationState.Ready || _activeTurnManager == null)
        {
            OnPlayClicked(this, new RoutedEventArgs());
        }

        _activeTurnManager?.InjectUserInput("Player", opt.Text);

        TxtStatus.Text = $"Player executed {opt.Category}: \"{opt.Text}\"";

        if (_controlContext.State == SimulationState.Paused)
        {
            _controlContext.Step();
        }
    }

    private void OnPauseClicked(object? sender, RoutedEventArgs e) => _controlContext.Pause();

    private void OnStepClicked(object? sender, RoutedEventArgs e)
    {
        if (_controlContext.State == SimulationState.Ready || _controlContext.State == SimulationState.Stopped)
        {
            OnPlayClicked(sender, e);
            _controlContext.Pause();
        }
        else
        {
            _controlContext.Step();
        }
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        _controlContext.Stop();
        TxtStatus.Text = "Simulation stopped by user.";
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        _controlContext.Stop();
        _dialogueFeed.Clear();
        _charAGoals.Clear();
        _charBGoals.Clear();
        _turnHistory.Clear();
        TxtAgentConsole.Text = "[SYSTEM] Console output reset.";
        TxtImagePrompt.Text = "No active image prompt.";
        TxtCanvasPlaceholder.Text = "[IMAGE CANVAS READY]";
        TxtCanvasPlaceholder.IsVisible = true;
        ImgCanvasFrame.Source = null;
        TxtStatus.Text = "Simulation state reset.";
        OnSimulationStateChanged(SimulationState.Ready);
    }

    private void OnSaveSessionClicked(object? sender, RoutedEventArgs e)
    {
        if (_charA == null)
        {
            TxtStatus.Text = "No active simulation session to save.";
            return;
        }

        string saveDir = Path.Combine(Directory.GetCurrentDirectory(), "Output");
        string filePath = Path.Combine(saveDir, $"session_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.json");

        var sessionData = new RoleplaySessionData
        {
            SceneContext = ComposeActiveSceneContext(),
            CharacterA = _charA,
            CharacterB = _charB ?? new Character { Name = "Player" },
            History = _turnHistory
        };

        SessionService.SaveSession(filePath, sessionData);
        
        // Also commit durable logs for characters with in-memory changes
        if (_charA != null && _charA.DurableLog != null)
        {
            CommitService.CommitCharacterLogExplicit(_charA);
        }
        if (_charB != null && _charB.DurableLog != null && !string.Equals(_charB.Name, "Player", StringComparison.OrdinalIgnoreCase))
        {
            CommitService.CommitCharacterLogExplicit(_charB);
        }
        
        TxtStatus.Text = $"Session saved to {filePath}";
    }

    private void OnLoadSessionClicked(object? sender, RoutedEventArgs e)
    {
        string saveDir = Path.Combine(Directory.GetCurrentDirectory(), "Output");
        if (!Directory.Exists(saveDir)) return;

        var files = Directory.GetFiles(saveDir, "session_*.json").OrderByDescending(f => f).ToList();
        if (files.Count == 0)
        {
            TxtStatus.Text = "No saved session files found in Output/.";
            return;
        }

        var session = SessionService.LoadSession(files[0]);
        if (session != null)
        {
            _charA = session.CharacterA;
            _charB = session.CharacterB;
            // Session stores composed context; keep the freeform place line when possible
            _scenePrompt = session.SceneContext.Contains("Location:", StringComparison.Ordinal)
                ? session.SceneContext.Split('\n').FirstOrDefault(l => l.StartsWith("Location:", StringComparison.Ordinal))?.Substring("Location:".Length).Trim()
                  ?? session.SceneContext
                : session.SceneContext;
            UpdateActiveSceneLabel();

            _dialogueFeed.Clear();
            foreach (var h in session.History)
            {
                bool isA = h.SpeakerName.Equals(_charA.Name, StringComparison.OrdinalIgnoreCase);
                Character? speakerChar = isA ? _charA : _charB;
                Bitmap? speakerBitmap = null;
                if (speakerChar != null && !string.IsNullOrEmpty(speakerChar.AvatarPath) && File.Exists(speakerChar.AvatarPath))
                {
                    try { speakerBitmap = new Bitmap(speakerChar.AvatarPath); } catch { }
                }

                _dialogueFeed.Add(new DialogueMessageModel
                {
                    TurnIndex = h.TurnIndex,
                    SpeakerName = h.SpeakerName,
                    TargetName = h.TargetName,
                    Dialogue = h.Dialogue,
                    SomaticText = string.Join(", ", h.SomaticZones),
                    BondDeltaText = $"Bond {h.BondDelta}",
                    SpeakerEmotion = h.SpeakerEmotion,
                    SpeakerEmotionEmoji = h.SpeakerEmotionEmoji,
                    GoalStatusText = h.GoalStatus,
                    SpeakerColor = isA ? "#38BDF8" : "#F43F5E",
                    SpeakerBg = isA ? "#0F172A" : "#1F1123",
                    SpeakerBitmap = speakerBitmap
                });
            }

            UpdateCharacterCards();
            UpdateUserRoleDropdown();
            TxtStatus.Text = $"Session loaded from {Path.GetFileName(files[0])}";
        }
    }

    private void OnUserInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnSendInputClicked(sender, e);
        }
    }

    private void OnSendInputClicked(object? sender, RoutedEventArgs e)
    {
        string text = TxtUserInput.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;

        // Slash commands are system controls only — never dialogue, never LLM turns.
        if (PlayerCommandService.IsCommand(text))
        {
            TxtUserInput.Text = "";
            ExecutePlayerCommand(PlayerCommandService.Parse(text));
            return;
        }

        string role = ComboUserRole.SelectedItem?.ToString() ?? "👤 Player / DM";

        var msg = new DialogueMessageModel
        {
            TurnIndex = _dialogueFeed.Count + 1,
            SpeakerName = role,
            TargetName = _charA?.Name ?? "Character A",
            Dialogue = text,
            SomaticText = "Player Intervention",
            BondDeltaText = "Bond ±0",
            SpeakerEmotionEmoji = "👤",
            GoalStatusText = "User Input",
            SpeakerColor = "#F59E0B",
            SpeakerBg = "#2E1065",
            IsLeft = true
        };

        _dialogueFeed.Add(msg);
        TxtUserInput.Text = "";
        ScrollDialogue.ScrollToEnd();

        if (_controlContext.State == SimulationState.Ready || _activeTurnManager == null)
        {
            OnPlayClicked(this, new RoutedEventArgs());
        }

        _activeTurnManager?.InjectUserInput(role, text);

        TxtStatus.Text = $"User intervention sent: \"{text}\"";

        if (_controlContext.State == SimulationState.Paused)
        {
            _controlContext.Step();
        }
    }

    /// <summary>
    /// Runs a /command from the player input bar without injecting speech or advancing character turns
    /// (except /play and /step, which only control playback).
    /// </summary>
    private void ExecutePlayerCommand(PlayerCommand command)
    {
        switch (command.Kind)
        {
            case PlayerCommandKind.Help:
                PostSystemNotice("Commands", PlayerCommandService.GetHelpText());
                TxtStatus.Text = "Help: slash commands listed in the dialogue feed (not sent to characters).";
                break;

            case PlayerCommandKind.Play:
                OnPlayClicked(this, new RoutedEventArgs());
                PostSystemNotice("Command", "/play — start/resume playback");
                break;

            case PlayerCommandKind.Pause:
                OnPauseClicked(this, new RoutedEventArgs());
                PostSystemNotice("Command", "/pause — simulation paused");
                break;

            case PlayerCommandKind.Step:
                OnStepClicked(this, new RoutedEventArgs());
                PostSystemNotice("Command", "/step — advance one beat");
                break;

            case PlayerCommandKind.Stop:
                OnStopClicked(this, new RoutedEventArgs());
                PostSystemNotice("Command", "/stop — simulation stopped");
                break;

            case PlayerCommandKind.Reset:
                OnResetClicked(this, new RoutedEventArgs());
                PostSystemNotice("Command", "/reset — stage cleared");
                break;

            case PlayerCommandKind.Save:
                OnSaveSessionClicked(this, new RoutedEventArgs());
                PostSystemNotice("Command", "/save — session save requested");
                break;

            case PlayerCommandKind.Load:
                OnLoadSessionClicked(this, new RoutedEventArgs());
                PostSystemNotice("Command", "/load — session load requested");
                break;

            case PlayerCommandKind.Setup:
                _ = OpenSetupFromCommandAsync();
                break;

            case PlayerCommandKind.AutoPlay:
                OnSetAutoPlayMode(this, new RoutedEventArgs());
                PostSystemNotice("Command", "/auto — Auto-Play mode");
                break;

            case PlayerCommandKind.PlayerGuided:
                OnSetPlayerGuidedMode(this, new RoutedEventArgs());
                PostSystemNotice("Command", "/guided — Player-Guided mode");
                break;

            case PlayerCommandKind.Status:
                PostSystemNotice("Status", BuildStatusText());
                TxtStatus.Text = "Status written to dialogue feed.";
                break;

            case PlayerCommandKind.State:
                PostSystemNotice("State", PlayerCommandService.BuildCharacterStateReport(_charA!));
                TxtStatus.Text = "State report written to dialogue feed.";
                break;

            case PlayerCommandKind.Clear:
                _dialogueFeed.Clear();
                _turnHistory.Clear();
                PostSystemNotice("Command", "/clear — dialogue feed cleared");
                TxtStatus.Text = "Dialogue feed cleared (system notice only).";
                break;

            case PlayerCommandKind.Scene:
                if (command.Args.Length == 0 || string.IsNullOrWhiteSpace(command.Args[0]))
                {
                    var g = SceneGenreCatalog.GetById(_selectedGenre);
                    PostSystemNotice("Scene",
                        $"Genre: {g.DisplayName}\nScene: {_scenePrompt}\nUsage: /scene <place>\n       /genre <name>");
                    TxtStatus.Text = "Scene unchanged — provide text after /scene.";
                }
                else
                {
                    _scenePrompt = command.Args[0].Trim();
                    UpdateActiveSceneLabel();
                    SavePersistentSettings();
                    PostSystemNotice("Scene",
                        $"Scene set to: {_scenePrompt}\nGenre remains: {SceneGenreCatalog.GetById(_selectedGenre).DisplayName}\n(Identity unchanged.)");
                    TxtStatus.Text = "Scene updated via /scene (not sent as dialogue).";
                }
                break;

            case PlayerCommandKind.Genre:
                if (command.Args.Length == 0 || string.IsNullOrWhiteSpace(command.Args[0]))
                {
                    string list = string.Join(", ", SceneGenreCatalog.All.Select(x => x.DisplayName));
                    PostSystemNotice("Genre",
                        $"Current: {SceneGenreCatalog.GetById(_selectedGenre).DisplayName}\nOptions: {list}\nUsage: /genre <name>");
                    TxtStatus.Text = "Genre unchanged — provide a genre name after /genre.";
                }
                else
                {
                    var genre = SceneGenreCatalog.GetByDisplayName(command.Args[0].Trim());
                    _selectedGenre = genre.Id;
                    // Offer first preset for the new genre without forcing if custom freeform preferred
                    if (genre.Id != "custom" && genre.ScenePresets.Count > 0 &&
                        (_scenePrompt.StartsWith("Neon alley", StringComparison.OrdinalIgnoreCase)
                         || string.IsNullOrWhiteSpace(_scenePrompt)))
                    {
                        _scenePrompt = genre.ScenePresets[0];
                    }
                    UpdateActiveSceneLabel();
                    SavePersistentSettings();
                    PostSystemNotice("Genre",
                        $"Genre set to: {genre.DisplayName}\n{genre.Description}\nScene place: {_scenePrompt}\n(Genre is environment only — characters stay themselves.)");
                    TxtStatus.Text = $"Genre → {genre.DisplayName} (not sent as dialogue).";
                }
                break;

            case PlayerCommandKind.Adult:
                if (command.Args.Length == 0 || string.IsNullOrWhiteSpace(command.Args[0]))
                {
                    bool current = AdultAuth.IsUserAdultAttested;
                    PostSystemNotice("Adult",
                        $"Current user adult attestation: {(current ? "ON" : "OFF")}\nUsage: /adult on|off");
                    TxtStatus.Text = "Adult attestation unchanged — provide on or off after /adult.";
                }
                else
                {
                    string arg = command.Args[0].Trim().ToLowerInvariant();
                    bool enable = arg == "on" || arg == "true" || arg == "yes" || arg == "1";
                    bool disable = arg == "off" || arg == "false" || arg == "no" || arg == "0";
                    
                    if (enable)
                    {
                        AdultAuth.SetUserAdultAttested(true);
                        PostSystemNotice("Adult", "User adult content attestation: ENABLED");
                        TxtStatus.Text = "Adult attestation ON — adult paths authorized for eligible characters.";
                    }
                    else if (disable)
                    {
                        AdultAuth.SetUserAdultAttested(false);
                        PostSystemNotice("Adult", "User adult content attestation: DISABLED");
                        TxtStatus.Text = "Adult attestation OFF — all adult paths blocked.";
                    }
                    else
                    {
                        PostSystemNotice("Adult", "Invalid argument. Usage: /adult on|off");
                        TxtStatus.Text = "Invalid adult command argument.";
                    }
                }
                break;

            case PlayerCommandKind.Unknown:
            default:
                PostSystemNotice("Unknown command",
                    $"/{command.RawName} is not recognized.\nType /help for the command list.\n(Nothing was sent to any character.)");
                TxtStatus.Text = $"Unknown command /{command.RawName} — not sent to characters.";
                break;
        }
    }

    private async Task OpenSetupFromCommandAsync()
    {
        PostSystemNotice("Command", "/setup — opening setup…");
        OnOpenSetupClicked(this, new RoutedEventArgs());
        await Task.CompletedTask;
    }

    private string BuildStatusText()
    {
        string mode = _isPlayerGuidedMode ? "Player-Guided" : "Auto-Play";
        string charA = _charA?.Name ?? _selectedCharA;
        string charB = _selectedCharB.StartsWith("None", StringComparison.OrdinalIgnoreCase)
            ? "Solo (Player)"
            : (_charB?.Name ?? _selectedCharB);
        var genre = SceneGenreCatalog.GetById(_selectedGenre);
        return string.Join("\n", new[]
        {
            $"State: {_controlContext.State}",
            $"Mode: {mode}",
            $"Genre: {genre.DisplayName} (environment only)",
            $"Scene: {_scenePrompt}",
            $"Characters: {charA} / {charB}",
            $"Cards: {_selectedCharA} / {_selectedCharB}",
            $"LLMs: {_selectedLlmA} / {_selectedLlmB}",
            $"Max turns: {_maxTurns}",
            $"Delay: {_controlContext.DelayMs}ms",
            $"Feed messages: {_dialogueFeed.Count}",
        });
    }

    private void PostSystemNotice(string title, string body)
    {
        _dialogueFeed.Add(new DialogueMessageModel
        {
            TurnIndex = 0,
            SpeakerName = $"⚙️ {title}",
            TargetName = "System",
            Dialogue = body,
            SomaticText = "Command",
            BondDeltaText = "",
            SpeakerEmotionEmoji = "⚙️",
            GoalStatusText = "System",
            SpeakerColor = "#94A3B8",
            SpeakerBg = "#111827",
            IsLeft = true
        });
        ScrollDialogue.ScrollToEnd();
    }

    private void OnDelaySliderChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (SliderDelay != null && TxtDelayVal != null)
        {
            int val = (int)e.NewValue;
            TxtDelayVal.Text = $"{val}ms";
            _controlContext.DelayMs = val;
            SavePersistentSettings();
        }
    }

    private void OnSimulationStateChanged(SimulationState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            switch (state)
            {
                case SimulationState.Running:
                    TxtBadgeStatus.Text = "● RUNNING";
                    BadgeStatus.Background = SolidColorBrush.Parse("#065F46");
                    TxtBadgeStatus.Foreground = SolidColorBrush.Parse("#34D399");
                    BtnPlay.IsEnabled = false;
                    BtnPause.IsEnabled = true;
                    BtnStep.IsEnabled = true;
                    BtnStop.IsEnabled = true;
                    break;

                case SimulationState.Paused:
                    TxtBadgeStatus.Text = "❚❚ PAUSED";
                    BadgeStatus.Background = SolidColorBrush.Parse("#78350F");
                    TxtBadgeStatus.Foreground = SolidColorBrush.Parse("#FBBF24");
                    BtnPlay.IsEnabled = true;
                    BtnPause.IsEnabled = false;
                    BtnStep.IsEnabled = true;
                    BtnStop.IsEnabled = true;
                    break;

                case SimulationState.Stopped:
                    TxtBadgeStatus.Text = "■ STOPPED";
                    BadgeStatus.Background = SolidColorBrush.Parse("#7F1D1D");
                    TxtBadgeStatus.Foreground = SolidColorBrush.Parse("#FCA5A5");
                    BtnPlay.IsEnabled = true;
                    BtnPause.IsEnabled = false;
                    BtnStep.IsEnabled = true;
                    BtnStop.IsEnabled = false;
                    break;

                case SimulationState.Ready:
                default:
                    TxtBadgeStatus.Text = "● READY";
                    BadgeStatus.Background = SolidColorBrush.Parse("#1E293B");
                    TxtBadgeStatus.Foreground = SolidColorBrush.Parse("#94A3B8");
                    BtnPlay.IsEnabled = true;
                    BtnPause.IsEnabled = false;
                    BtnStep.IsEnabled = true;
                    BtnStop.IsEnabled = false;
                    break;
            }
        });
    }

    private string TruncateText(string str, int max)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Length <= max ? str : str.Substring(0, max - 1) + "…";
    }

    private void UpdateCharacterCards()
    {
        if (_charA != null)
        {
            TxtCharAName.Text = _charA.Name;
            TxtCharAState.Text = $"State: {_charA.CurrentState}";
            TxtCharAEmotion.Text = _charA.Emotion;
            TxtCharAEmoji.Text = _charA.EmotionEmoji;
            TxtCharABond.Text = _charA.Bond.ToString();
            ProgressCharABond.Value = _charA.Bond;
            TxtCharAStress.Text = _charA.Stress.ToString();
            ProgressCharAStress.Value = _charA.Stress;
            TxtCharAArousal.Text = _charA.Arousal.ToString();
            ProgressCharAArousal.Value = _charA.Arousal;
            TxtCharASomatic.Text = _charA.SomaticZones.Count > 0 ? string.Join(", ", _charA.SomaticZones) : "Calm / None";

            if (!string.IsNullOrEmpty(_charA.AvatarPath) && File.Exists(_charA.AvatarPath))
            {
                try
                {
                    ImgCharAPortrait.Source = new Bitmap(_charA.AvatarPath);
                }
                catch { }
            }

            _charAGoals.Clear();
            foreach (var g in _charA.Goals)
            {
                _charAGoals.Add(new GoalViewModel
                {
                    Title = $"{g.Type} ➔ {g.Target} (P:{g.Priority}/I:{g.Intensity})",
                    Description = $"Cooldown: {g.CooldownRemaining}t | Strategies: {string.Join(", ", g.Strategies)}"
                });
            }
        }

        bool isSoloMode = _selectedCharB.StartsWith("None", StringComparison.OrdinalIgnoreCase);
        CardCharB.IsVisible = !isSoloMode;

        if (_charB != null && !isSoloMode)
        {
            TxtCharBName.Text = _charB.Name;
            TxtCharBState.Text = $"State: {_charB.CurrentState}";
            TxtCharBEmotion.Text = _charB.Emotion;
            TxtCharBEmoji.Text = _charB.EmotionEmoji;
            TxtCharBBond.Text = _charB.Bond.ToString();
            ProgressCharBBond.Value = _charB.Bond;
            TxtCharBStress.Text = _charB.Stress.ToString();
            ProgressCharBStress.Value = _charB.Stress;
            TxtCharBArousal.Text = _charB.Arousal.ToString();
            ProgressCharBArousal.Value = _charB.Arousal;
            TxtCharBSomatic.Text = _charB.SomaticZones.Count > 0 ? string.Join(", ", _charB.SomaticZones) : "Calm / None";

            if (!string.IsNullOrEmpty(_charB.AvatarPath) && File.Exists(_charB.AvatarPath))
            {
                try
                {
                    ImgCharBPortrait.Source = new Bitmap(_charB.AvatarPath);
                }
                catch { }
            }

            _charBGoals.Clear();
            foreach (var g in _charB.Goals)
            {
                _charBGoals.Add(new GoalViewModel
                {
                    Title = $"{g.Type} ➔ {g.Target} (P:{g.Priority}/I:{g.Intensity})",
                    Description = $"Cooldown: {g.CooldownRemaining}t | Strategies: {string.Join(", ", g.Strategies)}"
                });
            }
        }
    }
}
