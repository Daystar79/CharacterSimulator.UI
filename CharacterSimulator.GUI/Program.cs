using System;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using CharacterSimulator.Logic.Services;
using CharacterSimulator.Logic;

namespace CharacterSimulator.GUI;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        // Lazy factory only — never touch SQLite / Instance before first UI paint.
        builder.Services.AddSingleton<ProfileService>(_ => ProfileService.Instance);
        builder.Services.AddSingleton<TurnControlContext>(_ =>
        {
            try { return new TurnControlContext(); }
            catch (Exception ex)
            {
                Console.WriteLine("[FATAL] TurnControlContext: " + ex);
                return new TurnControlContext(); // LoadSettings is fail-soft; should not throw
            }
        });
        // Simulation host owns TurnManager run loop + player commands for the GUI.
        builder.Services.AddSingleton<SimulationHost>(sp =>
            new SimulationHost(sp.GetRequiredService<TurnControlContext>()));

        builder.RootComponents.Add<App>("#app");

        var app = builder.Build();

        app.MainWindow
            .SetTitle("Character Simulator Studio — Blazor Desktop")
            .SetSize(1400, 900)
            .SetUseOsDefaultSize(false)
            .Center();

        AppDomain.CurrentDomain.UnhandledException += (_, error) =>
        {
            Console.WriteLine($"[FATAL ERROR] {error.ExceptionObject}");
        };

        try
        {
            app.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[FATAL] app.Run: " + ex);
            throw;
        }
    }
}
