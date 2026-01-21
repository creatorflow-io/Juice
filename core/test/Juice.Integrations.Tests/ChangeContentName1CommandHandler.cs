using System.Threading;
using System.Threading.Tasks;
using Juice.EF.Tests.Domain;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal class ChangeContentName1CommandHandler : IRequestHandler<ChangeContentName1Command, IOperationResult>
    {
        private readonly ContentRepository _contentRepository;
        public ChangeContentName1CommandHandler(ContentRepository contentRepository)
        {
            _contentRepository = contentRepository;
        }
        public async ValueTask<IOperationResult> Handle(ChangeContentName1Command request, CancellationToken cancellationToken)
        {
            var content = await _contentRepository.GetAsync(request.ContentId);
            if (content == null)
            {
                return OperationResult.Failed($"Content with id {request.ContentId} not found.");
            }
            content.ChangeName(request.Name);
            return await _contentRepository.UpdateAsync(content, cancellationToken);
        }
    }
}
