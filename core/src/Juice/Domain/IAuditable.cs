﻿namespace Juice.Domain
{
    public interface IAuditable
    {
        string? CreatedUser { get; }
        string? ModifiedUser { get; }
        DateTimeOffset CreatedDate { get; }
        DateTimeOffset? ModifiedDate { get; }
    }
}
