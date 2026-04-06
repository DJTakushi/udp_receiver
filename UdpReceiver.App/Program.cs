using UdpReceiver.App.Services;
using UdpReceiver.App.Parsers;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
});

builder.Services.AddSingleton<MessageStore>();
builder.Services.AddSingleton<ICanMessageParser, UsrCanetParser>();
builder.Services.AddHostedService<UdpListenerService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/messages", (MessageStore messageStore) =>
{
    return Results.Ok(messageStore.GetSnapshot());
});

app.Run();
