using UdpReceiver.App.Models;

namespace UdpReceiver.App.Parsers;

public interface ICanMessageParser
{
    string HardwareType { get; }

    bool CanParse(byte[] data);

    IReadOnlyList<CanFrameRecord> Parse(byte[] data, DateTimeOffset timestamp, string source, string target);
}
