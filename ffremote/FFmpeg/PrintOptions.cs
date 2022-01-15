internal static partial class FFmpeg
{
    /// <summary>
    /// options with OPT_EXIT flag
    /// </summary>
    public static readonly IReadOnlySet<string> PrintOptions = new HashSet<string>(new[]
    {
        "-L",
        "-h",
        "-?",
        "-help",
        "--help",
        "-version",
        "-buildconf",
        "-formats",
        "-muxers",
        "-demuxers",
        "-devices",
        "-codecs",
        "-decoders",
        "-encoders",
        "-bsfs",
        "-protocols",
        "-filters",
        "-pix_fmts",
        "-layouts",
        "-sample_fmts",
        "-colors",
        "-sources",
        "-sinks",
        "-hwaccels",
    });
}
