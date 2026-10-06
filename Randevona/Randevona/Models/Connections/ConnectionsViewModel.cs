using Domain.Models.Meta;

namespace Randevona.Models.Connections;

public sealed class ConnectionsViewModel
{
    public ConnectionPage? Connections { get; init; }
    public ConnectionNumber? Editing { get; init; }
    public string? Error { get; init; }
}
