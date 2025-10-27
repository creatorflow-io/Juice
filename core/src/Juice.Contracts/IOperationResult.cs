using Juice.Operation;

namespace Juice
{
    public interface IOperationResult
    {
        string? Message { get; }
        string? StackTrace { get; }
        bool Succeeded { get; }
        OperationalFailure Failure { get; }
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        Exception? Exception { get; }
        void ThrowIfNotSucceeded();
        string ToString();
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        virtual OperationModel OperationModel => new OperationModel(Message, StackTrace, Succeeded, Failure);
    }

    public interface IOperationResult<T> : IOperationResult
    {
        T? Data { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        T DataValue => Data ?? throw new InvalidOperationException("Data is null");
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        bool HasData => Data != null;
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        bool SucceededWithData => Succeeded && HasData;
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        new virtual OperationModel<T> OperationModel => new OperationModel<T>(Message, StackTrace, Succeeded, Failure, Data);
    }

}
