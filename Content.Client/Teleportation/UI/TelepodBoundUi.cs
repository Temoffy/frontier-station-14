using Content.Client.Teleportation.UI;
using Content.Shared.Teleportation;
using Content.Shared.Teleportation.Systems;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Client.Teleportation.UI;

[UsedImplicitly]
public sealed class TelepodBoundUi : BoundUserInterface
{
    //[Dependency] private readonly IFileDialogManager _fileDialogManager = default!;

    [ViewVariables]
    private TelepodWindow? _window;

    private bool _dialogIsOpen = false;

    public TelepodBoundUi(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<TelepodWindow>();
        _window.SendButtonPressed += OnSendButtonPressed;
        _window.RefreshButtonPressed += OnRefreshButtonPressed;
        _window.PeerSelected += OnPeerSelected;
    }

    private void OnSendButtonPressed()
    {
        SendMessage(new FaxSendMessage());
    }

    private void OnRefreshButtonPressed()
    {
        SendMessage(new FaxRefreshMessage());
    }

    private void OnPeerSelected(string address)
    {
        SendMessage(new FaxDestinationMessage(address));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not TelepodUiState cast)
            return;

        _window.UpdateState(cast);
    }
}
