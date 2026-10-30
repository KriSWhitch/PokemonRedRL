using Microsoft.Extensions.Configuration;

namespace PokemonRedRL.Agent;

public static class AgentRuntimeConfiguration
{
    public static ConfigurationRoot Load(string[] args, string? baseDirectory = null, string? workingDirectory = null)
    {
        baseDirectory = Path.GetFullPath(baseDirectory ?? AppContext.BaseDirectory);
        workingDirectory = Path.GetFullPath(workingDirectory ?? Environment.CurrentDirectory);
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        var builder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(baseDirectory, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(baseDirectory, $"appsettings.{environment}.json"), optional: true);
        if (!string.Equals(baseDirectory, workingDirectory, StringComparison.OrdinalIgnoreCase))
        {
            builder.AddJsonFile(Path.Combine(workingDirectory, "appsettings.json"), optional: true)
                .AddJsonFile(Path.Combine(workingDirectory, $"appsettings.{environment}.json"), optional: true);
        }
        return (ConfigurationRoot)builder.AddEnvironmentVariables().AddCommandLine(args).Build();
    }

    public static bool IsDiagnostic(IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("Vision:Enabled");
        var diagnostic = configuration.GetValue<bool>("vision-diagnostics");
        if (diagnostic && !enabled)
            throw new InvalidOperationException("Diagnostics require Vision.Enabled=true (--Vision:Enabled true).");
        if (enabled && !diagnostic)
            throw new InvalidOperationException("Vision learning is not available until S11; use --vision-diagnostics true.");
        return diagnostic;
    }
}