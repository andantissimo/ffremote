namespace System.Net.WebSockets;

internal readonly struct WebSocketMessage : IMemoryOwner<byte>
{
    private readonly IMemoryOwner<byte> _buffer;

    public WebSocketMessageType Type { get; }

    public Memory<byte> Memory => _buffer.Memory;

    internal WebSocketMessage(WebSocketMessageType type, IMemoryOwner<byte> buffer) => (Type, _buffer) = (type, buffer);

    public void Dispose()
    {
        _buffer.Dispose();
        GC.SuppressFinalize(this);
    }
}
