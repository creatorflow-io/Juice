using System;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal class ChangeContentName1Command: IRequest<IOperationResult>, IContentCommand
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
