namespace UdpReceiver.App.Models;

public sealed record UdpMessageRecord(
    DateTimeOffset Timestamp,
    string Source,
    string Target,
    byte[] Payload
);
