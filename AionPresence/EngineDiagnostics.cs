namespace AionPresence;

// stderr is shared by recoverable diagnostics and actual engine errors.
public sealed class EngineDiagnostics
{
    public const string CaptureGap = "Luka w danych gry — odczyt jest kontynuowany. Część aktualizacji mogła zostać pominięta.";
    private readonly object gate = new();
    private string error = "", warning = "";
    private long warningUntil;

    public void Receive(string line, long now)
    {
        line = line.Trim();
        if (line.Length == 0) return;
        lock (gate) {
            if (line.StartsWith("WARN ", StringComparison.Ordinal)) {
                warning = line.Contains("never reached the capture", StringComparison.Ordinal)
                    ? CaptureGap : PresenceFormat.Limit(line, 500);
                warningUntil = now + 15_000;
            } else { error = PresenceFormat.Limit(line, 500); }
        }
    }

    public string Message(long now)
    {
        lock (gate) return error.Length > 0 ? error : now < warningUntil ? warning : "";
    }

    public void Reset()
    {
        lock (gate) { error = warning = ""; warningUntil = 0; }
    }
}
