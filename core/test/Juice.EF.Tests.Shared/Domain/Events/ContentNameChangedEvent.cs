using Juice.MediatR;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Juice.Integrations.Tests")]
namespace Juice.EF.Tests.Domain.Events
{
    internal record ContentNameChangedEvent: MessageBase, INotification
    {
        public ContentNameChangedEvent(Content content, string originalName, string name)
        {
            ContentId = content.Id;
            OriginalName = originalName;
            Name = name;
        }
        public Guid ContentId { get; init; }
        public string Name { get; init; }
        public string OriginalName { get; init; }
    }
}
