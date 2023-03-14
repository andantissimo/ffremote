namespace Apache;

internal static class HtpasswdServiceCollectionExtensions
{
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026")]
    public static IServiceCollection AddHtpasswd(this IServiceCollection services, IConfiguration config)
    {
        return services.Configure<Htpasswd>(config.GetSection(nameof(Htpasswd)));
    }
}
