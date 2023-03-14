internal static partial class FFmpeg
{
    /// <summary>
    /// options with OPT_BOOL flag
    /// </summary>
    public static readonly IReadOnlySet<string> UnaryOptions = new HashSet<string>(new[]
    {
        "-report",
        "-hide_banner",
        "-y",
        "-n",
        "-ignore_unknown",
        "-copy_unknown",
        "-accurate_seek",
        "-benchmark",
        "-benchmark_all",
        "-stdin",
        "-nostdin",
        "-dump",
        "-hex",
        "-re",
        "-copyts",
        "-start_at_zero",
        "-shortest",
        "-bitexact",
        "-xerror",
        "-copyinkf",
        "-stats",
        "-nostats",
        "-debug_ts",
        "-find_stream_info",
        "-intra",
        "-vn",
        "-deinterlace",
        "-psnr",
        "-vstats",
        "-qphist",
        "-force_fps",
        "-autorotate",
        "-noautorotate",
        "-an",
        "-sn",
        "-fix_sub_duration",
        "-isync",
        "-dn",
    });
}
