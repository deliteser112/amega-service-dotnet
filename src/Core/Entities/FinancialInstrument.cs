namespace Core.Entities;

public class FinancialInstrument
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InstrumentType Type { get; set; }
}

public enum InstrumentType
{
    Forex,
    Crypto
}

