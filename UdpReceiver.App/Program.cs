using UdpReceiver.App.Services;
using UdpReceiver.App.Parsers;
using System.Text;

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

app.MapGet("/api/messages/log", (MessageStore messageStore) =>
{
    var logText = messageStore.BuildCanLogText();
    var bytes = Encoding.UTF8.GetBytes(logText);
    var fileName = $"can-log-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log";
    return Results.File(bytes, "text/plain; charset=utf-8", fileName);
});

app.MapPost("/api/messages/clear", (MessageStore messageStore) =>
{
    messageStore.Clear();
    return Results.Ok();
});

app.Run();
