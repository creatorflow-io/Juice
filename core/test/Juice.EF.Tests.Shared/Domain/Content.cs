using System;
using Juice.Domain;
using Juice.Domain.Attributes;
using Juice.EF.Tests.Domain.Events;
using Juice.MediatR;

namespace Juice.EF.Tests.Domain
{
    [Notice(EntityStates.Created | EntityStates.Modified)]
    public class Content : DynamicAuditEntity<Guid>, IAggregateRoot<INotification>
    {
        public Content(string code, string name) : base(Guid.NewGuid(), name)
        {
            Name = name;
            Code = code;
        }

        public string Code { get; private set; }

        [UpdateDateTime(EntityStates.Created)]
        public DateTimeOffset AlternativeCreationDate { get; private set; }
        [UpdateDateTime(EntityStates.Modified)]
        public DateTimeOffset? AlternativeModificationDate { get; private set; }
        [UpdateUserInfoAttribute(EntityStates.Created)]
        public string? AlternativeCreatedUser { get; private set; }
        [UpdateUserInfoAttribute(EntityStates.Modified)]
        public string? AlternativeModifiedUser { get; private set; }

        public IList<INotification> DomainEvents { get; } = [];

        public void ChangeName(string name)
        {
            Validator.ThrowIfNullOrWhiteSpace(name);
            this.AddDomainEvent(new ContentNameChangedEvent(this, Name, name));
            Name = name;
        }
    }
}
