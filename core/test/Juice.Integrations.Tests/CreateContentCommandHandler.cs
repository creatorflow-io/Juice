using System;
using System.Threading;
using System.Threading.Tasks;
using Juice.Domain;
using Juice.EF.Tests.Domain;
using Juice.MediatR;
using Juice.Services;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.Tests
{

    internal class CreateContentCommandHandler : IRequestHandler<CreateContentCommand, IOperationResult<Guid>>
    {
        private readonly IStringIdGenerator _idGenerator;
        private readonly IUnitOfWork<Content> _unitOfWork;
        private readonly ILogger _logger;
        public CreateContentCommandHandler(IStringIdGenerator idGenerator, IUnitOfWork<Content> unitOfWork, ILogger<CreateContentCommandHandler> logger)
        {
            _idGenerator = idGenerator;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async ValueTask<IOperationResult<Guid>> Handle(CreateContentCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating new content");
            var code1 = _idGenerator.GenerateRandomId(6);
            var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());
            await _unitOfWork.AddAsync(content, cancellationToken);

            return OperationResult.Result(content.Id);
        }
    }
}
