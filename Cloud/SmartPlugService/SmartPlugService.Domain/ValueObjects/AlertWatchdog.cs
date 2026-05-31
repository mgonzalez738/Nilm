namespace SmartPlugService.Domain.ValueObjects;

public record AlertWatchdog (
   string AlertGuid,
   string Property,
   DateTime Timestamp,
   int Timeout,
   int TimeoutLimit);