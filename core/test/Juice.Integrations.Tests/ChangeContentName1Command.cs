using System;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal record ChangeContentName1Command: MessageBase, IRequest<IOperationResult>, IContentCommand
    {
        public ChangeContentName1Command(Guid contentId, string name)
        {
            ContentId = contentId;
            Name = name;
        }
        public Guid ContentId { get; init; }
        public string Name { get; init; }
    }
}
