using Core.Entities;
using Core.Interfaces.Repositories;

namespace Application.Repositories;

public class InstrumentRepository : IInstrumentRepository
{
    // In-memory repository for demo purposes
    // In production, this would connect to a database
    private static readonly List<FinancialInstrument> _instruments = new()
    {
        new FinancialInstrument { Symbol = "EURUSD", Name = "Euro/US Dollar", Type = InstrumentType.Forex },
        new FinancialInstrument { Symbol = "USDJPY", Name = "US Dollar/Japanese Yen", Type = InstrumentType.Forex },
        new FinancialInstrument { Symbol = "BTCUSD", Name = "Bitcoin/US Dollar", Type = InstrumentType.Crypto }
    };

    public Task<IEnumerable<FinancialInstrument>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<FinancialInstrument>>(_instruments);
    }

    public Task<FinancialInstrument?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var instrument = _instruments.FirstOrDefault(i => 
            i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(instrument);
    }
}

