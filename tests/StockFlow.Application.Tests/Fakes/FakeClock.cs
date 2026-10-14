using StockFlow.Application.Abstractions.Time;

namespace StockFlow.Application.Tests.Fakes
{
    public sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } =
            new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }
}