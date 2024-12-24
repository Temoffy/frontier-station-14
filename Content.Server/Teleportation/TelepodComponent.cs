using Content.Shared.Construction.Prototypes;
using Content.Shared.DragDrop;
using Content.Shared.Fax.Components;
using Content.Shared.Teleportation.Components;
using Robust.Shared.Containers;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Teleportation
{
    [RegisterComponent]
    public sealed partial class TelepodComponent : SharedTelepodComponent
    {
        /// <summary>
        /// Name with which the telepod will be visible to others on the network
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("name")]
        public string FaxName { get; set; } = "Unknown";

        /// <summary>
        /// Device address of telepod in network to which request will be send
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("destinationAddress")]
        public string? DestinationFaxAddress { get; set; }

        /// <summary>
        /// player or animal to be sent
        /// </summary>
        public ContainerSlot BodyContainer = default!;

        /// <summary>
        /// should respond to pings in network (based off of faxing)
        /// This will make it visible to others on the network
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public bool ResponsePings { get; set; } = true;

        /// <summary>
        /// Known telepods in network by address with names
        /// </summary>
        [ViewVariables]
        public Dictionary<string, string> KnownTelepods { get; } = new();

        /// <summary>
        /// queue of the incoming teleport requests
        /// </summary>
        [ViewVariables]
        [DataField]
        public Queue<FaxPrintout> TeleportQueue { get; private set; } = new();

        /// <summary>
        /// remaining outbound timeout
        /// </summary>
        [ViewVariables]
        [DataField]
        public float OutboundTimeoutRemaining;

        /// <summary>
        /// Outbound timeout
        /// </summary>
        [ViewVariables]
        [DataField]
        public float OutboundTimeout = 15f; //TODO: preferably change to require power or fuel
    }
}
