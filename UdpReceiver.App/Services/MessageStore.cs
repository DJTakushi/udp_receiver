using UdpReceiver.App.Models;

namespace UdpReceiver.App.Services;

public sealed class MessageStore
{
    private readonly object _gate = new();
    private readonly LinkedList<UdpMessageRecord> _messages = new();
    private readonly int _capacity;

    public MessageStore(IConfiguration configuration)
    {
        _capacity = Math.Max(1, configuration.GetValue<int?>("Udp:MaxRecords") ?? 100);
    }

    public void Add(UdpMessageRecord message)
    {
        lock (_gate)
        {
            _messages.AddFirst(message);
            while (_messages.Count > _capacity)
            {
                _messages.RemoveLast();
            }
        }
    }

    public IReadOnlyList<UdpMessageRecord> GetLatest()
    {
        lock (_gate)
        {
            return _messages.ToList();
        }
    }
}
