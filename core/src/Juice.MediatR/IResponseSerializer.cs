namespace Juice.MediatR
{
    public interface IResponseSerializer
    {
        string? SerializeResponse<T>(T? response);
        T? DeserializeResponse<T>(string? response);
    }
}
