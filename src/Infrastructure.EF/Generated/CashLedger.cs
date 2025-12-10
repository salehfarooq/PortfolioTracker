using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class CashLedger
{
    public int LedgerID { get; set; }

    public int AccountID { get; set; }

    public DateTime TxnDate { get; set; }

    public decimal Amount { get; set; }

    public string Type { get; set; } = null!;

    public string? Reference { get; set; }

    public virtual Account Account { get; set; } = null!;
}
