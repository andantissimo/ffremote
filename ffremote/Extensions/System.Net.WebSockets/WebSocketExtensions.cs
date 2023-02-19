namespace System.Net.WebSockets;

internal static class WebSocketExtensions
{
    /// <summary>
    /// default value of <see cref="WebSocketOptions.ReceiveBufferSize"/>
    /// </summary>
    private const int ReceiveBufferSize = 4096;

    public static async ValueTask<WebSocketMessageType> ReceiveMessageAsync(this WebSocket socket, Stream stream, CancellationToken cancellationToken = default)
    {
        using var buffer = MemoryPool<byte>.Shared.Rent(ReceiveBufferSize);
        ValueWebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer.Memory, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(buffer.Memory[..result.Count], cancellationToken).ConfigureAwait(false);
        }
        while (!result.EndOfMessage);
        if (result.MessageType == WebSocketMessageType.Close && socket.CloseStatus != WebSocketCloseStatus.NormalClosure)
            throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
        return result.MessageType;
    }

    public static async ValueTask<IMemoryOwner<byte>> ReceiveBinaryAsync(this WebSocket socket, int length = default, CancellationToken cancellationToken = default)
    {
        var buffer = new MemoryPoolStream(length);
        try
        {
            var type = await socket.ReceiveMessageAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (type != WebSocketMessageType.Binary)
                throw new WebSocketException(WebSocketError.InvalidMessageType);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    public static async ValueTask<string> ReceiveStringAsync(this WebSocket socket, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryPoolStream();
        var type = await socket.ReceiveMessageAsync(stream, cancellationToken).ConfigureAwait(false);
        if (type != WebSocketMessageType.Text)
            throw new WebSocketException(WebSocketError.InvalidMessageType);
        return Encoding.UTF8.GetString(stream.Memory.Span);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Make sure all of the required types are preserved.")]
    public static async ValueTask<T?> ReceiveFromJsonAsync<T>(this WebSocket socket, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryPoolStream();
        var type = await socket.ReceiveMessageAsync(stream, cancellationToken).ConfigureAwait(false);
        if (type != WebSocketMessageType.Text)
            throw new WebSocketException(WebSocketError.InvalidMessageType);
        return JsonSerializer.Deserialize<T>(stream.Memory.Span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask SendAsync(this WebSocket socket, ReadOnlyMemory<byte> binary, CancellationToken cancellationToken = default)
    {
        return socket.SendAsync(binary, WebSocketMessageType.Binary, true, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask SendAsync(this WebSocket socket, string text, CancellationToken cancellationToken = default)
    {
        return socket.SendAsync(Encoding.UTF8.GetBytes(text).AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
    }

    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed. Make sure all of the required types are preserved.")]
    public static ValueTask SendAsJsonAsync<T>(this WebSocket socket, T value, CancellationToken cancellationToken = default)
    {
        return socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value).AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
    }
}
