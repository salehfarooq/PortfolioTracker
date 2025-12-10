using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class Holding
{
    public int HoldingID { get; set; }

    public int AccountID { get; set; }

    public int SecurityID { get; set; }

    public decimal Quantity { get; set; }

    public decimal AvgCost { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Security Security { get; set; } = null!;
}
