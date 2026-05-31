namespace SmartPlugService.Domain.ValueObjects;

public record AlertLimit
(
    string AlertGuid,
    string Property,
    DateTime Timestamp,
    double Value,
    string Unit,
    double LimitLow,
    double LimitHigh,
    double LimitHysteresis);