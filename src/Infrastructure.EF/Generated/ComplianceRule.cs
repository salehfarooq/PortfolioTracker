using System;
using System.Collections.Generic;

namespace Infrastructure.EF.Generated;

public partial class ComplianceRule
{
    public int RuleID { get; set; }

    public string RuleType { get; set; } = null!;

    public decimal LimitValue { get; set; }

    public string? Description { get; set; }
}
