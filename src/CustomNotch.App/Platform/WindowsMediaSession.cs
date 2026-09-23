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
    private TypedEventHandler<GlobalSystemMediaTransportControlsSessionManager, CurrentSessionChangedEventArgs>? _onSession;
    /// <summary>Dispose() peut s'exécuter pendant que InitAsync() attend RequestAsync() : sans ce drapeau, la
    /// continuation se rattache un gestionnaire et une session comme si de rien n'était, et l'objet se « redispose ».</summary>
    private volatile bool _disposed;

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
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_disposed) return;
            _manager = manager;
            _onSession = (m, _) => Attach(m.GetCurrentSession());
            _manager.CurrentSessionChanged += _onSession;
            Attach(_manager.GetCurrentSession());
        }
        catch (Exception ex)
        {
            Log.Warning("media", $"Session média indisponible : {ex.Message}");
        }
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_disposed) session = null;
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
            var cover = await ReadThumbnailAsync(props?.Thumbnail);
            Set(title.Length == 0 ? null : new MediaState(title, props?.Artist ?? "", AppName(session.SourceAppUserModelId), playing, cover));
        }
        catch (Exception ex)
        {
            Log.Warning("media", $"Lecture de la session média : {ex.Message}");
            Set(null);
        }
    }

    /// <summary>La vignette (pochette) de la session, telle quelle : les applications donnent du PNG ou du JPEG de
    /// quelques dizaines de Ko. Plafonnée à 512 Ko (une image plus grande est ignorée plutôt que copiée à chaque
    /// lecture) ; toute erreur rend null — la pochette est un agrément, jamais une raison de perdre le titre.</summary>
    private static async Task<byte[]?> ReadThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return null;
        try
        {
            using var stream = await thumbnail.OpenReadAsync().AsTask();
            if (stream.Size == 0 || stream.Size > 512 * 1024) return null;
            using var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
            var size = (uint)stream.Size;
            await reader.LoadAsync(size).AsTask();
            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            Log.Warning("media", $"Vignette : {ex.Message}");
            return null;
        }
    }

    /// <summary>« Spotify.exe » ou « SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify » → un nom court. Un AUMID vide ou
    /// réduit à des séparateurs ne doit pas faire échouer la lecture de la session : on rend « média » plutôt que de
    /// lever une exception.</summary>
    private static string AppName(string aumid)
    {
        var s = aumid.Split('!')[0];
        s = s.Split('_')[0];
        var parts = s.Split('.', StringSplitOptions.RemoveEmptyEntries);
        s = parts.Length > 0 ? parts[^1] : s;
        if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s[..^4];
        return s.Length > 0 ? s : "média";
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
        _disposed = true;
        if (_manager is not null && _onSession is not null) _manager.CurrentSessionChanged -= _onSession;
        Attach(null);
        _manager = null;
    }
}
