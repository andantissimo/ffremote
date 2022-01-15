namespace Apache;

internal class HtpasswdConfigurationProvider : FileConfigurationProvider
{
    public HtpasswdConfigurationProvider(HtpasswdConfigurationSource source) : base(source) { }

    public override void Load(Stream stream) => Data = Read(stream);

    public static IDictionary<string, string> Read(Stream stream)
    {
        var data = new Dictionary<string, string>();
        using var reader = new StreamReader(stream);
        while (reader.Peek() != -1)
        {
            var line = reader.ReadLine();
            if (line is not { Length: > 0 })
                continue;
            var (user, hash) = line.Split(':', 2);
            if (user is not { Length: > 0 } || hash is not { Length: > 0 })
                throw new FormatException($"Unrecognized line format: {line}");
            var key = ConfigurationPath.Combine(nameof(Htpasswd), user);
            if (data.ContainsKey(key))
                throw new FormatException($"Key is duplicated: {key}");
            data[key] = hash;
        }
        return data;
    }
}
