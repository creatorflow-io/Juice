using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using Finbuckle.MultiTenant;
using Juice.Domain;
using Juice.MultiTenant.Domain.Events;
using Juice.MultiTenant.Shared.Enums;
using MediatR;

namespace Juice.MultiTenant.Domain.AggregatesModel.TenantAggregate
{
    public class Tenant : DynamicEntity<string>, IAggregrateRoot<INotification>,
        ITenant, ITenantInfo
    {
        public string? Identifier { get; set; }
        public string? ConnectionString { get; set; }

        public TenantStatus Status { get; private set; }

        [NotMapped]
        public IList<INotification> DomainEvents { get; } = new List<INotification>();

        /// <summary>
        /// You can decide to use userid or username for the tenant owner.
        /// </summary>
        public string? OwnerUser { get; private set; }

        #region methods

        public virtual void Update(string name, string identifier, string? connectionString)
        {
            Name = name;
            Identifier = identifier;
            ConnectionString = connectionString;
            FirePropertiesChanged();
        }

        public virtual void RequestApproval()
        {
            if (Status == TenantStatus.New)
            {
                Status = TenantStatus.PendingApproval;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in New status can be requested for approval.");
            }
        }

        public virtual void Approved()
        {
            if (Status == TenantStatus.PendingApproval)
            {
                Status = TenantStatus.Approved;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in PendingApproval status can be approved.");
            }
        }

        public virtual void Rejected()
        {
            if (Status == TenantStatus.PendingApproval)
            {
                Status = TenantStatus.Rejected;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in PendingApproval status can be rejected.");
            }
        }

        public virtual void Initializing()
        {
            if (Status == TenantStatus.New || Status == TenantStatus.Approved)
            {
                Status = TenantStatus.Initializing;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in New or Approved status can be initializing.");
            }
        }

        public virtual void Initialized()
        {
            if (Status == TenantStatus.Initializing)
            {
                Status = TenantStatus.Initialized;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in Initializing status can be initialized.");
            }
        }

        public virtual void Suspend()
        {
            if (Status == TenantStatus.Active
                || Status == TenantStatus.Inactive)
            {
                Status = TenantStatus.Suspended;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in Active or Inactive status can be suspended.");
            }
        }

        public virtual void Abandon()
        {
            Status = TenantStatus.Abandoned;
            DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
        }

        public virtual void RequestActivate()
        {
            if (Status == TenantStatus.New || Status == TenantStatus.Approved
                 || Status == TenantStatus.Initialized)
            {
                Status = TenantStatus.PendingToActive;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in New, Approved or Initialized status can be requested to activate.");
            }
        }

        public virtual void Activate()
        {
            if (Status == TenantStatus.New || Status == TenantStatus.Approved
                || Status == TenantStatus.Initialized || Status == TenantStatus.PendingToActive)
            {
                Status = TenantStatus.Active;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in New, Approved, Initialized or PendingToActive status can be activated.");
            }
        }

        public virtual void Reactivate()
        {
            if (Status == TenantStatus.Inactive || Status == TenantStatus.Suspended)
            {
                Status = TenantStatus.Active;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in Inactive or Suspended status can be activated.");
            }
        }

        public virtual void Deactivate()
        {
            if (Status == TenantStatus.Active)
            {
                Status = TenantStatus.Inactive;
                DomainEvents.Add(new TenantStatusChangedDomainEvent(Id, Identifier, Status));
            }
            else
            {
                throw new InvalidOperationException("Only tenants in Active status can be deactivated.");
            }
        }

        public virtual void PreDeleteCheck()
        {
            if (Status != TenantStatus.Suspended && Status != TenantStatus.Inactive)
            {
                throw new InvalidOperationException("Only tenants in Suspended status can be deleted.");
            }
        }

        public virtual void UpdateProperties(Dictionary<string, string?> properties)
        {
            if (properties.Any())
            {
                foreach (var property in properties)
                {
                    SetProperty(property.Value, property.Key);
                }
                FirePropertiesChanged();
            }
        }

        public virtual void FirePropertiesChanged()
        {
            if (DomainEvents.Any(x => x is TenantPropertiesChangedDomainEvent))
                DomainEvents.Add(new TenantPropertiesChangedDomainEvent(Id, Identifier));
        }

        public virtual void SetOwner(string owner)
        {
            this.AddDomainEvent(new TenantOwnerChangedDomainEvent(Id, Identifier, OwnerUser, owner));

            OwnerUser = owner;
        }

        public virtual void ClearOwner()
        {
            OwnerUser = null;

            this.AddDomainEvent(new TenantOwnerChangedDomainEvent(Id, Identifier, OwnerUser, null));
        }

        public virtual void TransferOwner(string? from, string to)
        {
            if (OwnerUser == from)
            {
                OwnerUser = to;
                this.AddDomainEvent(new TenantOwnerChangedDomainEvent(Id, Identifier, OwnerUser, to));
            }
            else
            {
                throw new InvalidOperationException("Only the current owner can transfer ownership.");
            }
        }
        public override void SetProperty(object? value, [CallerMemberName] string? name = null)
        {
            base.SetProperty(value, name);
            FirePropertiesChanged();
        }
        #endregion
    }
}
