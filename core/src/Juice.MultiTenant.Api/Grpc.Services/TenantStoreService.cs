using System.Diagnostics;
using Grpc.Core;
using Juice.MultiTenant.Api.Commands;
using Juice.MultiTenant.Grpc;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.EF.Grpc.Services
{
    public class TenantStoreService : TenantStore.TenantStoreBase
    {
        private readonly TenantStoreDbContext<Tenant> _dbContext;
        private readonly IMediator _mediator;
        public TenantStoreService(TenantStoreDbContext<Tenant> dbContext, IMediator mediator)
        {
            _dbContext = dbContext;
            _mediator = mediator;
        }

        public override async Task<TenantInfo?> TryGetByIdentifier(TenantIdenfier request, ServerCallContext? context = default)
        {
            var timer = new Stopwatch();
            try
            {
                timer.Start();
                return await _dbContext.TenantInfo
                            .Where(ti => ti.Identifier == request.Identifier)
                            .Select(ti => new TenantInfo
                            {
                                Id = ti.Id,
                                Name = ti.Name,
                                Identifier = ti.Identifier,
                                ConnectionString = ti.ConnectionString,
                                Disabled = ti.Disabled,
                                SerializedProperties = ti.SerializedProperties
                            })
                            .SingleOrDefaultAsync();
            }
            finally
            {
                timer.Stop();
                var requestId = context?.GetHttpContext()?.Request?.Headers["x-requestid"];
                Console.WriteLine("Find tenant by identifier take {0} milliseconds." + requestId ?? "",
                    timer.ElapsedMilliseconds);
            }
        }

        public override async Task<TenantQueryResult> GetAll(TenantQuery request, ServerCallContext context)
        {
            var tenants = await _dbContext
                .TenantInfo
                .Select(ti => new TenantInfo
                {
                    Id = ti.Id,
                    Name = ti.Name,
                    Identifier = ti.Identifier,
                    ConnectionString = ti.ConnectionString,
                    Disabled = ti.Disabled,
                    SerializedProperties = ti.SerializedProperties
                })
                .ToListAsync();
            var result = new TenantQueryResult
            {
            };
            result.Tenants.AddRange(tenants);
            return result;
        }

        public override async Task<TenantInfo?> TryGet(TenantIdenfier request, ServerCallContext context)
        {
            return await _dbContext.TenantInfo
                            .Where(ti => ti.Id == request.Id)
                            .Select(ti => new TenantInfo
                            {
                                Id = ti.Id,
                                Name = ti.Name,
                                Identifier = ti.Identifier,
                                ConnectionString = ti.ConnectionString,
                                Disabled = ti.Disabled,
                                SerializedProperties = ti.SerializedProperties
                            })
                            .SingleOrDefaultAsync();
        }

        public override async Task<TenantOperationResult> TryAdd(TenantInfo request, ServerCallContext context)
        {
            try
            {
                var command = new CreateTenantCommand(request.Id, request.Identifier, request.Name, request.ConnectionString);

                var result = await _mediator.Send(command);

                return new TenantOperationResult
                {
                    Message = result.Message,
                    Succeeded = result.Succeeded
                };
            }
            catch (Exception ex)
            {
                return new TenantOperationResult
                {
                    Message = ex.Message,
                    Succeeded = false
                };
            }
        }

        public override async Task<TenantOperationResult> TryUpdate(TenantInfo request, ServerCallContext context)
        {
            try
            {
                var command = new UpdateTenantCommand(request.Id, request.Identifier, request.Name, request.ConnectionString);

                var result = await _mediator.Send(command);

                return new TenantOperationResult
                {
                    Message = result.Message,
                    Succeeded = result.Succeeded
                };
            }
            catch (Exception ex)
            {
                return new TenantOperationResult
                {
                    Message = ex.Message,
                    Succeeded = false
                };
            }
        }

        public override async Task<TenantOperationResult> TryRemove(TenantIdenfier request, ServerCallContext context)
        {
            try
            {
                var command = new DeleteTenantCommand(request.Id);

                var result = await _mediator.Send(command);

                return new TenantOperationResult
                {
                    Message = result.Message,
                    Succeeded = result.Succeeded
                };
            }
            catch (Exception ex)
            {
                return new TenantOperationResult
                {
                    Message = ex.Message,
                    Succeeded = false
                };
            }
        }
    }
}
