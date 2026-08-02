using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterSimulator.Logic;
using CharacterSimulator.Logic.Safety;
using Spectre.Console;

namespace CharacterSimulator.TUI;

public class TerminalUi
{
    public void Run()
    {
        AnsiConsole.Clear();
        RenderHeader();

        // 1. Discover Character cards (opaque file ids; labels from card name field)
        string charDir = CharacterCatalog.ResolveCharactersDirectory();
        var cards = CharacterCatalog.ListCards();

        if (cards.Count == 0)
        {
            AnsiConsole.MarkupLine("[bold red]No character files found in Characters/ folder![/]");
            return;
        }

        // Map selector labels (display name) -> filename. Disambiguate duplicate names with short id.
        var labelCounts = cards.GroupBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        string LabelFor(CharacterCatalog.CharacterCardRef c) =>
            labelCounts[c.DisplayName] > 1
                ? $"{c.DisplayName} ({c.CardId[..Math.Min(8, c.CardId.Length)]})"
                : c.DisplayName;

        var labelToFile = cards.ToDictionary(LabelFor, c => c.FileName, StringComparer.OrdinalIgnoreCase);
        var labelOptions = cards.Select(LabelFor).ToList();

        // 2. Interactive Character Selection
        var charALabel = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold cyan]Select Character 1 (Player A):[/]")
                .PageSize(12)
                .AddChoices(labelOptions));
        string charAFile = labelToFile[charALabel];
        string charADisplay = CharacterCatalog.GetCharacterDisplayName(charAFile);

        var charBLabelOptions = new List<string> { "None (Solo Roleplay)" };
        charBLabelOptions.AddRange(labelOptions.Where(l => l != charALabel));
        if (charBLabelOptions.Count == 1) charBLabelOptions.AddRange(labelOptions);

        var charBLabel = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold magenta]Select Character 2 (Player B):[/]")
                .PageSize(12)
                .AddChoices(charBLabelOptions));
        string charBFile = charBLabel.StartsWith("None", StringComparison.OrdinalIgnoreCase)
            ? charBLabel
            : labelToFile[charBLabel];
        string charBDisplay = charBFile.StartsWith("None", StringComparison.OrdinalIgnoreCase)
            ? "Solo"
            : CharacterCatalog.GetCharacterDisplayName(charBFile);

        // 3. Interactive System LLM Discovery Selection
        var llmOptions = LlmDiscoveryService.GetAvailableProviderNames();

        var clientChoiceA = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold cyan]Select Installed LLM Provider for {charADisplay}:[/]")
                .AddChoices(llmOptions));

        string clientChoiceB = clientChoiceA;
        if (!charBFile.StartsWith("None", StringComparison.OrdinalIgnoreCase))
        {
            clientChoiceB = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold magenta]Select Installed LLM Provider for {charBDisplay}:[/]")
                    .AddChoices(llmOptions));
        }

        // 4. Genre (environment tone only) then scene place
        var genreChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold purple]Select Scene Genre (environment only — characters stay themselves):[/]")
                .PageSize(12)
                .AddChoices(SceneGenreCatalog.DisplayNames));

        var genre = SceneGenreCatalog.GetByDisplayName(genreChoice);
        var sceneChoices = genre.ScenePresets.Append("Custom Scene Prompt...").ToList();

        var sceneChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold yellow]Scene place for {genre.DisplayName}:[/]")
                .AddChoices(sceneChoices));

        string scenePlace = sceneChoice;
        if (sceneChoice == "Custom Scene Prompt...")
        {
            scenePlace = AnsiConsole.Ask<string>("[bold yellow]Enter custom scene place:[/", genre.ScenePresets.FirstOrDefault() ?? "Quiet room");
        }

        string sceneContext = SceneGenreCatalog.ComposeSceneContext(genre.Id, scenePlace);

        int maxTurns = AnsiConsole.Ask<int>("[bold green]Enter max simulation turns (1-20):[/]", 8);

        bool allowAdult = AnsiConsole.Confirm("[bold red]Enable Adult Mode (/adult on)? (Requires 18+ attestation)[/]", false);
        AdultAuth.SetUserAdultAttested(allowAdult);

        // Load Characters
        var charA = CharacterLoader.Load(Path.Combine(charDir, charAFile));
        Character? charB = null;
        bool isSolo = charBFile.StartsWith("None", StringComparison.OrdinalIgnoreCase);
        if (!isSolo)
            charB = CharacterLoader.Load(Path.Combine(charDir, charBFile));

        ILLMClient clientA = LlmDiscoveryService.CreateClient(clientChoiceA);
        ILLMClient? clientB = isSolo ? null : LlmDiscoveryService.CreateClient(clientChoiceB);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        string logPath = Path.Combine("Output", $"conversation_tui_{timestamp}.log");
        var logger = new Logger(logPath);
        var sceneManager = new SceneManager();
        var turnManager = new TurnManager(clientA, clientB, sceneManager, logger);

        AnsiConsole.Clear();
        string vsLabel = isSolo ? "Player (solo)" : charB!.Name;
        AnsiConsole.MarkupLine($"[bold underline green]Starting Simulation:[/] [cyan]{charA.Name}[/] vs [magenta]{vsLabel}[/]");
        AnsiConsole.MarkupLine($"[bold yellow]Active LLM Providers:[/] [cyan]Agent A: {clientChoiceA}[/] | [magenta]Agent B: {(isSolo ? "Player" : clientChoiceB)}[/]");
        AnsiConsole.MarkupLine($"[dim]Genre:[/] {genre.DisplayName} [dim](environment only)[/]");
        AnsiConsole.MarkupLine($"[dim]Scene:[/] {scenePlace}");
        AnsiConsole.MarkupLine($"[dim]Log file:[/] [link]{logPath}[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold yellow]Turn / Speaker[/]").Width(18))
            .AddColumn(new TableColumn("[bold white]Dialogue & Reaction[/]"))
            .AddColumn(new TableColumn("[bold cyan]Bond Delta[/]").Width(12))
            .AddColumn(new TableColumn("[bold green]Goal Update[/]").Width(22));

        turnManager.OnTurnStep += (e) =>
        {
            string speakerColor = e.SpeakerName.Equals(charA.Name, StringComparison.OrdinalIgnoreCase) ? "cyan" : "magenta";
            string somaticBadge = e.SomaticZones.Count > 0 ? $" [dim italic]({string.Join(", ", e.SomaticZones)})[/]" : "";
            string bondBadge = e.BondDelta >= 0 ? $"[green]+{e.BondDelta}[/] ({e.CurrentBond})" : $"[red]{e.BondDelta}[/] ({e.CurrentBond})";
            string goalText = string.IsNullOrEmpty(e.ActiveGoalType) ? "[dim]None[/]" : $"[{speakerColor}]{e.ActiveGoalType}[/]\n[dim]{e.GoalStatus}[/]";

            table.AddRow(
                $"[bold {speakerColor}]T{e.TurnIndex} {e.SpeakerName}[/]",
                $"[{speakerColor}]\"{Markup.Escape(e.Dialogue)}\"[/]{somaticBadge}",
                bondBadge,
                goalText
            );

            AnsiConsole.Clear();
            RenderHeader();
            if (isSolo || charB == null)
                RenderSoloCard(charA);
            else
                RenderCharacterCards(charA, charB);
            AnsiConsole.Write(table);
            System.Threading.Thread.Sleep(300);
        };

        turnManager.OnGoalEvaluated += (g) =>
        {
            string outcome = g.IsSuccess ? "[bold green]★ GOAL SUCCESS[/]" : "[bold red]✖ GOAL FAILED[/]";
            AnsiConsole.MarkupLine($"\n{outcome} [bold white]{g.CharacterName}[/] evaluated goal [yellow]{g.GoalType}[/] on [white]{g.TargetName}[/]!");
        };

        // Run simulation (genre is already composed into sceneContext)
        turnManager.RunConversation(charA, charB, sceneContext, maxTurns);

        AnsiConsole.MarkupLine("\n[bold green]Simulation Complete![/]");
        AnsiConsole.MarkupLine($"Log saved to: [underline cyan]{Path.GetFullPath(logPath)}[/]");
        AnsiConsole.Prompt(new TextPrompt<string>("[dim]Press Enter to exit...[/]").AllowEmpty());
    }

    private void RenderHeader()
    {
        var figlet = new FigletText("SIMULATOR")
            .Color(Color.Cyan1);
        AnsiConsole.Write(figlet);
        AnsiConsole.Write(new Rule("[bold blue]Character Simulator TUI Engine[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();
    }

    private void RenderCharacterCards(Character charA, Character charB)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        var cardA = new Panel(
            $"[bold cyan]Name:[/] {Markup.Escape(charA.Name)}\n" +
            $"[bold cyan]Focus/State:[/] {Markup.Escape(charA.CurrentState)}\n" +
            $"[bold cyan]Bias Lens:[/] [yellow]{Markup.Escape(charA.BiasState)}[/]\n" +
            $"[bold cyan]Bond:[/] [yellow]{charA.Bond}[/]\n" +
            $"[bold cyan]Somatic:[/] {Markup.Escape(string.Join(", ", charA.SomaticZones))}")
            .Header($"[bold cyan]{Markup.Escape(charA.Name)}[/]")
            .BorderColor(Color.Cyan1);

        var cardB = new Panel(
            $"[bold magenta]Name:[/] {Markup.Escape(charB.Name)}\n" +
            $"[bold magenta]Focus/State:[/] {Markup.Escape(charB.CurrentState)}\n" +
            $"[bold magenta]Bias Lens:[/] [yellow]{Markup.Escape(charB.BiasState)}[/]\n" +
            $"[bold magenta]Bond:[/] [yellow]{charB.Bond}[/]\n" +
            $"[bold magenta]Somatic:[/] {Markup.Escape(string.Join(", ", charB.SomaticZones))}")
            .Header($"[bold magenta]{Markup.Escape(charB.Name)}[/]")
            .BorderColor(Color.Magenta1);

        grid.AddRow(cardA, cardB);
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();
    }

    private void RenderSoloCard(Character charA)
    {
        var cardA = new Panel(
            $"[bold cyan]Name:[/] {Markup.Escape(charA.Name)}\n" +
            $"[bold cyan]Focus/State:[/] {Markup.Escape(charA.CurrentState)}\n" +
            $"[bold cyan]Bias Lens:[/] [yellow]{Markup.Escape(charA.BiasState)}[/]\n" +
            $"[bold cyan]Bond:[/] [yellow]{charA.Bond}[/]\n" +
            $"[bold cyan]Somatic:[/] {Markup.Escape(string.Join(", ", charA.SomaticZones))}")
            .Header($"[bold cyan]{Markup.Escape(charA.Name)} · solo[/]")
            .BorderColor(Color.Cyan1);
        AnsiConsole.Write(cardA);
        AnsiConsole.WriteLine();
    }
}
