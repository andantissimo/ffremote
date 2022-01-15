namespace Apache;

internal class HtpasswdConfigurationSource : FileConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder) => new HtpasswdConfigurationProvider(this);
}
