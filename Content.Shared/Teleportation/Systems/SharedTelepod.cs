using Robust.Shared.Serialization;

namespace Content.Shared.Teleportation.Systems;

[Serializable, NetSerializable]
public enum TelepodUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class TelepodUiState : BoundUserInterfaceState
{
    public string DeviceName { get; }
    public Dictionary<string, string> AvailablePeers { get; }
    public string? DestinationAddress { get; }
    public bool IsBodyInserted { get; }
    public bool CanSend { get; }

    public TelepodUiState(string deviceName,
        Dictionary<string, string> peers,
        bool canSend,
        bool isBodyInserted,
        string? destAddress)
    {
        DeviceName = deviceName;
        AvailablePeers = peers;
        IsBodyInserted = isBodyInserted;
        CanSend = canSend;
        DestinationAddress = destAddress;
    }
}

[Serializable, NetSerializable]
public sealed class FaxCopyMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class FaxSendMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class FaxRefreshMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class FaxDestinationMessage : BoundUserInterfaceMessage
{
    public string Address { get; }

    public FaxDestinationMessage(string address)
    {
        Address = address;
    }
}
