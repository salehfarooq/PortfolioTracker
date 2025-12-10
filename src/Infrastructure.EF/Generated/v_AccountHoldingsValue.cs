using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class v_AccountHoldingsValue
{
    public int AccountID { get; set; }

    public string Username { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public int SecurityID { get; set; }

    public string Ticker { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal AvgCost { get; set; }

    public DateOnly LatestPriceDate { get; set; }

    public decimal LatestClosePrice { get; set; }

    public decimal? MarketValue { get; set; }

    public decimal? UnrealizedPL { get; set; }
}
