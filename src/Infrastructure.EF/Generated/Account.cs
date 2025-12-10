using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class Account
{
    public int AccountID { get; set; }

    public int UserID { get; set; }

    public string AccountType { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public DateOnly CreatedDate { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<CashLedger> CashLedgers { get; set; } = new List<CashLedger>();

    public virtual ICollection<Holding> Holdings { get; set; } = new List<Holding>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual User User { get; set; } = null!;
}
