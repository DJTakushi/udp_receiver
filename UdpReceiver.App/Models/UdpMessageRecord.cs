namespace UdpReceiver.App.Models;

public sealed record UdpMessageRecord(
    DateTimeOffset Timestamp,
    string Source,
    byte[] Payload
);
