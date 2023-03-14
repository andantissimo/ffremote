namespace System.Net.WebSockets;

internal static class WebSocketExtensions
{
    /// <summary>
    /// default value of <see cref="WebSocketOptions.ReceiveBufferSize"/>
    /// </summary>
    private const int ReceiveBufferSize = 4096;

    public static async ValueTask<WebSocketMessage> ReceiveMessageAsync(this WebSocket socket, int length = default, CancellationToken cancellationToken = default)
    {
        #pragma warning disable CA2000
        var stream = new MemoryPoolStream(length);
        #pragma warning restore CA2000
        try
        {
            ValueWebSocketReceiveResult result;
            using var buffer = MemoryPool<byte>.Shared.Rent(ReceiveBufferSize);
            do
            {
                result = await socket.ReceiveAsync(buffer.Memory, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(buffer.Memory[..result.Count], cancellationToken).ConfigureAwait(false);
            }
            while (!result.EndOfMessage);
            if (result.MessageType == WebSocketMessageType.Close && socket.CloseStatus != WebSocketCloseStatus.NormalClosure)
                throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
            return new(result.MessageType, stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<IMemoryOwner<byte>> ReceiveBinaryAsync(this WebSocket socket, int length = default, CancellationToken cancellationToken = default)
    {
        var message = await socket.ReceiveMessageAsync(length, cancellationToken).ConfigureAwait(false);
        try
        {
            if (message.Type != WebSocketMessageType.Binary)
                throw new WebSocketException(WebSocketError.InvalidMessageType);
            return message;
        }
        catch
        {
            message.Dispose();
            throw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<string> ReceiveStringAsync(this WebSocket socket, CancellationToken cancellationToken = default)
    {
        using var message = await socket.ReceiveMessageAsync(default, cancellationToken).ConfigureAwait(false);
        if (message.Type != WebSocketMessageType.Text)
            throw new WebSocketException(WebSocketError.InvalidMessageType);
        return Encoding.UTF8.GetString(message.Memory.Span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<T?> ReceiveFromJsonAsync<T>(this WebSocket socket, CancellationToken cancellationToken = default)
    {
        using var message = await socket.ReceiveMessageAsync(default, cancellationToken).ConfigureAwait(false);
        if (message.Type != WebSocketMessageType.Text)
            throw new WebSocketException(WebSocketError.InvalidMessageType);
        return JsonSerializer.Deserialize<T>(message.Memory.Span);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask SendAsJsonAsync<T>(this WebSocket socket, T value, CancellationToken cancellationToken = default)
    {
        return socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value).AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
    }
}
