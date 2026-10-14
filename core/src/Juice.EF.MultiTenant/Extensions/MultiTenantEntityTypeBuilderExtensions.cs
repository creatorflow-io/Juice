using System.Linq.Expressions;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Juice.EF.MultiTenant;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Juice.MultiTenant.EF.Extensions
{

    public static class MultiTenantEntityTypeBuilderExtensions
    {
        private class ExpressionVariableScope
        {
            public IMultiTenantDbContext? Context { get; }
        }

        private static LambdaExpression? GetQueryFilter(this EntityTypeBuilder builder)
        {
            return builder.Metadata.GetQueryFilter();
        }
        /// <summary>
        /// <para>***</para>
        /// NOTE: Use <see cref="IsMultiTenant" /> with <see cref="SharingType.Tenant"/> to mark an entity as cross-tenant instead.
        /// <para/>
        /// Adds MultiTenant support for an entity. Call <see cref="IsMultiTenant" /> after
        /// <see cref="EntityTypeBuilder.HasQueryFilter" /> to merge query filters. It is recommended
        /// to query the entries that have matched tenant first then fallback to non-tenant entries.
        /// 
        /// </summary>
        /// <param name="builder">The typed EntityTypeBuilder instance.</param>
        /// <returns>A MultiTenantEntityTypeBuilder instance.</returns>
        ///
        public static MultiTenantEntityTypeBuilder IsCrossTenant(this EntityTypeBuilder builder)
            => IsMultiTenant(builder, SharingType.Tenant);

        /// <summary>
        /// Adds MultiTenant support for an entity. Call <see cref="IsMultiTenant" /> after
        /// <see cref="EntityTypeBuilder.HasQueryFilter" /> to merge query filters. It is recommended
        /// to query the entries that have matched tenant first then fallback to non-tenant entries.
        /// 
        /// </summary>
        /// <param name="builder">The typed EntityTypeBuilder instance.</param>
        /// <param name="entitySharingType"></param>
        /// <returns>A MultiTenantEntityTypeBuilder instance.</returns>
        ///
        public static MultiTenantEntityTypeBuilder IsMultiTenant(this EntityTypeBuilder builder, SharingType entitySharingType)
        {
            if (builder.Metadata.IsMultiTenant())
                return new MultiTenantEntityTypeBuilder(builder);

            builder.HasAnnotation(Finbuckle.MultiTenant.EntityFrameworkCore.Constants.MultiTenantAnnotationName, entitySharingType);

            try
            {
                builder.Property<string>("TenantId")
                       //.IsRequired()
                       .HasMaxLength(Constants.TenantIdMaxLength);
                //                       .HasValueGenerator<TenantIdGenerator>();
            }
            catch (Exception ex)
            {
                throw new MultiTenantException($"{builder.Metadata.ClrType} unable to add TenantId property", ex);
            }

            // build expression tree for e => EF.Property<string>(e, "TenantId") == TenantInfo.Id

            // where e is one of our entity types
            // will need this ParameterExpression for next step and for final step
            var entityParamExp = Expression.Parameter(builder.Metadata.ClrType, "e");

            var existingQueryFilter = builder.GetQueryFilter();

            // override to match existing query parameter if applicable
            if (existingQueryFilter != null)
            {
                entityParamExp = existingQueryFilter.Parameters.First();
            }

            // build up expression tree for: EF.Property<string>(e, "TenantId")
            var tenantIdExp = Expression.Constant("TenantId", typeof(string));
            var efPropertyExp = Expression.Call(typeof(Microsoft.EntityFrameworkCore.EF), nameof(Microsoft.EntityFrameworkCore.EF.Property), new[] { typeof(string) }, entityParamExp, tenantIdExp);
            var entityExp = efPropertyExp;

            // build up express tree for: TenantInfo?.Id
            // EF will magically sub the current db context in for scope.Context
            var scopeConstantExp = Expression.Constant(new ExpressionVariableScope());
            var contextMemberInfo = typeof(ExpressionVariableScope).GetMember(nameof(ExpressionVariableScope.Context))[0];
            var contextMemberAccessExp = Expression.MakeMemberAccess(scopeConstantExp, contextMemberInfo);
            var contextTenantInfoExp = Expression.Property(contextMemberAccessExp, nameof(IMultiTenantDbContext.TenantInfo));
            var tenantInfoIsNullExp = Expression.Equal(contextTenantInfoExp, Expression.Constant(null, typeof(ITenantInfo)));
            var tenantInfoIsNotNullExp = Expression.Not(tenantInfoIsNullExp);
            var tenantInfoIdExp = Expression.Property(contextTenantInfoExp, nameof(ITenantInfo.Id));
            var contextExp = Expression.Condition(
                tenantInfoIsNullExp,
                Expression.Constant(null, typeof(string)),
                tenantInfoIdExp
            );

            var predicate = entitySharingType switch
            {
                // (EF.Property<string>(e, "TenantId") == TenantInfo.Id
                // OR ( EF.Property<string>(e, "TenantId") == null
                // OR EF.Property<string>(e, "TenantId") == '')
                SharingType.Tenant => Expression.OrElse(
                    Expression.Equal(entityExp, contextExp),
                    Expression.OrElse(
                        Expression.Equal(entityExp, Expression.Constant(null)),
                        Expression.Equal(entityExp, Expression.Constant("")))),
                // (EF.Property<string>(e, "TenantId") == TenantInfo.Id
                // OR TenantInfo.Id == null OR TenantInfo.Id == ''
                SharingType.Global => Expression.OrElse(
                    Expression.Equal(entityExp, contextExp),
                    Expression.OrElse(
                        Expression.Equal(contextExp, Expression.Constant(null)),
                        Expression.Equal(contextExp, Expression.Constant("")))),
                // (EF.Property<string>(e, "TenantId") == TenantInfo.Id
                // OR (
                //      (EF.Property<string>(e, "TenantId") == null OR EF.Property<string>(e, "TenantId") == '')
                //      AND (TenantInfo.Id == null OR TenantInfo.Id == '')
                //    )
                _ => Expression.OrElse(
                    Expression.Equal(entityExp, contextExp),
                    Expression.AndAlso(
                        Expression.OrElse(
                            Expression.Equal(entityExp, Expression.Constant(null)),
                            Expression.Equal(entityExp, Expression.Constant(""))),
                        Expression.OrElse(
                            Expression.Equal(contextExp, Expression.Constant(null)),
                            Expression.Equal(contextExp, Expression.Constant("")))))
            };

            // combine with existing filter
            if (existingQueryFilter != null)
            {
                predicate = Expression.AndAlso(existingQueryFilter.Body, predicate);
            }

            // build the final expression tree
            var delegateType = Expression.GetDelegateType(builder.Metadata.ClrType, typeof(bool));
            var lambdaExp = Expression.Lambda(delegateType, predicate, entityParamExp);

            // set the filter
            builder.HasQueryFilter(lambdaExp);

            return new MultiTenantEntityTypeBuilder(builder);

        }

    }
}
