using Content.Server.Cloning;
using Content.Server.Teleportation;
using Content.Shared.Destructible;
using Content.Shared.ActionBlocker;
using Content.Shared.DragDrop;
using Content.Shared.UserInterface;
using Content.Shared.Movement.Events;
using Content.Shared.Teleportation.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Content.Server.Cloning.Components;
using Content.Server.Construction;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking.Events;
using Content.Server.Power.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using static Content.Shared.Teleportation.Components.SharedTelepodComponent;
using System.Reflection.PortableExecutable; // Hmm... //dunno what the hmm is about, it was there when I frankencoded this from the medscanner -Temoffy

namespace Content.Server.Teleportation
{
    public sealed class TelepodSystem : EntitySystem
    {
        [Dependency] private readonly DeviceLinkSystem _signalSystem = default!;
        [Dependency] private readonly ActionBlockerSystem _blocker = default!;
        [Dependency] private readonly ClimbSystem _climbSystem = default!;
        [Dependency] private readonly CloningConsoleSystem _cloningConsoleSystem = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly ContainerSystem _containerSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterface = default!;

        private const float UpdateRate = 1f;
        private float _updateDif;

        public override void Initialize()
        {
            base.Initialize();

            //management
            SubscribeLocalEvent<TelepodComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<TelepodComponent, DestructionEventArgs>(OnDestroyed);

            //In/out of machine
            SubscribeLocalEvent<TelepodComponent, GetVerbsEvent<InteractionVerb>>(AddInsertOtherVerb);
            SubscribeLocalEvent<TelepodComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
            SubscribeLocalEvent<TelepodComponent, DragDropTargetEvent>(OnDragDropOn);
            SubscribeLocalEvent<TelepodComponent, CanDropTargetEvent>(OnCanDragDropOn);
            SubscribeLocalEvent<TelepodComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);

            //SubscribeLocalEvent<TelepodComponent, PortDisconnectedEvent>(OnPortDisconnected);
            //SubscribeLocalEvent<TelepodComponent, AnchorStateChangedEvent>(OnAnchorChanged);
            //SubscribeLocalEvent<TelepodComponent, RefreshPartsEvent>(OnRefreshParts);
            //SubscribeLocalEvent<TelepodComponent, UpgradeExamineEvent>(OnUpgradeExamine);

            // UI
            SubscribeLocalEvent<TelepodComponent, AfterActivatableUIOpenEvent>(OnToggleInterface);
        }

        //section start
        //component and machine management
        private void OnComponentInit(EntityUid uid, TelepodComponent scannerComponent, ComponentInit args)
        {
            base.Initialize();
            scannerComponent.BodyContainer = _containerSystem.EnsureContainer<ContainerSlot>(uid, $"scanner-bodyContainer");
            //_signalSystem.EnsureSinkPorts(uid, TelepodComponent.ScannerPort);
        }

        private void OnDestroyed(EntityUid uid, TelepodComponent scannerComponent, DestructionEventArgs args)
        {
            EjectBody(uid, scannerComponent);
        }

        //section start
        //in and out of the machine
        private void AddInsertOtherVerb(EntityUid uid, TelepodComponent component, GetVerbsEvent<InteractionVerb> args)
        {
            if (args.Using == null ||
                !args.CanAccess ||
                !args.CanInteract ||
                IsOccupied(component) ||
                !CanScannerInsert(uid, args.Using.Value, component))
                return;

            var name = "Unknown";
            if (TryComp(args.Using.Value, out MetaDataComponent? metadata))
                name = metadata.EntityName;

            InteractionVerb verb = new()
            {
                Act = () => InsertBody(uid, args.Target, component),
                Category = VerbCategory.Insert,
                Text = name
            };
            args.Verbs.Add(verb);
        }

        private void AddAlternativeVerbs(EntityUid uid, TelepodComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            // Eject verb
            if (IsOccupied(component))
            {
                AlternativeVerb verb = new()
                {
                    Act = () => EjectBody(uid, component),
                    Category = VerbCategory.Eject,
                    Text = Loc.GetString("medical-scanner-verb-noun-occupant"),
                    Priority = 1 // Promote to top to make ejecting the ALT-click action
                };
                args.Verbs.Add(verb);
            }

            // Self-insert verb
            if (!IsOccupied(component) &&
                CanScannerInsert(uid, args.User, component) &&
                _blocker.CanMove(args.User))
            {
                AlternativeVerb verb = new()
                {
                    Act = () => InsertBody(uid, args.User, component),
                    Text = Loc.GetString("medical-scanner-verb-enter")
                };
                args.Verbs.Add(verb);
            }
        }

        private void OnCanDragDropOn(EntityUid uid, TelepodComponent component, ref CanDropTargetEvent args)
        {
            args.Handled = true;
            args.CanDrop |= CanScannerInsert(uid, args.Dragged, component);
        }

        public bool CanScannerInsert(EntityUid uid, EntityUid target, TelepodComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return false;

            return HasComp<BodyComponent>(target);
        }

        private void OnRelayMovement(EntityUid uid, TelepodComponent scannerComponent, ref ContainerRelayMovementEntityEvent args)
        {
            if (!_blocker.CanInteract(args.Entity, uid))
                return;

            EjectBody(uid, scannerComponent);
        }

