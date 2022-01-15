using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace Apache;

internal static class HtpasswdConfigurationExtensions
{
    public static IConfigurationBuilder AddHtpasswdFile(this IConfigurationBuilder builder, string path = ".htpasswd", bool optional = true, bool reloadOnChange = true)
    {
        if (path is not { Length: > 0 })
            throw new ArgumentException("Invalid file path", nameof(path));

        return builder.AddHtpasswdFile(s =>
        {
            if (builder.GetFileProvider() is PhysicalFileProvider fileProvider)
                s.FileProvider = new PhysicalFileProvider(fileProvider.Root, ExclusionFilters.None);
            s.Path = path;
            s.Optional = optional;
            s.ReloadOnChange = reloadOnChange;
            s.ResolveFileProvider();
        });
    }

    public static IConfigurationBuilder AddHtpasswdFile(this IConfigurationBuilder builder, Action<HtpasswdConfigurationSource> configureSource) => builder.Add(configureSource);
}
