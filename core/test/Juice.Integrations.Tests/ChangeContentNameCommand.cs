using System;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal record ChangeContentNameCommand: IRequest<IOperationResult>, IContentCommand
    {
        public Guid ContentId { get; init; }
        public string NewName { get; init; }
        public ChangeContentNameCommand(Guid contentId, string newName)
        {
            ContentId = contentId;
            NewName = newName;
        }
    }
}
