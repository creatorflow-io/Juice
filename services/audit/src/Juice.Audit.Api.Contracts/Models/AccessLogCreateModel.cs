using Juice.Domain;

namespace Juice.Audit.Api.Contracts.Models
{
    public class AccessLogCreateModel : IValidatable
    {
        public AccessLogCreateModel(DateTimeOffset dateTime, string? user, string action, string? requestId, string extraMetadata, string request_Method, string? request_Host, string request_Path, string? request_Data, string? request_QueryString, string? request_Headers, string? request_Schema, string? request_RemoteIpAddress, string? request_AccessZone, int response_StatusCode, string? response_Headers, string? response_Data, string? response_Message, string? response_Error, long? response_ElapsedMilliseconds, string server_MachineName, string server_OSVersion, string? server_SoftwareVersion, string server_AppName)
        {
            DateTime = dateTime;
            User = user;
            Action = action;
            RequestId = requestId;
            ExtraMetadata = extraMetadata;
            Request_Method = request_Method;
            Request_Host = request_Host;
            Request_Path = request_Path;
            Request_Data = request_Data;
            Request_QueryString = request_QueryString;
            Request_Headers = request_Headers;
            Request_Schema = request_Schema;
            Request_RemoteIpAddress = request_RemoteIpAddress;
            Request_AccessZone = request_AccessZone;
            Response_StatusCode = response_StatusCode;
            Response_Headers = response_Headers;
            Response_Data = response_Data;
            Response_Message = response_Message;
            Response_Error = response_Error;
            Response_ElapsedMilliseconds = response_ElapsedMilliseconds;
            Server_MachineName = ValidatableExtensions.TrimExceededLength(server_MachineName, LengthConstants.NameLength) ?? "";
            Server_OSVersion = ValidatableExtensions.TrimExceededLength(server_OSVersion, LengthConstants.NameLength) ?? "";
            Server_SoftwareVersion = ValidatableExtensions.TrimExceededLength(server_SoftwareVersion, LengthConstants.NameLength) ?? "";
            Server_AppName = ValidatableExtensions.TrimExceededLength(server_AppName, LengthConstants.NameLength) ?? "";

            this.NotNullOrWhiteSpace(Action, LengthConstants.NameLength);

            this.NotExceededLength(RequestId, LengthConstants.IdentityLength);

            this.NotExceededLength(Request_Method, LengthConstants.IdentityLength);
            this.NotExceededLength(Request_Host, LengthConstants.NameLength);
            this.NotExceededLength(Request_Path, LengthConstants.NameLength);
            this.NotExceededLength(Request_Schema, LengthConstants.IdentityLength);
            this.NotExceededLength(Request_RemoteIpAddress, LengthConstants.IdentityLength);

        }

        public DateTimeOffset DateTime { get; init; }

        public string? User { get; init; }

        public string Action { get; private set; }

        public string? RequestId { get; private set; }

        public string ExtraMetadata { get; private set; } = "{}";


        #region RequestInfo
        public string Request_Method { get; private set; }
        public string? Request_Host { get; private set; }
        public string Request_Path { get; private set; }
        public string? Request_Data { get; private set; }
        public string? Request_QueryString { get; private set; }
        public string? Request_Headers { get; private set; }
        public string? Request_Schema { get; private set; }
        public string? Request_RemoteIpAddress { get; private set; }
        public string? Request_AccessZone { get; private set; }
        #endregion

        #region ResponseInfo
        public int Response_StatusCode { get; private set; }
        public string? Response_Headers { get; private set; }
        public string? Response_Data { get; private set; }
        public string? Response_Message { get; private set; }
        public string? Response_Error { get; private set; }
        public long? Response_ElapsedMilliseconds { get; private set; }
        #endregion

        #region ServerInfo
        public string Server_MachineName { get; private set; }
        public string Server_OSVersion { get; private set; }
        public string? Server_SoftwareVersion { get; private set; }
        public string Server_AppName { get; private set; }
        #endregion


        public IList<string> ValidationErrors => new List<string>();
    }
}
