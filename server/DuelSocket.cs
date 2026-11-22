using System.Net.WebSockets;
using System.Threading.Channels;
using Google.Protobuf;
using Spell.Protocol.V1;

namespace Spell.Server;

internal static class DuelSocket
{
    public static async Task Run(WebSocket ws, Channel<ServerFrame> outgoing, Action<ClientFrame> receive, CancellationToken stop)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stop);
        var reading = Read();
        var writing = Write();
        try { await Task.WhenAny(reading, writing); }
        finally
        {
            lifetime.Cancel();
            try { await Task.WhenAll(reading, writing); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (WebSocketException) { /* The peer disconnected during I/O. */ }
            catch (InvalidProtocolBufferException) { /* Drop a malformed frame. */ }
        }

        async Task Read()
        {
            var buffer = new byte[4096];
            while (!lifetime.IsCancellationRequested)
            {
                int count = 0;
                ValueWebSocketReceiveResult part;
                do
                {
                    part = await ws.ReceiveAsync(buffer.AsMemory(count), lifetime.Token);
                    if (part.MessageType != WebSocketMessageType.Binary) return;
                    count += part.Count;
                    if (!part.EndOfMessage && count == buffer.Length) return;
                } while (!part.EndOfMessage);
                receive(ClientFrame.Parser.ParseFrom(buffer, 0, count));
            }
        }

        async Task Write()
        {
            await foreach (var frame in outgoing.Reader.ReadAllAsync(lifetime.Token))
                await ws.SendAsync(frame.ToByteArray().AsMemory(), WebSocketMessageType.Binary, true, lifetime.Token);
        }
    }
}
