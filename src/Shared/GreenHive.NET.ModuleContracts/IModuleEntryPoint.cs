namespace GreenHive.NET.ModuleContracts;

/// <summary>Entry point of every client module (manifest field client.entryType).</summary>
public interface IModuleEntryPoint
{
    /// <summary>Registers services, commands, navigation targets, views, icons, texts and event subscriptions.</summary>
    void Initialize(IModuleContext context);

    /// <summary>The module's generated XAML metadata provider (compiledXaml) or null.</summary>
    object? GetXamlMetadataProvider() => null;

    /// <summary>Releases resources, closes connections and detaches handlers.</summary>
    Task ShutdownAsync() => Task.CompletedTask;
}
