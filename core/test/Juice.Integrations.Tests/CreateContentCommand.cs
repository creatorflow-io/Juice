using System;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal record CreateContentCommand: MessageBase, IRequest<IOperationResult<Guid>>, IContentCommand
    {

    }
}
