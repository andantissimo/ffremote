using static Microsoft.AspNetCore.Http.StatusCodes;

internal class Worker
{
    /// <summary>
    /// <see cref="Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.Constants.DefaultServerAddress"/>
    /// </summary>
    private const string DefaultServerAddress = "http://localhost:5000";

    /// <summary>
    /// <see cref="Microsoft.AspNetCore.Mvc.Infrastructure.FileResultExecutorBase.BufferSize" />
    /// </summary>
    private const int BufferSize = 64 * 1024;

    private static readonly string NullDevice = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL" : "/dev/null";

    private static readonly Encoding SystemEncoding = Encoding.GetEncoding(0);

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
    private readonly Uri _address;
    private readonly IOptionsMonitor<Apache.Htpasswd> _htpasswd;

    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();

    private class Session
    {
        internal const string Name = "sid";

        internal ConcurrentDictionary<Guid, (WebSocket Socket, long Length, SemaphoreSlim Semaphore)> Inputs { get; } = new();
        internal ConcurrentDictionary<Guid, (FileInfo File, TaskCompletionSource Sent)> Outputs { get; } = new();
        internal StreamWriter? StandardInput;
        internal TaskCompletionSource<int> Exited { get; } = new();
        internal CancellationToken Aborted { get; init; }
    }

