namespace EnergyMetersService.Domain.Entities
{
    public abstract record TimeSeries
    {
        //  Metadata

        public required Metadata Metadata { get; init; }

        // Timestamp

        private readonly DateTime _timestamp;

        public required DateTime Timestamp
        {
            get => _timestamp;
            init
            {
                var utcTime = value.ToUniversalTime();

                _timestamp = new DateTime(
                    utcTime.Year,
                    utcTime.Month,
                    utcTime.Day,
                    utcTime.Hour,
                    utcTime.Minute,
                    utcTime.Second,
                    DateTimeKind.Utc);
            }
        }
    }

    public record Metadata
    {
        public required string SensorId { get; init; }
    }
}