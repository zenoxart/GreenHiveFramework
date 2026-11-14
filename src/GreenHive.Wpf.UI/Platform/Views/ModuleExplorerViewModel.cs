using GreenHive.NET.CommandContracts;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.UI.Platform;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Navigation;

namespace GreenHive.Wpf.UI.Platform.Views;

/// <summary>ViewModel of the Module Explorer: the command tree of placement "shell.navigation".</summary>
public sealed class ModuleExplorerViewModel : ViewModelAppObject
{
    /// <summary>Raised when the tree must be rebuilt.</summary>
    public event EventHandler? TreeChanged;

    public RelayCommandPool Commands => Context<ClientAppContext>().Commands;

    public IReadOnlyList<CommandTreeNode> Tree => Commands.GetTree(NavigationRegistry.NavigationPlacement);

    public string Label(CommandTreeNode node) => CommandTreeAdapters.Label(Commands, node);

    public void Execute(CommandTreeNode node)
    {
        if (node.CommandId is not null)
        {
            _ = Commands.ExecuteAsync(node.CommandId);
        }
    }

    protected override void OnCreated() =>
        Commands.TreeChanged += (_, placement) =>
        {
            if (placement is "*" or NavigationRegistry.NavigationPlacement)
            {
                TreeChanged?.Invoke(this, EventArgs.Empty);
            }
        };
}