        private void OnDragDropOn(EntityUid uid, TelepodComponent scannerComponent, ref DragDropTargetEvent args)
        {
            InsertBody(uid, args.Dragged, scannerComponent);
        }

        public void InsertBody(EntityUid uid, EntityUid to_insert, TelepodComponent? scannerComponent)
        {
            if (!Resolve(uid, ref scannerComponent))
                return;

            if (scannerComponent.BodyContainer.ContainedEntity != null)
                return;

            if (!HasComp<BodyComponent>(to_insert))
                return;

            _containerSystem.Insert(to_insert, scannerComponent.BodyContainer);
            UpdateAppearance(uid, scannerComponent);
        }

        public void EjectBody(EntityUid uid, TelepodComponent? scannerComponent)
        {
            if (!Resolve(uid, ref scannerComponent))
                return;

            if (scannerComponent.BodyContainer.ContainedEntity is not { Valid: true } contained)
                return;

            _containerSystem.Remove(contained, scannerComponent.BodyContainer);
            _climbSystem.ForciblySetClimbing(contained, uid);
            UpdateAppearance(uid, scannerComponent);
        }

        /*private void OnPortDisconnected(EntityUid uid, TelepodComponent component, PortDisconnectedEvent args)
        {
            component.ConnectedConsole = null;
        }*/

        /*private void OnAnchorChanged(EntityUid uid, TelepodComponent component, ref AnchorStateChangedEvent args)
        {
            if (component.ConnectedConsole == null || !TryComp<CloningConsoleComponent>(component.ConnectedConsole, out var console))
                return;

            if (args.Anchored)
            {
                _cloningConsoleSystem.RecheckConnections(component.ConnectedConsole.Value, console.CloningPod, uid, console);
                return;
            }
            _cloningConsoleSystem.UpdateUserInterface(component.ConnectedConsole.Value, console);
        }*/

        private TelepodStatus GetStatus(EntityUid uid, TelepodComponent scannerComponent)
        {
            if (this.IsPowered(uid, EntityManager))
            {
                var body = scannerComponent.BodyContainer.ContainedEntity;
                if (body == null)
                    return TelepodStatus.Open;

                //if (!TryComp<MobStateComponent>(body.Value, out var state))
                //{   // Is not alive or dead or critical
                //    return TelepodStatus.Yellow;
                //}

                //return GetStatusFromDamageState(body.Value, state);
                return TelepodStatus.Green;
            }
            return TelepodStatus.Off;
        }
        //use the below function in the above
        public static bool IsOccupied(TelepodComponent scannerComponent)
        {
            return scannerComponent.BodyContainer.ContainedEntity != null;
        }

        /*private TelepodStatus GetStatusFromDamageState(EntityUid uid, MobStateComponent state)
        {
            if (_mobStateSystem.IsAlive(uid, state))
                return TelepodStatus.Green;

            if (_mobStateSystem.IsCritical(uid, state))
                return TelepodStatus.Red;

            if (_mobStateSystem.IsDead(uid, state))
                return TelepodStatus.Death;

            return TelepodStatus.Yellow;
        }*/

        private void UpdateAppearance(EntityUid uid, TelepodComponent scannerComponent)
        {
            if (TryComp<AppearanceComponent>(uid, out var appearance))
            {
                _appearance.SetData(uid, TelepodVisuals.Status, GetStatus(uid, scannerComponent), appearance);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            _updateDif += frameTime;
            if (_updateDif < UpdateRate)
                return;

            _updateDif -= UpdateRate;

            var query = EntityQueryEnumerator<TelepodComponent>();
            while (query.MoveNext(out var uid, out var scanner))
            {
                UpdateAppearance(uid, scanner);
            }
        }

        /*private void OnRefreshParts(EntityUid uid, TelepodComponent component, RefreshPartsEvent args)
        {
            var ratingFail = args.PartRatings[component.MachinePartCloningFailChance];

            component.CloningFailChanceMultiplier = MathF.Pow(component.PartRatingFailMultiplier, ratingFail - 1);
        }*/

        /*private void OnUpgradeExamine(EntityUid uid, TelepodComponent component, UpgradeExamineEvent args)
        {
            args.AddPercentageUpgrade("medical-scanner-upgrade-cloning", component.CloningFailChanceMultiplier);
        }*/

        private void OnToggleInterface(EntityUid uid, TelepodComponent component, AfterActivatableUIOpenEvent args)
        {
            UpdateUserInterface(uid, component);
        }

        private void UpdateUserInterface(EntityUid uid, TelepodComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            var isBodyInserted = component.BodyContainer.ContainedEntity != null;
            var canSend = isBodyInserted; /*&&
                          component.DestinationFaxAddress != null &&
                          component.SendTimeoutRemaining <= 0 &&
                          component.InsertingTimeRemaining <= 0;*/
            var state = new TelepodUiState(component.FaxName, component.KnownTelepods, canSend, isBodyInserted, component.DestinationFaxAddress);
            _userInterface.SetUiState(uid, TelepodUiKey.Key, state);
        }
    }
}
