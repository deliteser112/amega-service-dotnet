using Application.DTOs;
using Application.UseCases;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstrumentsController(
    GetInstrumentsUseCase getInstrumentsUseCase,
    ILogger<InstrumentsController> logger) : ControllerBase
{
    /// <summary>
    /// Get a list of all available financial instruments
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InstrumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InstrumentDto>>> GetInstruments(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("GET /api/instruments - Request received");
            var instruments = await getInstrumentsUseCase.ExecuteAsync(cancellationToken);
            return Ok(instruments);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving instruments");
            return StatusCode(500, new { error = "An error occurred while retrieving instruments" });
        }
    }
}

