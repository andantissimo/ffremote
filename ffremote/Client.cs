internal class Client : BackgroundService
{
    private static readonly Uri DefaultWorkerEndpoint = new("http://localhost:5000");

    private static readonly IReadOnlySet<string> UnsupportedOptions = new HashSet<string>(new[]
     {
        "-report",
        "-filter_script",
        "-attach",
        "-dump_attachment",
        "-pass",
        "-passlogfile",
        "-vstats",
        "-vstats_file",
        "-sdp_file",
        "-fpre",
    });

    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger _logger;

    public Client(IHostApplicationLifetime lifetime, ILogger<Client> logger)
    {
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        Environment.ExitCode = 1;
        try
        {
            var args = Environment.GetCommandLineArgs()[1..];
            var (code, stdout, stderr) = await RunAsync(args, stopping).ConfigureAwait(false);
            if (stderr is { Length: > 0 })
                Console.Error.Write(stderr);
            if (stdout is { Length: > 0 })
                Console.Out.Write(stdout);
            Environment.ExitCode = code;
        }
        catch (Exception ex) when (ex is HttpRequestException or WebSocketException)
        {
            if (ex.InnerException is HttpRequestException inner)
                ex = inner;
            Console.Error.WriteLine(ex.Message);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            _logger.LogDebug("Application stopping");
            Environment.ExitCode = 130; // SIGINT
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private static async Task<(int ExitCode, string? Output, string? Error)> RunAsync(string[] args, CancellationToken stopping)
    {
        bool? overwrite = null;
        var endpoint = DefaultWorkerEndpoint;
        var inputs = new List<(string Path, Guid Id)>();
        var outputs = new List<(string? Path, Guid Id)>();
        var options = new List<string>();
        var nostdin = false;
        TimeSpan? timeout = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-")
                return (1, null, "Standard I/O is not supported.");
            if (args[i].StartsWith('-'))
            {
                var option = args[i];
                var nospec = option.Split(':', 2)[0];
                if (UnsupportedOptions.Contains(nospec))
                    return (1, null, $"Unsupported option '{nospec.TrimStart('-')}'.");
                switch (option)
                {
                    case "-h" or "-?" or "-help" or "--help":
                        options.Add(option);
                        if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                            options.Add(args[++i]);
                        continue;
                    case "-y":
                        overwrite = true;
                        continue;
                    case "-n":
                        overwrite = false;
                        continue;
                    case "-nostdin":
                        nostdin = true;
                        break;
                }
                if (FFmpeg.PrintOptions.Contains(option) || FFmpeg.UnaryOptions.Contains(option))
                {
                    options.Add(option);
                    continue;
                }
                if (++i >= args.Length)
                    return (1, null, $"Missing argument for option '{option.TrimStart('-')}'.");
                switch (option)
                {
                    case "-w":
                        if (!Uri.TryCreate($"{args[i].TrimEnd('/')}/", UriKind.Absolute, out endpoint))
                            return (1, null, $"'{args[i]}' is not a valid URI.");
                        break;
                    case "-v" or "-loglevel":
                        options.Add(option, args[i]);
                        break;
                    case "-i":
                        if (args[i] == "-")
                            return (1, null, "Standard I/O is not supported.");
                        inputs.Add((args[i], Guid.NewGuid()));
                        options.Add(option, $"{inputs[^1].Id}{Path.GetExtension(args[i])}");
                        break;
                    case "-rw_timeout":
                        if (double.TryParse(args[i], out var usec))
                            timeout = TimeSpan.FromMilliseconds(usec / 1000);
                        options.Add(option, args[i]);
                        break;
                    default:
                        options.Add(option, args[i]);
                        break;
                }
            }
            else if (IsNullDevice(args[i]))
            {
                outputs.Add((null, Guid.Empty));
                options.Add("-y", "/dev/null");
            }
            else
            {
                outputs.Add((args[i], Guid.NewGuid()));
                options.Add($"{outputs[^1].Id}{Path.GetExtension(args[i])}");
            }
        }

        var printing = options.Intersect(FFmpeg.PrintOptions).Any();
        if (!printing)
        {
            if (outputs.Count == 0)
                return (1, null, "At least one output file must be specified");
            if (inputs.Count == 0)
                return (1, null, "Output file #0 does not contain any stream");
            foreach (var (path, _) in inputs)
            {
                if (!File.Exists(path))
                    return (1, null, $"{path}: No such file or directory");
            }
            foreach (var (path, _) in outputs)
            {
                if (path is not null && File.Exists(path) && overwrite != true)
                {
                    if (overwrite == false)
                        return (1, null, $"File '{path}' already exists. Exiting.");
                    Console.Error.Write("File '{0}' already exists, Overwrite? [y/N] ", path);
                    if (Console.ReadLine()?.Equals("y", StringComparison.OrdinalIgnoreCase) != true)
                        return (1, null, "Not overwriting - exiting");
                }
            }
        }

        using var handler = new SocketsHttpHandler { ConnectTimeout = timeout ?? TimeSpan.FromSeconds(5), CookieContainer = new(), UseCookies = true };
        using var client = new HttpClient(handler) { Timeout = timeout ?? Timeout.InfiniteTimeSpan, BaseAddress = endpoint.ToHttpUri() };
        if (endpoint.UserInfo.Contains(':'))
            client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(endpoint.UserInfo)));

        if (printing)
        {
            var relativeUri = QueryHelpers.AddQueryString(string.Empty, "q", string.Join(' ', options));
            #pragma warning disable IL2026
            var (stdout, stderr) = await client.GetFromJsonAsync<string[]>(relativeUri, stopping).ConfigureAwait(false);
            #pragma warning restore IL2026
            return (0, stdout, stderr);
        }

        using var socket = new ClientWebSocket();
        if (client.DefaultRequestHeaders.Authorization is not null)
            socket.Options.SetRequestHeader(HeaderNames.Authorization, $"{client.DefaultRequestHeaders.Authorization}");
        await socket.ConnectAsync(client.BaseAddress.ToWebSocketUri(), stopping).ConfigureAwait(false);
        if (!SetCookieHeaderValue.TryParse(await socket.ReceiveStringAsync(stopping).ConfigureAwait(false), out var cookie))
            return (1, null, "Unexpected command received");
        handler.CookieContainer.Add(new Cookie($"{cookie.Name}", $"{cookie.Value}") { Domain = endpoint.Host });

        var inputTaskSets = new List<(Task Connected, Task Closed)>();
        foreach (var (path, id) in inputs)
        {
            var connected = new TaskCompletionSource();
            inputTaskSets.Add((connected.Task, InputAsync()));

            async Task InputAsync()
            {
                using var _ = stopping.Register(() => connected.TrySetCanceled());
                using var file = File.OpenRead(path);
                using var sock = new ClientWebSocket();
                sock.Options.Cookies = handler.CookieContainer;
                if (client.DefaultRequestHeaders.Authorization is not null)
                    sock.Options.SetRequestHeader(HeaderNames.Authorization, $"{client.DefaultRequestHeaders.Authorization}");
                await sock.ConnectAsync(new(client.BaseAddress.ToWebSocketUri(), $"{id}"), stopping).ConfigureAwait(false);
                #pragma warning disable IL2026
                await sock.SendAsJsonAsync(file.Length, stopping).ConfigureAwait(false);
                #pragma warning restore IL2026
                connected.TrySetResult();
                while (sock.State == WebSocketState.Open && !stopping.IsCancellationRequested)
                {
                    using var message = await sock.ReceiveMessageAsync(default, stopping).ConfigureAwait(false);
                    if (message.Type != WebSocketMessageType.Text)
                        break;
                    if (!RangeHeaderValue.TryParse(Encoding.UTF8.GetString(message.Memory.Span), out var range))
                        break;
                    var (from, to) = (range.Ranges.First().From!.Value, range.Ranges.First().To!.Value);
                    var length = (int)(to + 1 - from);
                    using var buffer = MemoryPool<byte>.Shared.Rent(length);
                    file.Seek(from, SeekOrigin.Begin);
                    await file.ReadAsync(buffer.Memory[..length], stopping).ConfigureAwait(false);
                    await sock.SendAsync(buffer.Memory[..length], stopping).ConfigureAwait(false);
                }
                await sock.CloseAsync(WebSocketCloseStatus.NormalClosure, null, stopping).ConfigureAwait(false);
            }
        }
        await Task.WhenAll(inputTaskSets.Select(set => set.Connected)).ConfigureAwait(false);

        #pragma warning disable IL2026
        await socket.SendAsJsonAsync(options, stopping).ConfigureAwait(false);
        #pragma warning restore IL2026
        int exitCode = 1;
        while (socket.State == WebSocketState.Open && !stopping.IsCancellationRequested)
        {
            if (!nostdin && Console.KeyAvailable)
            {
                using var content = new ByteArrayContent(Encoding.ASCII.GetBytes(new[] { Console.ReadKey().KeyChar }));
                content.Headers.ContentType = new(MediaTypeNames.Text.Plain);
                using var response = await client.PutAsync(string.Empty, content, stopping).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            using var message = await socket.ReceiveMessageAsync(default, stopping).ConfigureAwait(false);
            switch (message.Type)
            {
                case WebSocketMessageType.Close:
                    Console.Error.WriteLine();
                    _ = int.TryParse(socket.CloseStatusDescription, out exitCode);
                    break;
                case WebSocketMessageType.Text:
                    var stderr = Encoding.UTF8.GetString(message.Memory.Span);
                    if (stderr.StartsWith("frame=", StringComparison.Ordinal))
                        Console.Error.Write('\r' + stderr);
                    else
                        Console.Error.WriteLine(stderr);
                    break;
            }
        }
        await Task.WhenAll(inputTaskSets.Select(set => set.Closed)).ConfigureAwait(false);

        if (exitCode == 0)
        {
            var fileOutputs = outputs.Where(output => output.Path is not null);
            if (fileOutputs.Any())
            {
                await Task.WhenAll(fileOutputs.Select(output => OutputAsync(output.Path!, output.Id))).ConfigureAwait(false);

                async Task OutputAsync(string path, Guid id)
                {
                    using var file = File.Create(path);
                    using var body = await client.GetStreamAsync($"{id}", stopping).ConfigureAwait(false);
                    await body.CopyToAsync(file, stopping).ConfigureAwait(false);
                }
            }
        }

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, stopping).ConfigureAwait(false);

        return (exitCode, null, null);
    }

    private static bool IsNullDevice(string path) => path == "/dev/null" || path.Equals("NUL", StringComparison.OrdinalIgnoreCase);
}
