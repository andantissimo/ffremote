namespace Apache;

[SuppressMessage("Security", "CA5350", Justification = "use SHA1")]
[SuppressMessage("Security", "CA5351", Justification = "use MD5")]
internal class Htpasswd : Dictionary<string, string>
{
    private static readonly Regex APR1Pattern = new(@"^\$apr1\$([^$]{8})\$(.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SHA1Pattern = new(@"^\{SHA\}(.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public bool Contains(string user, string pass)
    {
        if (!TryGetValue(user, out var rest))
            return false;

        if (APR1Pattern.TryMatch(rest, out var apr1))
        {
            var (salt, hash) = (apr1.Groups[1].Value, apr1.Groups[2].Value);
            return ComputeAPR1(Encoding.UTF8.GetBytes(pass), Encoding.ASCII.GetBytes(salt)) == hash;
        }

        if (SHA1Pattern.TryMatch(rest, out var sha1))
        {
            var hash = sha1.Groups[1].Value;
            return Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(pass))) == hash;
        }

        return false;
    }

    private static string ComputeAPR1(byte[] key, byte[] salt)
    {
        // https://github.com/nginx/nginx/blob/master/src/core/ngx_crypt.c

        using var md5 = MD5.Create();
        md5.TransformBlock(key);
        md5.TransformBlock(Encoding.ASCII.GetBytes("$apr1$"));
        md5.TransformBlock(salt);

        using var ctx1 = MD5.Create();
        ctx1.TransformBlock(key);
        ctx1.TransformBlock(salt);
        ctx1.TransformBlock(key);
        ctx1.TransformFinalBlock();
        var final = ctx1.Hash!;

        for (var n = key.Length; n > 0; n -= 16)
            md5.TransformBlock(final, 0, n > 16 ? 16 : n);

        Array.Clear(final);

        for (var i = key.Length; i > 0; i >>= 1)
            md5.TransformBlock((i & 1) != 0 ? final : key, 0, 1);

        md5.TransformFinalBlock();
        final = md5.Hash!;

        for (var i = 0; i < 1000; i++)
        {
            ctx1.Initialize();
            ctx1.TransformBlock((i & 1) != 0 ? key : final);
            if ((i % 3) != 0)
                ctx1.TransformBlock(salt);
            if ((i % 7) != 0)
                ctx1.TransformBlock(key);
            ctx1.TransformBlock((i & 1) != 0 ? final : key);
            ctx1.TransformFinalBlock();
            final = ctx1.Hash!;
        }

        var encrypted = new char[22];
        To64(encrypted.AsSpan( 0), final[ 0] << 16 | final[ 6] << 8 | final[12], 4);
        To64(encrypted.AsSpan( 4), final[ 1] << 16 | final[ 7] << 8 | final[13], 4);
        To64(encrypted.AsSpan( 8), final[ 2] << 16 | final[ 8] << 8 | final[14], 4);
        To64(encrypted.AsSpan(12), final[ 3] << 16 | final[ 9] << 8 | final[15], 4);
        To64(encrypted.AsSpan(16), final[ 4] << 16 | final[10] << 8 | final[ 5], 4);
        To64(encrypted.AsSpan(20), final[11], 2);

        return new string(encrypted);

        static void To64(Span<char> p, int v, int n)
        {
            for (var i = 0; i < n; i++)
            {
                p[i] = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"[v & 0x3F];
                v >>= 6;
            }
        }
    }
}
