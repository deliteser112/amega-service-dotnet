using Core.Entities;

namespace Core.Interfaces.Repositories;

public interface IInstrumentRepository
{
    Task<IEnumerable<FinancialInstrument>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FinancialInstrument?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);
}

