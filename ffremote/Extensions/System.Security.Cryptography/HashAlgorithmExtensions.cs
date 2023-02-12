namespace System.Security.Cryptography;

internal static class HashAlgorithmExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TransformBlock(this HashAlgorithm hash, byte[] buffer, int offset, int count) => hash.TransformBlock(buffer, offset, count, null, 0);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TransformBlock(this HashAlgorithm hash, byte[] buffer) => hash.TransformBlock(buffer, 0, buffer.Length, null, 0);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] TransformFinalBlock(this HashAlgorithm hash, byte[] buffer) => hash.TransformFinalBlock(buffer, 0, buffer.Length);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] TransformFinalBlock(this HashAlgorithm hash) => hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
}
