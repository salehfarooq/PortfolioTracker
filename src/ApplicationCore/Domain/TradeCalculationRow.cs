namespace ApplicationCore.Domain;

public readonly record struct TradeCalculationRow(int SecurityId, string OrderType, decimal Quantity, decimal Price);

