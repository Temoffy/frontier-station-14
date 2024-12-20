using Content.Shared.DragDrop;
using Robust.Shared.Serialization;

namespace Content.Shared.Teleportation.Components
{
    public abstract partial class SharedTelepodComponent : Component
    {
        [Serializable, NetSerializable]
        public enum TelepodVisuals : byte
        {
            Status
        }

        [Serializable, NetSerializable]
        public enum TelepodStatus : byte
        {
            Off,
            Open,
            Red,
            Death,
            Green,
            Yellow,
        }
    }
}

