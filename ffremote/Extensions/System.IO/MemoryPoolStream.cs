namespace System.IO;

internal sealed class MemoryPoolStream : Stream, IMemoryOwner<byte>
{
    /// <summary>
    /// <see href="https://github.com/dotnet/runtime/blob/v6.0.0/src/libraries/System.Memory/src/System/Buffers/ArrayMemoryPool.cs#L17"/>
    /// </summary>
    private const int MinBufferSize = 4096;

    private IMemoryOwner<byte> _buffer;
    private int _length;

    public Memory<byte> Memory => _buffer.Memory[0.._length];

    public MemoryPoolStream(int capacity = default)
    {
        _buffer = MemoryPool<byte>.Shared.Rent(Math.Max(capacity, MinBufferSize));
    }

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override long Length => _length;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override bool CanRead => false;

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(byte[] buffer, int offset, int count)
    {
        Debug.Assert(buffer is not null);
        Debug.Assert(offset >= 0);
        Debug.Assert(count >= 0);
        Write(buffer.AsSpan(offset, count));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Reserve(_length + buffer.Length);
        buffer.CopyTo(_buffer.Memory.Span.Slice(_length, buffer.Length));
        _length += buffer.Length;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        _buffer?.Dispose();
        base.Dispose(disposing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reserve(int capacity)
    {
        if (capacity <= _buffer.Memory.Length)
            return;
        capacity = Math.Max(capacity, (int)Math.Min(2u * _buffer.Memory.Length, MemoryPool<byte>.Shared.MaxBufferSize));
        var buffer = MemoryPool<byte>.Shared.Rent(capacity);
        _buffer.Memory.CopyTo(buffer.Memory);
        _buffer.Dispose();
        _buffer = buffer;
    }
}
