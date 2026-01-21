using System;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal class CreateContentCommand: IRequest<IOperationResult<Guid>>, IContentCommand
    {

    }
}
