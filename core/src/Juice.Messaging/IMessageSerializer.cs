namespace Juice.Messaging
{
    public interface IMessageSerializer
    {
        T? Deserialize<T>(string? payload, Type? eventType = default);
        string Serialize(object? value);
        byte[] SerializeToUtf8Bytes<T>(T? value);
        T? DeserializeFromUtf8Bytes<T>(byte[]? payload, Type? eventType = default);
    }
}
