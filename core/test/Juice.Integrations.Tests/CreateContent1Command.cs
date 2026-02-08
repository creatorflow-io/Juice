using System;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal record CreateContent1Command: Message, IRequest<IOperationResult<Guid>>, IContentCommand
    {

    }
}
