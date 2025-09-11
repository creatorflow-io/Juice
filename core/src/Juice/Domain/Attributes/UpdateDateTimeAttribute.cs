namespace Juice.Domain.Attributes
{
    /// <summary>
    /// Notifies the system to update the current datetime on this property when the entity is created or modified.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class UpdateDateTimeAttribute: Attribute
    {
        public EntityStates UpdateOn { get; }
        public UpdateDateTimeAttribute(EntityStates updateOn)
        {
            UpdateOn = updateOn;
        }
        public UpdateDateTimeAttribute()
        {
            UpdateOn = EntityStates.Created | EntityStates.Modified;
        }
    }
}
