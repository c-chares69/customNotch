using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace CustomNotch.Core.Platform;

/// <summary>
/// Une seule instance à la fois, et un canal pour lui parler : un port d'écoute sur
/// l'adresse locale, dérivé du dossier de données, que le système rend si l'application
/// meurt. Une seconde copie envoie <c>show</c> et s'arrête.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public const int PortBase = 47700;
    public const int PortSpan = 200;
    public const string StoppedMarker = "stopped_by_user";

    public event Action? ShowRequested;
    /// <summary>Toute autre commande reçue sur le canal (« open Pointages »), pour les tests et les scripts.</summary>
    public event Action<string>? CommandReceived;

    private readonly string _home;
    private TcpListener? _listener;
    private CancellationTokenSource? _stop;

    public SingleInstance(string home)
    {
        _home = home;
        Port = PortFor(home);
    }

    public int Port { get; }
    public bool Held => _listener is not null;

    public static int PortFor(string home)
    {
        var digest = SHA1.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(home).ToLowerInvariant()));
        return PortBase + ((digest[0] << 8) | digest[1]) % PortSpan;
    }

    /// <summary>Envoie une commande à l'instance en cours ; false si personne ne répond.</summary>
    public static bool Ping(string home, string command = "ping", double timeoutSeconds = 1.0)
    {
        try
        {
            using var client = new TcpClient();
            if (!client.ConnectAsync(IPAddress.Loopback, PortFor(home)).Wait(TimeSpan.FromSeconds(timeoutSeconds))) return false;
            using var stream = client.GetStream();
            stream.ReadTimeout = (int)(timeoutSeconds * 1000);
            var bytes = Encoding.UTF8.GetBytes(command + "\n");
            stream.Write(bytes, 0, bytes.Length);
            var buffer = new byte[64];
            var read = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, read).Trim() == "ok";
        }
        catch (Exception ex) when (ex is SocketException or IOException or AggregateException or InvalidOperationException)
        {
            return false;
        }
    }

    public static bool IsRunning(string home) => Ping(home);

    public static void MarkStoppedByUser(string home)
    {
        try
        {
            Directory.CreateDirectory(home);
            File.WriteAllText(Path.Combine(home, StoppedMarker), "");
        }
        catch (IOException) { }
    }

    public static void ClearStoppedByUser(string home)
    {
        try { File.Delete(Path.Combine(home, StoppedMarker)); } catch (IOException) { }
    }

    public static bool StoppedByUser(string home) => File.Exists(Path.Combine(home, StoppedMarker));

    /// <summary>Retourne false si une autre instance tient déjà le port.</summary>
    public bool Acquire()
    {
        var listener = new TcpListener(IPAddress.Loopback, Port);
        try
        {
            listener.ExclusiveAddressUse = true;
            listener.Start(4);
        }
        catch (SocketException)
        {
            return false;
        }
        _listener = listener;
        _stop = new CancellationTokenSource();
        _ = Task.Run(() => ServeAsync(listener, _stop.Token));
        return true;
    }

    private async Task ServeAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { return; }
            using (client)
            {
                try
                {
                    using var stream = client.GetStream();
                    // ReadTimeout n'agit pas sur ReadAsync : une connexion muette bloquerait le
                    // canal pour toujours. Une seconde, puis on passe au suivant.
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    deadline.CancelAfter(TimeSpan.FromSeconds(1));
                    var buffer = new byte[256];
                    var read = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false);
                    var line = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                    if (line == "show") ShowRequested?.Invoke();
                    else if (line.Length > 0 && line != "ping") CommandReceived?.Invoke(line);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("ok\n"), ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException) { }
            }
        }
    }

    public void Release()
    {
        _stop?.Cancel();
        _listener?.Stop();
        _listener = null;
    }

    public void Dispose() => Release();
}
