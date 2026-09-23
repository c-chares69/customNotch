using CustomNotch.Core;
using CustomNotch.Core.Sources.Media;
using Windows.Foundation;
using Windows.Media.Control;

namespace CustomNotch.App.Platform;

/// <summary>La session média système (celle que la touche lecture/pause du clavier pilote) : Spotify, un onglet
/// YouTube, VLC… sans compte ni API. L'initialisation WinRT est asynchrone : l'objet existe tout de suite (pour être
/// enregistré avant l'ordonnanceur), son état arrive un peu après et déclenche Changed.</summary>
public sealed class WindowsMediaSession : IMediaSession, IDisposable
{
    private readonly object _lock = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private MediaState? _state;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs>? _onProps;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs>? _onPlayback;

    public event Action? Changed;

    public static WindowsMediaSession Create()
    {
        var s = new WindowsMediaSession();
        _ = s.InitAsync();
        return s;
    }

    private async Task InitAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (m, _) => Attach(m.GetCurrentSession());
            Attach(_manager.GetCurrentSession());
        }
        catch (Exception ex)
        {
            Log.Warning("media", $"Session média indisponible : {ex.Message}");
        }
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        lock (_lock)
        {
            if (_session is not null)
            {
                if (_onProps is not null) _session.MediaPropertiesChanged -= _onProps;
                if (_onPlayback is not null) _session.PlaybackInfoChanged -= _onPlayback;
            }
            _session = session;
            if (session is not null)
            {
                _onProps = (_, _) => _ = RefreshAsync();
                _onPlayback = (_, _) => _ = RefreshAsync();
                session.MediaPropertiesChanged += _onProps;
                session.PlaybackInfoChanged += _onPlayback;
            }
        }
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_lock) session = _session;
        if (session is null) { Set(null); return; }
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var playing = session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var title = props?.Title ?? "";
            Set(title.Length == 0 ? null : new MediaState(title, props?.Artist ?? "", AppName(session.SourceAppUserModelId), playing));
        }
        catch (Exception ex)
        {
            Log.Warning("media", $"Lecture de la session média : {ex.Message}");
            Set(null);
        }
    }

    /// <summary>« Spotify.exe » ou « SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify » → un nom court.</summary>
    private static string AppName(string aumid)
    {
        var s = aumid.Split('!')[0];
        s = s.Split('_')[0];
        s = s.Split('.').Last(part => part.Length > 0);
        return s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s;
    }

    private void Set(MediaState? state)
    {
        lock (_lock)
        {
            if (Equals(state, _state)) return;
            _state = state;
        }
        Changed?.Invoke();
    }

    public MediaState? Current() { lock (_lock) return _state; }

    private async Task Do(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> op)
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_lock) session = _session;
        if (session is null) return;
        try { await op(session); }
        catch (Exception ex) { Log.Warning("media", $"Commande média : {ex.Message}"); }
    }

    public Task ToggleAsync() => Do(s => s.TryTogglePlayPauseAsync());
    public Task NextAsync() => Do(s => s.TrySkipNextAsync());
    public Task PreviousAsync() => Do(s => s.TrySkipPreviousAsync());

    public void Dispose()
    {
        Attach(null);
        _manager = null;
    }
}
