using Application.DTOs;
using Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.UseCases;

public class GetInstrumentsUseCase(
    IInstrumentRepository instrumentRepository,
    ILogger<GetInstrumentsUseCase> logger)
{
    public async Task<IEnumerable<InstrumentDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching all available instruments");
        
        var instruments = await instrumentRepository.GetAllAsync(cancellationToken);
        
        var result = instruments.Select(i => new InstrumentDto
        {
            Symbol = i.Symbol,
            Name = i.Name,
            Type = i.Type.ToString()
        }).ToList();

        logger.LogInformation("Retrieved {Count} instruments", result.Count);
        
        return result;
    }
}

