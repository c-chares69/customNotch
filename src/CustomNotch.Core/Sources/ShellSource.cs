using System.Diagnostics;
using System.Text.Json.Nodes;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>L'échappatoire : une commande, sa sortie lue comme nombre, JSON ou texte. Fenêtre cachée, délai borné.</summary>
public sealed class ShellSource : SourceBase
{
    public override string Type => "shell";
    public override SourceSchema Schema => new(Type, "Commande", new SchemaField[]
    {
        new("command", "string", "Commande", Required: true, Help: "Exécutée par cmd.exe /c"),
        new("parse", "choice", "Lire la sortie comme", Default: "number", Choices: new[] { "number", "json", "text" }),
        new("path", "string", "Chemin JSON", Help: "Si parse = json"),
        new("max", "number", "Maximum"),
        new("unit", "string", "Unité"),
        new("timeoutSeconds", "number", "Délai (s)", Default: "5"),
    }, "terminal");
    public override TimeSpan DefaultRefresh => TimeSpan.FromMinutes(1);

    public override async Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var command = ctx.Str("command") ?? throw new InvalidOperationException("command manquante");
        var timeout = TimeSpan.FromSeconds(ctx.Num("timeoutSeconds") ?? 5);
        // Arguments (ligne brute), pas ArgumentList : cmd.exe ne suit pas l'échappement CRT standard et double
        // les guillemets internes (un chemin entre guillemets, un JSON échoué en "\"…\"").
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = global::System.Text.Encoding.UTF8,
            Arguments = $"/c {command}",
        };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Impossible de lancer la commande.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        // On lit stdout ET stderr en continu : sinon un tube plein (stderr bavard) bloque l'enfant jusqu'au délai.
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var error = process.StandardError.ReadToEndAsync(deadline.Token);
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new InvalidOperationException($"délai de {timeout.TotalSeconds} s dépassé");
        }
        var stdout = (await output.ConfigureAwait(false)).Trim();
        var stderr = (await error.ConfigureAwait(false)).Trim();
        if (process.ExitCode != 0)
        {
            var detail = stderr.Length > 200 ? stderr[..200] : stderr;
            throw new InvalidOperationException($"code de sortie {process.ExitCode}" + (detail.Length > 0 ? $" : {detail}" : ""));
        }
        var unit = ctx.Str("unit");
        switch ((ctx.Str("parse") ?? "number").ToLowerInvariant())
        {
            case "json":
                var node = JsonPath.Select(JsonNode.Parse(stdout), ctx.Str("path") ?? "");
                var number = node is JsonValue jv && jv.TryGetValue<double>(out var d) ? d : (double?)null;
                return new Reading(Value: number, Max: ctx.Num("max"), Unit: unit, Text: number is null ? node?.ToString() : null);
            case "text":
                return new Reading(Text: stdout, Detail: new[] { new DetailRow("Sortie", stdout) });
            default:
                var digits = new string(stdout.TakeWhile(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray()).Replace(',', '.');
                if (!double.TryParse(digits, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var value))
                    throw new InvalidOperationException($"« {stdout} » n'est pas un nombre");
                return new Reading(Value: value, Max: ctx.Num("max"), Unit: unit);
        }
    }
}
