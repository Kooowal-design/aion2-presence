using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

namespace AionPresence;

public static class IpcFrame
{
    public const int MaxBytes = 1024 * 1024;
    public static async Task Write(Stream stream, int op, JsonNode value, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes(value.ToJsonString());
        if (body.Length > MaxBytes) throw new IOException("Za duża ramka IPC.");
        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, op);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), body.Length);
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(body, ct);
        await stream.FlushAsync(ct);
    }
    public static async Task<(int Op, JsonObject Body)> Read(Stream stream, CancellationToken ct)
    {
        var header = new byte[8];
        await stream.ReadExactlyAsync(header, ct);
        var size = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if (size < 2 || size > MaxBytes) throw new IOException("Nieprawidłowa długość ramki IPC.");
        var bytes = new byte[size];
        await stream.ReadExactlyAsync(bytes, ct);
        var body = JsonNode.Parse(bytes) as JsonObject ?? throw new IOException("Nieprawidłowa ramka JSON.");
        return (BinaryPrimitives.ReadInt32LittleEndian(header), body);
    }
}

public sealed class DiscordPipe : IAsyncDisposable
{
    private readonly NamedPipeClientStream pipe;
    public JsonObject Ready { get; }
    private DiscordPipe(NamedPipeClientStream pipe, JsonObject ready) { this.pipe = pipe; Ready = ready; }
    public static async Task<DiscordPipe> Connect(string excluded, CancellationToken ct, string prefix = "discord-ipc-")
    {
        for (var i = 0; i < 10; i++) {
            var name = prefix + i;
            if (name == excluded) continue;
            var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous,
                System.Security.Principal.TokenImpersonationLevel.Identification);
            try {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(2500);
                await client.ConnectAsync(120, deadline.Token);
                await IpcFrame.Write(client, 0, new JsonObject { ["v"] = 1, ["client_id"] = PresenceApp.ApplicationId }, deadline.Token);
                var (op, ready) = await IpcFrame.Read(client, deadline.Token);
                if (op != 1 || ready["evt"]?.GetValue<string>() != "READY") throw new IOException("Discord odrzucił połączenie.");
                return new DiscordPipe(client, ready);
            }
            catch (Exception e) when (e is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException) {
                await client.DisposeAsync();
                ct.ThrowIfCancellationRequested();
            }
        }
        throw new IOException("Nie znaleziono działającego Discorda desktop.");
    }
    public async Task<JsonObject> Set(JsonObject command, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(4000);
        await IpcFrame.Write(pipe, 1, command, deadline.Token);
        while (true) {
            var (op, response) = await IpcFrame.Read(pipe, deadline.Token);
            if (op == 3) { await IpcFrame.Write(pipe, 4, response, deadline.Token); continue; }
            if (op == 2) throw new IOException("Discord zamknął połączenie.");
            if (response["nonce"]?.ToJsonString() != command["nonce"]?.ToJsonString()) continue;
            if (response["evt"]?.GetValue<string>() == "ERROR")
                throw new IOException("Discord: " + (response["data"]?["message"]?.GetValue<string>() ?? "odrzucono status"));
            return response;
        }
    }
    public ValueTask DisposeAsync() => pipe.DisposeAsync();
}
