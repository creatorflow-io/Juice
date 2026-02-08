using System;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal record CreateContentCommand: Message, IRequest<IOperationResult<Guid>>, IContentCommand
    {

    }
}
