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

    public ClaudePage(SettingsContext ctx) : base(ctx, "Claude", "Connexion, jeton, usage et sessions Claude Code en cours.")
    {
        _claude = ctx.Registry.Get("claude") as ClaudeSource;

        var home = Debounced(HomeValue(), v => Try(() => Ctx.Editor.SetSourceGlobal("claude", s =>
        {
            if (string.IsNullOrWhiteSpace(v)) s.Remove("home"); else s["home"] = v;
        })));

        Body.Children.Add(Banner);
        Body.Children.Add(Section("Compte"));
        Body.Children.Add(Card(Stack(
            Row("Abonnement", _plan),
            Row("Palier", _tier),
            Row("Jeton", _token),
            Row("CLI Claude", _cli),
            Row("Dossier .claude (autre compte)", home))));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        buttons.Children.Add(Btn("Se connecter", () => _claude?.SignIn?.Invoke()));
        buttons.Children.Add(Btn("Relire maintenant", () => _claude?.RefreshAll()));
        Body.Children.Add(buttons);

        Body.Children.Add(Section("Lecture"));
        Body.Children.Add(Card(Stack(
            Row("Dernière lecture", _lastRead),
            Row("Dernière erreur", _lastError),
            Row("Prochain essai", _nextAttempt),
            Row("Dernier renouvellement", _lastRenewal))));

        Body.Children.Add(Section("Sessions"));
        Body.Children.Add(Card(_sessions));

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
            _sessions.Children.Add(Ui.Text("Aucune session Claude Code en cours.", 12, null, "Muted"));
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

    private static StackPanel Stack(params UIElement[] children)
    {
        var s = new StackPanel();
        foreach (var c in children) s.Children.Add(c);
        return s;
    }
}
