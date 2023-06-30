using System.Linq.Expressions;

namespace Juice.Extensions
{
    public static class QueryableExtensions
    {
        public static IQueryable<TSource> OrderBy<TSource>(
       this IQueryable<TSource> query, string propertyName)
        {
            var entityType = typeof(TSource);
            if (entityType.GetProperty(propertyName) == null)
            {
                return query;
            }
            //Create x=>x.PropName
            var propertyInfo = entityType.GetProperty(propertyName);
            ParameterExpression arg = Expression.Parameter(entityType, "x");
            MemberExpression property = Expression.Property(arg, propertyName);
            var selector = Expression.Lambda<Func<TSource, object>>(property, new ParameterExpression[] { arg });

            return query.OrderBy(selector);
        }

        public static IOrderedQueryable<TSource> ThenBy<TSource>(
            this IOrderedQueryable<TSource> query, string propertyName)
        {
            var entityType = typeof(TSource);
            if (entityType.GetProperty(propertyName) == null)
            {
                return query;
            }
            //Create x=>x.PropName
            ParameterExpression arg = Expression.Parameter(entityType, "x");
            MemberExpression property = Expression.Property(arg, propertyName);
            var selector = Expression.Lambda<Func<TSource, object>>(property, new ParameterExpression[] { arg });

            return query.ThenBy(selector);
        }

        public static IQueryable<TSource> OrderByDescending<TSource>(
            this IQueryable<TSource> query, string propertyName)
        {
            var entityType = typeof(TSource);
            if (entityType.GetProperty(propertyName) == null)
            {
                return query;
            }
            //Create x=>x.PropName
            var propertyInfo = entityType.GetProperty(propertyName);
            ParameterExpression arg = Expression.Parameter(entityType, "x");
            MemberExpression property = Expression.Property(arg, propertyName);
            var selector = Expression.Lambda<Func<TSource, object>>(property, new ParameterExpression[] { arg });

            return query.OrderByDescending(selector);
        }

        public static IOrderedQueryable<TSource> ThenByDescending<TSource>(
            this IOrderedQueryable<TSource> query, string propertyName)
        {
            var entityType = typeof(TSource);
            if (entityType.GetProperty(propertyName) == null)
            {
                return query;
            }
            //Create x=>x.PropName
            ParameterExpression arg = Expression.Parameter(entityType, "x");
            MemberExpression property = Expression.Property(arg, propertyName);
            var selector = Expression.Lambda<Func<TSource, object>>(property, new ParameterExpression[] { arg });

            return query.ThenByDescending(selector);
        }

    }
}
