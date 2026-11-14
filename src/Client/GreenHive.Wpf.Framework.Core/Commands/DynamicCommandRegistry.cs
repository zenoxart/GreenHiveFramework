using System.Collections.Concurrent;
using GreenHive.NET.CommandContracts;

namespace GreenHive.Wpf.Framework.Commands;

/// <summary>A registered command: owner module, metadata and current implementation.</summary>
public sealed record CommandRegistration(string ModuleId, CommandDescriptor Descriptor, ICommandHandler Handler);

/// <summary>
/// Maps command IDs to the currently valid implementation. A module may replace its own commands
/// (hot-swap); a command ID owned by another module is never overwritten.
/// </summary>
public sealed class DynamicCommandRegistry
{
    private readonly ConcurrentDictionary<string, CommandRegistration> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (string ModuleId, CommandGroupDescriptor Group)> _groups = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CommandRegistration> All => _commands.Values.ToList();

    public IReadOnlyCollection<CommandGroupDescriptor> Groups => _groups.Values.Select(g => g.Group).ToList();

    public bool TryRegister(string moduleId, CommandDescriptor descriptor, ICommandHandler handler, out string? conflictingModule)
    {
        conflictingModule = null;
        if (_commands.TryGetValue(descriptor.Id, out var existing) && !existing.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase))
        {
            conflictingModule = existing.ModuleId;
            return false;
        }

        _commands[descriptor.Id] = new CommandRegistration(moduleId, descriptor, handler);
        return true;
    }

    public void RegisterGroup(string moduleId, CommandGroupDescriptor group) =>
        _groups[$"{group.Placement}|{group.Path}"] = (moduleId, group);

    public CommandRegistration? Find(string commandId) =>
        _commands.TryGetValue(commandId, out var registration) ? registration : null;

    /// <summary>Removes all commands and groups of a module and returns the removed command IDs.</summary>
    public IReadOnlyList<string> UnregisterModule(string moduleId)
    {
        var removed = new List<string>();
        foreach (var (id, registration) in _commands)
        {
            if (registration.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase) && _commands.TryRemove(id, out _))
            {
                removed.Add(id);
            }
        }

        foreach (var (key, group) in _groups)
        {
            if (group.ModuleId.Equals(moduleId, StringComparison.OrdinalIgnoreCase))
            {
                _groups.TryRemove(key, out _);
            }
        }

        return removed;
    }
}
