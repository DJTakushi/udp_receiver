namespace UdpReceiver.App.Models;

public sealed record CanFrameRecord(
    DateTimeOffset Timestamp,
    string Source,
    string Target,
    string Identity,
    byte FrameInfo,
    bool IsExtended,
    bool IsRtr,
    int CanDlc,
    uint CanId,
    byte[] DataBytes
);
