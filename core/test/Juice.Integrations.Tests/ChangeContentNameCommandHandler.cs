using System.Threading;
using System.Threading.Tasks;
using Juice.Domain;
using Juice.EF.Tests.Domain;
using Juice.MediatR;

namespace Juice.Integrations.Tests
{
    internal class ChangeContentNameCommandHandler : IRequestHandler<ChangeContentNameCommand, IOperationResult>
    {
        private IUnitOfWork<Content> _unitOfWork;
        public ChangeContentNameCommandHandler(IUnitOfWork<Content> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async ValueTask<IOperationResult> Handle(ChangeContentNameCommand request, CancellationToken cancellationToken)
        {
            var content = await _unitOfWork.FindAsync(c => c.Id == request.ContentId, cancellationToken: cancellationToken);
            if (content is null)
            {
                return OperationResult.Failed("Content not found");
            }
            content.ChangeName(request.NewName);
            return OperationResult.Success;
        }
    }
}
