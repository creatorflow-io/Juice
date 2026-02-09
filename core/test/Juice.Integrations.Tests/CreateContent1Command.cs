using System;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal record CreateContent1Command: MessageBase, IRequest<IOperationResult<Guid>>, IContentCommand
    {

    }
}
