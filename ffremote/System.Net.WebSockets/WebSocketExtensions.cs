namespace System.Net.WebSockets;

internal static class WebSocketExtensions
{
    /// <summary>
    /// default value of <see cref="WebSocketOptions.ReceiveBufferSize"/>
    /// </summary>
    private const int ReceiveBufferSize = 4096;

    public static async ValueTask<WebSocketMessageType> ReceiveMessageAsync(this WebSocket socket, Stream stream, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[ReceiveBufferSize];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        if (result.MessageType == WebSocketMessageType.Close && result.CloseStatus != WebSocketCloseStatus.NormalClosure)
            throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
        return result.MessageType;
    }

    public static async ValueTask<byte[]> ReceiveBytesAsync(this WebSocket socket, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        var type = await socket.ReceiveMessageAsync(stream, cancellationToken).ConfigureAwait(false);
        if (type != WebSocketMessageType.Binary)
            throw new WebSocketException(WebSocketError.InvalidMessageType);
        return stream.ToArray();
    }

    public static async ValueTask<string> ReceiveStringAsync(this WebSocket socket, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        var type = await socket.ReceiveMessageAsync(stream, cancellationToken).ConfigureAwait(false);
        if (type != WebSocketMessageType.Text)
            throw new WebSocketException(WebSocketError.InvalidMessageType);
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Make sure all of the required types are preserved.")]
    public static async ValueTask<T?> ReceiveFromJsonAsync<T>(this WebSocket socket, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        var type = await socket.ReceiveMessageAsync(stream, cancellationToken).ConfigureAwait(false);
        if (type != WebSocketMessageType.Text)
            throw new WebSocketException(WebSocketError.InvalidMessageType);
        return JsonSerializer.Deserialize<T>(stream.GetBuffer().AsSpan(0, (int)stream.Length));
    }

    public static ValueTask SendAsync(this WebSocket socket, byte[] binary, CancellationToken cancellationToken = default)
    {
        return socket.SendAsync(binary.AsMemory(), WebSocketMessageType.Binary, true, cancellationToken);
    }

    public static ValueTask SendAsync(this WebSocket socket, byte[] binary, int start, int length, CancellationToken cancellationToken = default)
    {
        return socket.SendAsync(binary.AsMemory(start, length), WebSocketMessageType.Binary, true, cancellationToken);
    }

    public static ValueTask SendAsync(this WebSocket socket, string text, CancellationToken cancellationToken = default)
    {
        return socket.SendAsync(Encoding.UTF8.GetBytes(text).AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Make sure all of the required types are preserved.")]
    public static ValueTask SendAsJsonAsync<T>(this WebSocket socket, T value, CancellationToken cancellationToken = default)
    {
        return socket.SendAsync(JsonSerializer.Serialize(value), cancellationToken);
    }
}
