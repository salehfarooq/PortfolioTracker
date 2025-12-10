using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class v_SecurityLatestPrice
{
    public int SecurityID { get; set; }

    public DateOnly LatestPriceDate { get; set; }

    public decimal LatestClosePrice { get; set; }
}
