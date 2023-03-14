using Apache;

const string Usage = @"Usage: ffremote [-w http://worker/endpoint] [options] [[infile options] -i infile]... {[outfile options] outfile}...

Use -h to get full help or, even better, run 'man ffmpeg'";

if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is not { Length: > 0 })
{
    if (args.Length == 0 || args[0] == "-w" && args.Length <= 2)
    {
        Console.Error.WriteLine(Usage);
        return;
    }
    await Host.CreateDefaultBuilder(FFmpeg.TranslateArguments(args))
              .ConfigureServices(services => services.AddHostedService<Client>())
              .RunConsoleAsync(console => console.SuppressStatusMessages = true)
              .ConfigureAwait(false);
}
else
{
    var web = WebApplication.CreateBuilder();
    web.Configuration.AddHtpasswdFile();
    web.Services.AddHtpasswd(web.Configuration);
    var app = web.Build();
    app.UseWebSockets()
       .UseMiddleware<Worker>();
    app.Run();
}
