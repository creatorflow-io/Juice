using System;
using System.Threading;
using System.Threading.Tasks;
using Juice.EF.Tests.Domain;
using Juice.MediatR;
using Juice.Services;
using Microsoft.Extensions.Logging;

namespace Juice.Integrations.Tests
{
    internal class CreateContent1CommandHandler : IRequestHandler<CreateContent1Command, IOperationResult<Guid>>
    {
        private readonly ContentRepository _contentRepository;
        private readonly ILogger _logger;
        private readonly IStringIdGenerator _idGenerator;
        public CreateContent1CommandHandler(ContentRepository contentRepository,
            ILogger<CreateContent1CommandHandler> logger,
            IStringIdGenerator idGenerator
            )
        {
            _contentRepository = contentRepository;
            _logger = logger;
            _idGenerator = idGenerator;
        }
        public async ValueTask<IOperationResult<Guid>> Handle(CreateContent1Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating new content");
            var code1 = _idGenerator.GenerateRandomId(6);
            var content = new Content(code1, "Test name " + DateTimeOffset.Now.ToString());
            var added = await _contentRepository.AddAsync(content, cancellationToken);
            if(!added.SucceededWithData)
            {
                _logger.LogError("Failed to add content: {0}", added);
                return added.Of<Guid>();
            }
            return OperationResult.Result(added.DataValue.Id);
        }
    }
}
