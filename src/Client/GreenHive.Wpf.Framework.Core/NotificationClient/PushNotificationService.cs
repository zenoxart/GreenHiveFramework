using System.Text.Json;
using GreenHive.NET.Contracts;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.NET.NotificationContracts;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.Wpf.Framework.NotificationClient;

/// <summary>
/// A Windows push (WNS) channel. WNS needs package identity, so a plain WPF host has none; an MSIX-packaged host can
/// plug in an implementation (for example based on the Windows App SDK PushNotificationManager).
/// </summary>
public interface IPushChannelProvider
{
    /// <summary>Raised with the raw payload (UTF-8 JSON of a NotificationEnvelope) of a received push.</summary>
    event EventHandler<string>? PushReceived;

    /// <summary>Creates the channel and returns its URI, or null when push is not available.</summary>
    Task<Uri?> CreateChannelAsync(Guid remoteId, CancellationToken ct = default);
}

/// <summary>
/// Optional Windows push (WNS): registers a push channel and sends its URI to the NotificationPoint, which pushes events
/// for this user while no live connection exists. Needs the Azure app ID of the WNS registration (RemoteId) and an
/// <see cref="IPushChannelProvider"/>; without them the service stays inactive and events are delivered by catch-up
/// when the app reconnects.
/// </summary>
public sealed class PushNotificationService : AppObject
{
    public Guid? RemoteId { get; set; }

    public IPushChannelProvider? Provider { get; set; }

    public async Task RegisterAsync()
    {
        if (RemoteId is not { } remoteId || Provider is not { } provider)
        {
            return;
        }

        try
        {
            provider.PushReceived += (_, json) =>
            {
                if (JsonSerializer.Deserialize<NotificationEnvelope>(json, GreenHiveJson.Options) is { } envelope)
                {
                    AppContext.Dispatcher?.Run(() => Context<INotificationContext>().Router.Publish(envelope));
                }
            };

            if (await provider.CreateChannelAsync(remoteId) is { } channelUri)
            {
                var registered = await Context<ISecurityContext>().Backend.PostAsync<JsonElement?>("/api/notifications/channels",
                    new { channelUri = channelUri.ToString() });
                AppContext.Log.Write(registered.IsSuccess ? "Push channel registered" : $"Push channel registration failed: {registered.Error}",
                    registered.IsSuccess ? LogEntryType.Information : LogEntryType.Warning, nameof(PushNotificationService));
            }
        }
        catch (Exception ex)
        {
            AppContext.Log.Write($"Push notifications are not available: {ex.Message}", LogEntryType.Warning, nameof(PushNotificationService));
        }
    }
}
