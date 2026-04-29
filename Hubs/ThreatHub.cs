using Microsoft.AspNetCore.SignalR;

namespace naija_shield_backend.Hubs;

/// <summary>
/// SignalR hub that the Enterprise Dashboard connects to in order to receive
/// real-time threat notifications. The backend does not accept messages from
/// clients — all communication flows server → client only.
///
/// Clients listen for the "NewThreatDetected" event which carries a
/// <see cref="Models.ThreatFeedEvent"/> payload matching the Threat Feed table columns.
///
/// Hub URL (mapped in Program.cs): /hubs/threat
/// SignalR transport: Azure SignalR Service (connection string from Key Vault).
/// </summary>
public sealed class ThreatHub : Hub
{
    // No inbound client methods required — server pushes only via IHubContext<ThreatHub>.
}
