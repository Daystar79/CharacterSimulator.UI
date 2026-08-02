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

        // Register core logic services
        builder.Services.AddSingleton<ProfileService>(ProfileService.Instance);
        builder.Services.AddSingleton<TurnControlContext>();

        // Build root app component
        builder.RootComponents.Add<App>("#app");

        var app = builder.Build();

        // Configure native desktop window properties
        app.MainWindow
            .SetTitle("Character Simulator Studio — Blazor Desktop")
            .SetSize(1400, 900)
            .SetUseOsDefaultSize(false)
            .Center();

        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
        {
            Console.WriteLine($"[FATAL ERROR] {error.ExceptionObject}");
        };

        app.Run();
    }
}
