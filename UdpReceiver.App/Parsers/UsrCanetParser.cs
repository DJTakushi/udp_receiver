using System.Text;
using UdpReceiver.App.Models;

namespace UdpReceiver.App.Parsers;

public sealed class UsrCanetParser : ICanMessageParser
{
    private const int FrameSize = 13;

    public string HardwareType => "USR-CANET200";

    public bool CanParse(byte[] data)
    {
        if (data.Length == 0)
        {
            return false;
        }

        if (ValidateFrameSequence(data, 0))
        {
            return true;
        }

        int splitIndex = FindFrameStartIndex(data);
        if (splitIndex >= 0 && ValidateFrameSequence(data, splitIndex))
        {
            return true;
        }

        return data.Length > 5 && IsAllAscii(data, 0, data.Length) && Encoding.ASCII.GetString(data).Contains('|');
    }

    public IReadOnlyList<CanFrameRecord> Parse(
        byte[] data,
        DateTimeOffset timestamp,
        string source,
        string target)
    {
        if (data.Length == 0)
        {
            return [];
        }

        if (IsAllAscii(data, 0, data.Length) && Encoding.ASCII.GetString(data).Contains('|'))
        {
            return [];
        }

        int startIndex = ValidateFrameSequence(data, 0) ? 0 : FindFrameStartIndex(data);
        if (startIndex < 0 || !ValidateFrameSequence(data, startIndex))
        {
            return [];
        }

        string identity = startIndex > 0
            ? Encoding.ASCII.GetString(data, 0, startIndex).TrimEnd('\r', '\n')
            : string.Empty;

        var frames = new List<CanFrameRecord>();
        for (int i = startIndex; i <= data.Length - FrameSize; i += FrameSize)
        {
            byte frameInfo = data[i];
            bool isExtended = (frameInfo & 0x80) != 0;
            bool isRtr = (frameInfo & 0x40) != 0;
            int canDlc = frameInfo & 0x0F;

            uint rawId = (uint)((data[i + 1] << 24) | (data[i + 2] << 16) | (data[i + 3] << 8) | data[i + 4]);
            uint canId = rawId & 0x1FFFFFFF;

            byte[] frameData = new byte[8];
            Array.Copy(data, i + 5, frameData, 0, 8);

            frames.Add(new CanFrameRecord(
                Timestamp: timestamp,
                Source: source,
                Target: target,
                Identity: identity,
                FrameInfo: frameInfo,
                IsExtended: isExtended,
                IsRtr: isRtr,
                CanDlc: canDlc,
                CanId: canId,
                DataBytes: frameData));
        }

        return frames;
    }

    private static int FindFrameStartIndex(byte[] data)
    {
        for (int i = 0; i <= data.Length - FrameSize; i++)
        {
            byte b = data[i];
            if ((b >= 32 && b <= 126) || b == 13 || b == 10)
            {
                continue;
            }

            int remaining = data.Length - i;
            if (remaining >= FrameSize && remaining % FrameSize == 0 && IsValidInfoByte(b))
            {
                return i;
            }

            return -1;
        }

        return -1;
    }

    private static bool ValidateFrameSequence(byte[] data, int startIndex)
    {
        int remaining = data.Length - startIndex;
        if (remaining < FrameSize || remaining % FrameSize != 0)
        {
            return false;
        }

        for (int i = startIndex; i <= data.Length - FrameSize; i += FrameSize)
        {
            if (!IsValidInfoByte(data[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidInfoByte(byte b)
    {
        if ((b & 0x30) != 0)
        {
            return false;
        }

        return (b & 0x0F) <= 8;
    }

    private static bool IsAllAscii(byte[] data, int start, int length)
    {
        for (int i = start; i < start + length; i++)
        {
            byte b = data[i];
            if ((b < 32 && b != 13 && b != 10) || b > 126)
            {
                return false;
            }
        }

        return true;
    }
}
