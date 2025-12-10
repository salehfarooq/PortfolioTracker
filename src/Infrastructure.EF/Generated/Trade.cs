using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class Trade
{
    public int TradeID { get; set; }

    public int OrderID { get; set; }

    public DateTime TradeDate { get; set; }

    public decimal Quantity { get; set; }

    public decimal Price { get; set; }

    public decimal Amount { get; set; }

    public decimal Fees { get; set; }

    public virtual Order Order { get; set; } = null!;
}
