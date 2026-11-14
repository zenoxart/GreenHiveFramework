namespace GreenHive.Wpf.Framework.Commands;

/// <summary>Implemented by application contexts that provide the central command manager.</summary>
public interface ICommandContext
{
    RelayCommandPool Commands { get; }
}
