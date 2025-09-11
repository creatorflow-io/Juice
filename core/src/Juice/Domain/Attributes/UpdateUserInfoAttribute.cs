namespace Juice.Domain.Attributes
{
    /// <summary>
    /// Notifies the system to update the user information on this property when the entity is created or modified.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class UpdateUserInfoAttribute: Attribute
    {
        public EntityStates UpdateOn { get; }
        public UpdateUserInfoAttribute(EntityStates updateOn)
        {
            UpdateOn = updateOn;
        }
        public UpdateUserInfoAttribute()
        {
            UpdateOn = EntityStates.Created | EntityStates.Modified;
        }
    }
}