    public Worker(RequestDelegate _, IHostApplicationLifetime lifetime, ILogger<Worker> logger, IServer server, IOptionsMonitor<Apache.Htpasswd> htpasswd)
    {
        _lifetime = lifetime;
        _logger = logger;
        _address = new(server.Features.Get<IServerAddressesFeature>()?.Addresses?.FirstOrDefault() ?? DefaultServerAddress);
        _htpasswd = htpasswd;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var (request, response) = (context.Request, context.Response);

        var aborted = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, _lifetime.ApplicationStopping).Token;
        try
        {
            var htpasswd = _htpasswd.CurrentValue;
            if (htpasswd.Count != 0)
            {
                if (!request.TryGetBasicAuthenticationCredentials(out var user, out var pass))
                {
                    response.StatusCode = Status401Unauthorized;
                    response.Headers.WWWAuthenticate = "Basic";
                    return;
                }
                if (!htpasswd.Contains(user, pass))
                {
                    response.StatusCode = Status403Forbidden;
                    return;
                }
            }

            if (context.WebSockets.IsWebSocketRequest)
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                await HandleWebSocketAsync(request, socket, aborted).ConfigureAwait(false);
            }
            else if (HttpMethods.IsGet(request.Method))
            {
                if (await HandleGetAsync(request, response, aborted).ConfigureAwait(false) is int statusCode)
                    response.StatusCode = statusCode;
            }
            else if (HttpMethods.IsPut(request.Method))
            {
                if (await HandlePutAsync(request, response, aborted).ConfigureAwait(false) is int statusCode)
                    response.StatusCode = statusCode;
            }
            else
            {
                response.StatusCode = Status405MethodNotAllowed;
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode is WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug("Connection closed prematurely");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request aborted");
        }
        catch (OperationCanceledException) when (_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            _logger.LogDebug("Application stopping");
            response.End(Status503ServiceUnavailable);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException)
        {
            _logger.LogWarning("Bad request: {Message}", ex.Message);
            response.End(Status400BadRequest);
        }
        catch (Exception ex) when (ex is NotImplementedException or NotSupportedException)
        {
            _logger.LogDebug(ex, "Not implemented");
            response.End(Status501NotImplemented);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            response.End(Status500InternalServerError);
        }
    }

    private async ValueTask HandleWebSocketAsync(HttpRequest request, WebSocket socket, CancellationToken aborted)
    {
        if (request.Cookies.TryGetValue(Session.Name, out var cookie) && Guid.TryParse(cookie, out var sid) && _sessions.TryGetValue(sid, out var session))
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(request.Path), out var id))
                throw new ArgumentException("Input ID is missing or not valid");

            aborted = CancellationTokenSource.CreateLinkedTokenSource(aborted, session.Aborted).Token;

            #pragma warning disable IL2026
            var length = await socket.ReceiveFromJsonAsync<long>(aborted).ConfigureAwait(false);
            #pragma warning restore IL2026
            if (!session.Inputs.TryAdd(id, (socket, length, new(1, 1))))
                throw new ArgumentException($"Input ID conflicted: {id}");
            _logger.LogDebug("Input connected: {SessionId}/{Id}", sid, id);
            try
            {
                await Task.WhenAny(session.Exited.Task, Task.Delay(Timeout.Infinite, aborted)).ConfigureAwait(false);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, aborted).ConfigureAwait(false);
                _logger.LogDebug("Input closed: {SessionId}/{Id}", sid, id);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Input canceled: {SessionId}/{Id}", sid, id);
            }
        }
        else
        {
            sid = Guid.NewGuid();

            session = new() { Aborted = aborted };
            if (!_sessions.TryAdd(sid, session))
                throw new InvalidOperationException($"Session ID conflicted: {sid}");
            _logger.LogDebug("Session started: {SessionId}", sid);
            try
            {
                await socket.SendAsync($"{new SetCookieHeaderValue(Session.Name, $"{sid}")}", aborted).ConfigureAwait(false);

                #pragma warning disable IL2026
                var args = await socket.ReceiveFromJsonAsync<string[]>(aborted).ConfigureAwait(false);
                #pragma warning restore IL2026
                if (args is null || args.Contains(null) || args.Select(a => a.Split(':', 2)[0]).Intersect(UnsupportedOptions).Any())
                    throw new ArgumentException("Invalid arguments");

                var startInfo = new ProcessStartInfo("ffmpeg")
                {
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = SystemEncoding,
                    StandardErrorEncoding = SystemEncoding,
                };
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-")
                        throw new NotSupportedException("Standard I/O is not supported");
                    if (args[i].StartsWith('-'))
                    {
                        var option = args[i];
                        if (FFmpeg.UnaryOptions.Contains(option))
                        {
                            startInfo.ArgumentList.Add(option);
                            continue;
                        }
                        if (++i >= args.Length)
                            throw new ArgumentException($"Missing argument for option '{option.TrimStart('-')}'");
                        switch (option)
                        {
                            case "-i":
                                if (!Guid.TryParse(Path.GetFileNameWithoutExtension(args[i]), out var id))
                                    throw new ArgumentException($"'{args[i]}' is not a valid input ID");
                                var uri = new Uri(_address, $"{id}{Path.GetExtension(args[i])}");
                                if (request.TryGetBasicAuthenticationCredentials(out var user, out var pass))
                                {
                                    uri = new UriBuilder(uri) { UserName = user, Password = pass }.Uri;
                                    startInfo.ArgumentList.Add("-auth_type", "basic");
                                }
                                startInfo.ArgumentList.Add("-cookies", $"{Session.Name}={sid}; path=/; domain={uri.Host}:{uri.Port}");
                                startInfo.ArgumentList.Add("-i", uri.AbsoluteUri);
                                break;
                            default:
                                startInfo.ArgumentList.Add(option, args[i]);
                                break;
                        }
                    }
                    else if (args[i] == "/dev/null")
                    {
                        startInfo.ArgumentList.Add(NullDevice);
                    }
                    else
                    {
                        if (!Guid.TryParse(Path.GetFileNameWithoutExtension(args[i]), out var id))
                            throw new ArgumentException($"'{args[i]}' is not a valid output ID");
                        var file = new FileInfo(Path.Combine(Path.GetTempPath(), $"{sid}-{id}{Path.GetExtension(args[i])}"));
                        if (!session.Outputs.TryAdd(id, (file, new())))
                            throw new ArgumentException($"Output ID conflicted: {id}");
                        startInfo.ArgumentList.Add(file.FullName);
                    }
                }
                if (session.Inputs.IsEmpty)
                    throw new ArgumentException("Output file #0 does not contain any stream");
                if (session.Inputs.Keys.Intersect(session.Outputs.Keys).Any())
                    throw new ArgumentException("Input/Output ID conflicted");

                using var process = new Process { StartInfo = startInfo };
                process.ErrorDataReceived += async (_, e) =>
                {
                    _logger.LogTrace("{ErrorData}", e.Data);
                    if (e.Data is string line)
                    {
                        if (!aborted.IsCancellationRequested && socket.State == WebSocketState.Open)
                            await socket.SendAsync(line, aborted).ConfigureAwait(false);
                    }
                };
                var exited = process.StartAsync(aborted);
                process.BeginErrorReadLine();
                session.StandardInput = process.StandardInput;
                var code = await exited.ConfigureAwait(false);
                process.CancelErrorRead();
                _logger.LogDebug("Exit code: {Code}", code);
                session.Exited.TrySetResult(code);

                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, $"{code}", aborted).ConfigureAwait(false);

                if (code == 0)
                {
                    var outputTasks = session.Outputs.Select(async pair =>
                    {
                        var (id, output) = pair;
                        try
                        {
                            _logger.LogDebug("Output awaiting: {SessionId}/{Id}", sid, id);
                            await output.Sent.Task.ConfigureAwait(false);
                            _logger.LogDebug("Output completed: {SessionId}/{Id}", sid, id);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogDebug("Output canceled: {SessionId}/{Id}", sid, id);
                        }
                    });
                    await Task.WhenAny(Task.WhenAll(outputTasks), Task.Delay(Timeout.Infinite, aborted)).ConfigureAwait(false);
                }

                _logger.LogDebug("Session ended: {SessionId}", sid);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Session aborted: {SessionId}", sid);
                session.Exited.TrySetCanceled(aborted);
            }
            finally
            {
                if (_sessions.TryRemove(sid, out var _))
                    _logger.LogDebug("Session deleted: {SessionId}", sid);
                foreach (var (_, output) in session.Outputs)
                {
                    if (output.File.Exists)
                    {
                        output.File.Delete();
                        _logger.LogDebug("Output deleted: {Name}", output.File.Name);
                    }
                }
            }
        }
    }

    private async ValueTask<int?> HandleGetAsync(HttpRequest request, HttpResponse response, CancellationToken aborted)
    {
        if (request.Cookies.TryGetValue(Session.Name, out var cookie) &&
            Guid.TryParse(cookie, out var sid) && _sessions.TryGetValue(sid, out var session))
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(request.Path), out var id))
                return Status404NotFound;

            aborted = CancellationTokenSource.CreateLinkedTokenSource(aborted, session.Aborted).Token;

            if (session.Inputs.TryGetValue(id, out var input))
            {
                var range = request.GetTypedHeaders().Range;
                if (range?.Unit != "bytes" || range?.Ranges?.Count != 1)
                    return Status416RangeNotSatisfiable;
                var from = range.Ranges.First().From ?? 0;
                var to = range.Ranges.First().To ?? input.Length - 1;
                if (to < from || from < 0 || input.Length <= to)
                    return Status416RangeNotSatisfiable;
                try
                {
                    response.StatusCode = Status206PartialContent;
                    response.ContentLength = to + 1 - from;
                    response.Headers.AcceptRanges = $"{range.Unit}";
                    response.Headers.ContentRange = $"{new ContentRangeHeaderValue(from, to, input.Length)}";

                    _logger.LogDebug("Input started: {SessionId}/{Id} {From}-{To}", sid, id, from, to);
                    for (var offset = from; offset <= to; offset += BufferSize)
                    {
                        var length = (int)Math.Min(to + 1 - offset, BufferSize);
                        Memory<byte> chunk = default;
                        await input.Semaphore.WaitAsync(aborted).ConfigureAwait(false);
                        try
                        {
                            await input.Socket.SendAsync($"{new RangeHeaderValue(offset, offset + length - 1)}", session.Aborted).ConfigureAwait(false);
                            chunk = await input.Socket.ReceiveBytesAsync(session.Aborted).ConfigureAwait(false);
                        }
                        finally
                        {
                            input.Semaphore.Release();
                        }
                        aborted.ThrowIfCancellationRequested();
                        if (chunk.Length != length)
                            throw new InvalidOperationException($"Bad length: request={length}, response={chunk.Length}");
                        await response.Body.WriteAsync(chunk, aborted).ConfigureAwait(false);
                    }
                    _logger.LogDebug("Input ended: {SessionId}/{Id} {From}-{To}", sid, id, from, to);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Input aborted: {SessionId}/{Id} {From}-{To}", sid, id, from, to);
                }
                return null;
            }

            if (session.Outputs.TryGetValue(id, out var output))
            {
                try
                {
                    using var file = output.File.OpenRead();
                    response.StatusCode = Status200OK;
                    response.ContentLength = file.Length;
                    _logger.LogDebug("Output started: {SessionId}/{Id}", sid, id);
                    await file.CopyToAsync(response.Body, BufferSize, aborted).ConfigureAwait(false);
                    _logger.LogDebug("Output ended: {SessionId}/{Id}", sid, id);
                    output.Sent.TrySetResult();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Output aborted: {SessionId}/{Id}", sid, id);
                    output.Sent.TrySetCanceled(aborted);
                }
                return null;
            }

            return Status404NotFound;
        }

        if (request.Query.TryGetValue("q", out var q) &&
            q.Split(' ') is string[] args && args.Intersect(FFmpeg.PrintOptions).Any())
        {
            var startInfo = new ProcessStartInfo("ffmpeg")
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = SystemEncoding,
                StandardOutputEncoding = SystemEncoding,
            };
            startInfo.ArgumentList.Add(args);
            _logger.LogDebug("Arguments: {Arguments}", q);

            MemoryStream stderr = new(), stdout = new();
            using var process = new Process { StartInfo = startInfo };
            await Task.WhenAll(
                process.StartAsync(aborted),
                process.StandardError.BaseStream.CopyToAsync(stderr, aborted),
                process.StandardOutput.BaseStream.CopyToAsync(stdout, aborted)
            ).ConfigureAwait(false);
            _logger.LogDebug("Exit code: {Code}", process.ExitCode);

            var pair = new[]
            {
                SystemEncoding.GetString(stdout.ToArray()),
                SystemEncoding.GetString(stderr.ToArray()),
            };
            #pragma warning disable IL2026
            await response.WriteAsJsonAsync(pair, aborted).ConfigureAwait(false);
            #pragma warning restore IL2026
            return null;
        }

        return Status404NotFound;
    }

    private async ValueTask<int?> HandlePutAsync(HttpRequest request, HttpResponse response, CancellationToken aborted)
    {
        if (request.Cookies.TryGetValue(Session.Name, out var cookie) &&
            Guid.TryParse(cookie, out var sid) && _sessions.TryGetValue(sid, out var session) &&
            session.StandardInput is StreamWriter stdin)
        {
            await request.Body.CopyToAsync(stdin.BaseStream, aborted).ConfigureAwait(false);
            response.ContentLength = 0;
            return Status202Accepted;
        }

        return Status404NotFound;
    }
}
