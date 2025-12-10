using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class Order
{
    public int OrderID { get; set; }

    public int AccountID { get; set; }

    public int SecurityID { get; set; }

    public string OrderType { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal Price { get; set; }

    public string Status { get; set; } = null!;

    public DateTime OrderDate { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Security Security { get; set; } = null!;

    public virtual ICollection<Trade> Trades { get; set; } = new List<Trade>();
}
