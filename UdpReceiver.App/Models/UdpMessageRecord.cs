using System.Buffers.Binary;
using System.Text;

namespace UdpReceiver.App.Models;

public sealed record UdpMessageRecord(
    DateTimeOffset Timestamp,
    string Source,
    string Target,
    string Identity,
    byte FrameInfo,
    uint CanId,
    byte[] DataBytes
)
{
    // Wire format (back to front): [identity…][frameInfo (1)][canId (4)][dataBytes (8)]
    private const int DataLen = 8;
    private const int CanIdLen = 4;
    private const int MinLen = 1 + CanIdLen + DataLen; // 13

    public static UdpMessageRecord Parse(
        DateTimeOffset timestamp, string source, string target, byte[] raw)
    {
        if (raw.Length < MinLen)
        {
            return new UdpMessageRecord(
                timestamp, source, target,
                Identity: string.Empty,
                FrameInfo: 0,
                CanId: 0,
                DataBytes: raw);
        }

        int dataStart     = raw.Length - DataLen;
        int canIdStart    = dataStart  - CanIdLen;
        int frameInfoIdx  = canIdStart - 1;

        return new UdpMessageRecord(
            timestamp, source, target,
            Identity:  Encoding.UTF8.GetString(raw, 0, frameInfoIdx),
            FrameInfo: raw[frameInfoIdx],
            CanId:     BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(canIdStart, CanIdLen)),
            DataBytes: raw[dataStart..]);
    }
}
