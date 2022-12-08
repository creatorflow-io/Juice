using System.Diagnostics;
using Grpc.Core;
using Juice.MultiTenant.Grpc;
using Microsoft.EntityFrameworkCore;

namespace Juice.MultiTenant.EF.Grpc.Services
{
    public class TenantStoreService : TenantStore.TenantStoreBase
    {
        private readonly TenantStoreDbContext<Tenant> _dbContext;
        public TenantStoreService(TenantStoreDbContext<Tenant> dbContext)
        {
            _dbContext = dbContext;
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
                Console.WriteLine("Take {0} milliseconds", timer.ElapsedMilliseconds);
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

        public override async Task<TenantOperationResult> TryAdd(TenantInfo request, ServerCallContext context)
        {
            try
            {
                var tenant = new Tenant
                {
                    Id = request.Id,
                    ConnectionString = request.ConnectionString,
                    Identifier = request.Identifier,
                    Name = request.Name
                };
                _dbContext.Add(tenant);
                await _dbContext.SaveChangesAsync();
                return new TenantOperationResult
                {
                    Message = "Tenant added!",
                    Succeeded = true
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

        public override async Task<TenantOperationResult> TryUpdate(TenantInfo request, ServerCallContext context)
        {
            try
            {
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return new TenantOperationResult
                    {
                        Succeeded = false,
                        Message = "Tenant not found"
                    };
                }
                tenant.Name = request.Name;
                tenant.Identifier = request.Identifier;
                tenant.ConnectionString = request.ConnectionString;
                await _dbContext.SaveChangesAsync();
                return new TenantOperationResult
                {
                    Succeeded = true,
                    Message = "Tenant updated!"
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
                var tenant = await _dbContext.TenantInfo
                                .Where(ti => ti.Id == request.Id).FirstOrDefaultAsync();
                if (tenant == null)
                {
                    return new TenantOperationResult
                    {
                        Succeeded = true,
                        Message = "Tenant does not exist"
                    };
                }
                _dbContext.Remove(tenant);

                await _dbContext.SaveChangesAsync();

                return new TenantOperationResult
                {
                    Succeeded = true,
                    Message = "Tenant deleted!"
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
