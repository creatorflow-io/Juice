using System;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal class CreateContent1Command: IRequest<IOperationResult<Guid>>, IContentCommand
    {

    }
}
