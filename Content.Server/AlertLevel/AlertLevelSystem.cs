using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking; // Frontier
using Robust.Shared.Player; // Frontier
using Content.Server._NF.SectorServices; // Frontier

namespace Content.Server.AlertLevel;

public sealed class AlertLevelSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly GameTicker _ticker = default!; // Frontier
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;

    // Until stations are a prototype, this is how it's going to have to be.
    public const string DefaultAlertLevelSet = "stationAlerts";

    public override void Initialize()
    {
        //SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialize); // Frontier: sector-wide services
        SubscribeLocalEvent<AlertLevelComponent, ComponentInit>(OnInit); // Frontier: sector-wide services
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);
    }

    public override void Update(float time)
    {
        var query = EntityQueryEnumerator<AlertLevelComponent>();

        while (query.MoveNext(out var station, out var alert))
        {
            if (alert.CurrentDelay <= 0)
            {
                if (alert.ActiveDelay)
                {
                    RaiseLocalEvent(new AlertLevelDelayFinishedEvent());
                    alert.ActiveDelay = false;
                }
                continue;
            }

            alert.CurrentDelay -= time;
        }
    }

    // Frontier: sector-wide services
    /*
    private void OnStationInitialize(StationInitializedEvent args)
    {
        if (!TryComp<AlertLevelComponent>(args.Station, out var alertLevelComponent))
            return;

        if (!_prototypeManager.TryIndex(alertLevelComponent.AlertLevelPrototype, out AlertLevelPrototype? alerts))
        {
            return;
        }

        alertLevelComponent.AlertLevels = alerts;

        var defaultLevel = alertLevelComponent.AlertLevels.DefaultLevel;
        if (string.IsNullOrEmpty(defaultLevel))
        {
            defaultLevel = alertLevelComponent.AlertLevels.Levels.Keys.First();
        }

        SetLevel(args.Station, defaultLevel, false, false, true);
    }
    */

    private void OnInit(EntityUid uid, AlertLevelComponent comp, ComponentInit args)
    {
        if (!_prototypeManager.TryIndex(comp.AlertLevelPrototype, out AlertLevelPrototype? alerts))
        {
            return;
        }

        comp.AlertLevels = alerts;

        var defaultLevel = comp.AlertLevels.DefaultLevel;
        if (string.IsNullOrEmpty(defaultLevel))
        {
            defaultLevel = comp.AlertLevels.Levels.Keys.First();
        }

        SetLevel(uid, defaultLevel, false, false, true);
    }
    // End Frontier

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        if (!args.ByType.TryGetValue(typeof(AlertLevelPrototype), out var alertPrototypes)
            || !alertPrototypes.Modified.TryGetValue(DefaultAlertLevelSet, out var alertObject)
            || alertObject is not AlertLevelPrototype alerts)
        {
            return;
        }

        var query = EntityQueryEnumerator<AlertLevelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.AlertLevels = alerts;

            if (!comp.AlertLevels.Levels.ContainsKey(comp.CurrentLevel))
            {
                var defaultLevel = comp.AlertLevels.DefaultLevel;
                if (string.IsNullOrEmpty(defaultLevel))
                {
                    defaultLevel = comp.AlertLevels.Levels.Keys.First();
                }

                SetLevel(uid, defaultLevel, true, true, true);
            }
        }

        RaiseLocalEvent(new AlertLevelPrototypeReloadedEvent());
    }

    public string GetLevel(EntityUid station, AlertLevelComponent? alert = null)
    {
        // Frontier: sector-wide alarms
        if (!TryComp(_sectorService.GetServiceEntity(), out alert))
            return string.Empty;

        // if (!Resolve(station, ref alert))
        // {
        //     return string.Empty;
        // }
        // End Frontier

        return alert.CurrentLevel;
    }

    public float GetAlertLevelDelay(EntityUid station, AlertLevelComponent? alert = null)
    {
        // Frontier: sector-wide alarms
        if (!TryComp(_sectorService.GetServiceEntity(), out alert))
            return float.NaN;

        // if (!Resolve(station, ref alert))
        // {
        //     return float.NaN;
        // }
        // End Frontier

        return alert.CurrentDelay;
    }

    /// <summary>
    /// Set the alert level based on the station's entity ID.
    /// </summary>
    /// <param name="station">Station entity UID.</param>
    /// <param name="level">Level to change the station's alert level to.</param>
    /// <param name="playSound">Play the alert level's sound.</param>
    /// <param name="announce">Say the alert level's announcement.</param>
    /// <param name="force">Force the alert change. This applies if the alert level is not selectable or not.</param>
    /// <param name="locked">Will it be possible to change level by crew.</param>
    public void SetLevel(EntityUid station, string level, bool playSound, bool announce, bool force = false,
        bool locked = false, MetaDataComponent? dataComponent = null, AlertLevelComponent? component = null)
    {
        // Frontier: sector-wide alerts
        EntityUid sectorEnt = _sectorService.GetServiceEntity();
        if (!TryComp<AlertLevelComponent>(sectorEnt, out component))
            return;
        // End Frontier

        if (!Resolve(station, ref dataComponent) // Frontier: remove component
            || component.AlertLevels == null
            || !component.AlertLevels.Levels.TryGetValue(level, out var detail)
            || component.CurrentLevel == level)
        {
            return;
        }

        if (!force)
        {
            if (!detail.Selectable
                || component.CurrentDelay > 0
                || component.IsLevelLocked)
            {
                return;
            }

            component.CurrentDelay = _cfg.GetCVar(CCVars.GameAlertLevelChangeDelay);
            component.ActiveDelay = true;
        }

        component.CurrentLevel = level;
        component.IsLevelLocked = locked;

        var stationName = dataComponent.EntityName;

        var name = level.ToLower();

        if (Loc.TryGetString($"alert-level-{level}", out var locName))
        {
            name = locName.ToLower();
        }

        // Announcement text. Is passed into announcementFull.
        var announcement = detail.Announcement;

        if (Loc.TryGetString(detail.Announcement, out var locAnnouncement))
        {
            announcement = locAnnouncement;
        }

        // The full announcement to be spat out into chat.
        var announcementFull = Loc.GetString("alert-level-announcement", ("name", name), ("announcement", announcement));

        var playDefault = false;
        if (playSound)
        {
            if (detail.Sound != null)
            {
                //var filter = _stationSystem.GetInOwningStation(station); // Frontier: global alerts
                var filter = Filter.Empty(); // Frontier
                filter.AddInMap(_ticker.DefaultMap, EntityManager); // Frontier
                _audio.PlayGlobal(detail.Sound, filter, true, detail.Sound.Params);
            }
            else
            {
                playDefault = true;
            }
        }

        if (announce)
        {
            _chatSystem.DispatchStationAnnouncement(station, announcementFull, playDefaultSound: playDefault,
                colorOverride: detail.Color, sender: stationName);
        }

        RaiseLocalEvent(new AlertLevelChangedEvent(EntityUid.Invalid, level)); // Frontier: pass invalid, we have no station
    }
}

public sealed class AlertLevelDelayFinishedEvent : EntityEventArgs
{}

public sealed class AlertLevelPrototypeReloadedEvent : EntityEventArgs
{}

public sealed class AlertLevelChangedEvent : EntityEventArgs
{
    public EntityUid Station { get; }
    public string AlertLevel { get; }

    public AlertLevelChangedEvent(EntityUid station, string alertLevel)
    {
        Station = station;
        AlertLevel = alertLevel;
    }
}
