using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class Security
{
    public int SecurityID { get; set; }

    public string Ticker { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public string? Sector { get; set; }

    public string? ListedIn { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Holding> Holdings { get; set; } = new List<Holding>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}
