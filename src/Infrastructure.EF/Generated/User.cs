using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class User
{
    public int UserID { get; set; }

    public string Username { get; set; } = null!;

    public byte[] PasswordHash { get; set; } = null!;

    public byte[] PasswordSalt { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public DateOnly JoinDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
}
