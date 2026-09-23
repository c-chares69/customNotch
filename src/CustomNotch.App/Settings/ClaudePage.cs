using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Sources.Claude;

namespace CustomNotch.App.Settings;

/// <summary>Compte, jeton, dernière lecture et sessions Claude Code — tout ce que ClaudeStatus expose, jamais le
/// jeton lui-même (§ ClaudeCredentials.AccessToken n'apparaît nulle part ici). Se rafraîchit sur
/// ClaudeSource.StatusChanged (marshalé sur le Dispatcher, comme OnStoreChanged marshalle ConfigStore.Changed) —
/// pas de minuteur propre, la source pousse déjà l'événement à chaque lecture ou renouvellement.</summary>
public sealed class ClaudePage : PageBase
{
    private readonly ClaudeSource? _claude;
    private readonly TextBlock _plan = Ui.Text("—");
    private readonly TextBlock _tier = Ui.Text("—");
    private readonly TextBlock _token = Ui.Text("—");
    private readonly TextBlock _cli = Ui.Text("—");
    private readonly TextBlock _lastRead = Ui.Text("—");
    private readonly TextBlock _lastError = Ui.Text("—");
    private readonly TextBlock _nextAttempt = Ui.Text("—");
    private readonly TextBlock _lastRenewal = Ui.Text("—");
    private readonly StackPanel _sessions = new();

    public ClaudePage(SettingsContext ctx) : base(ctx, "Claude", "Compte, jeton, lecture, sessions.")
    {
        _claude = ctx.Registry.Get("claude") as ClaudeSource;

        var home = Bricks.Text(this, "Dossier .claude (autre compte)", HomeValue(), v => Try(() => Ctx.Editor.SetSourceGlobal("claude", s =>
        {
            if (v.Length == 0) s.Remove("home"); else s["home"] = v;
        })));

        Body.Children.Add(Banner);
        Body.Children.Add(Bricks.Card(this, "Compte",
            Bricks.Row("Abonnement", _plan),
            Bricks.Row("Palier", _tier),
            Bricks.Row("Jeton", _token),
            Bricks.Row("CLI Claude", _cli),
            Bricks.Action(this, "Se connecter", () => _claude?.SignIn?.Invoke(), primary: true)));

        Body.Children.Add(Bricks.Card(this, "Dossier", home));

        Body.Children.Add(Bricks.Card(this, "Lecture",
            Bricks.Row("Dernière lecture", _lastRead),
            Bricks.Row("Dernière erreur", _lastError),
            Bricks.Row("Prochain essai", _nextAttempt),
            Bricks.Row("Dernier renouvellement", _lastRenewal),
            Bricks.Action(this, "Relire maintenant", () => _claude?.RefreshAll())));

        Body.Children.Add(Bricks.Card(this, "Sessions", _sessions));

        if (_claude is not null)
        {
            // Même patron que OnStoreChanged (PageBase) : marshalé sur le Dispatcher, désabonné par Detach().
            Action handler = () => Dispatcher.BeginInvoke(RefreshStatus);
            _claude.StatusChanged += handler;
            OnDetach(() => _claude.StatusChanged -= handler);
        }
        RefreshStatus();
    }

    public override void Refresh() => RefreshStatus();

    private void RefreshStatus()
    {
        var status = _claude?.Status;
        _plan.Text = status?.Credentials?.SubscriptionType ?? "—";
        _tier.Text = status?.Credentials?.RateLimitTier ?? "—";
        _token.Text = TokenText(status?.Credentials);
        _cli.Text = status?.CliPath is { Length: > 0 } cli ? cli : "introuvable — winget install Anthropic.ClaudeCode";
        _lastRead.Text = status?.LastSnapshot?.ReadAtMs is { } readAt ? Clock(readAt) : "jamais";
        _lastError.Text = status?.LastError ?? "aucune";
        _nextAttempt.Text = status?.NextAttemptMs is { } next ? Clock(next) : "—";
        _lastRenewal.Text = status?.LastRenewalMs is { } renewed ? Clock(renewed) : "—";

        _sessions.Children.Clear();
        var sessions = status?.Sessions ?? Array.Empty<ClaudeSession>();
        if (sessions.Count == 0)
        {
            _sessions.Children.Add(Bricks.Hint(this, "Aucune session Claude Code en cours."));
            return;
        }
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var s in sessions)
            _sessions.Children.Add(Ui.Text($"{s.Name} — {StateLabel(s.State)} — {Ui.Relative(nowMs - s.SinceMs)}", 13));
    }

    private string HomeValue() =>
        Ctx.Store.Current.Sources?["claude"] is JsonObject o && o["home"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    private static string TokenText(ClaudeCredentials? creds) => creds is null ? "aucun jeton"
        : creds.IsExpired(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) ? "expiré" : $"valide jusqu'à {Clock(creds.ExpiresAtMs)}";

    private static string StateLabel(SessionState state) => state switch
    {
        SessionState.Busy => "occupée",
        SessionState.Waiting => "en attente",
        SessionState.Idle => "inactive",
        SessionState.Ended => "terminée",
        _ => "",
    };

    private static string Clock(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("HH:mm");
}
