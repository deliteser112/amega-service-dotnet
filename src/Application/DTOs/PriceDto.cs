namespace Application.DTOs;

public class PriceDto
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime Timestamp { get; set; }
}

