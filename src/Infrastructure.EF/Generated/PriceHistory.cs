using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class PriceHistory
{
    public int SecurityID { get; set; }

    public DateOnly PriceDate { get; set; }

    public decimal? OpenPrice { get; set; }

    public decimal? HighPrice { get; set; }

    public decimal? LowPrice { get; set; }

    public decimal ClosePrice { get; set; }

    public long? Volume { get; set; }

    public decimal? ChangePct { get; set; }

    public virtual Security Security { get; set; } = null!;
}
