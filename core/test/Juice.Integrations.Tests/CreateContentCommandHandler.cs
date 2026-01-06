using System;
using System.Threading;
using System.Threading.Tasks;
using Juice.Domain;
using Juice.EF.Tests.Domain;
using Juice.MediatR;
using Juice.Services;

namespace Juice.Integrations.Tests
{

    internal class CreateContentCommandHandler : IRequestHandler<CreateContentCommand, IOperationResult>
    {
        private readonly IStringIdGenerator _idGenerator;
        private readonly IUnitOfWork<Content> _unitOfWork;
        public CreateContentCommandHandler(IStringIdGenerator idGenerator, IUnitOfWork<Content> unitOfWork)
        {
            _idGenerator = idGenerator;
            _unitOfWork = unitOfWork;
        }

        public async ValueTask<IOperationResult> Handle(CreateContentCommand request, CancellationToken cancellationToken)
        {
            var code1 = _idGenerator.GenerateRandomId(6);
            var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());
            await _unitOfWork.AddAsync(content, cancellationToken);

            return OperationResult.Success;
        }
    }
}
