namespace EnergyMeterWorkerService;

/// <summary>
/// Representa el modelo de datos para el payload recibido desde los enchufes inteligentes.
/// Se utiliza para deserializar los mensajes de telemetría provenientes de MQTT antes de su almacenamiento.
/// </summary>
public class WSmartPlugPayload
{
    /// <summary>
    /// Indica la marca de tiempo original en la que el enchufe inteligente registró la medición.
    /// </summary>
    public string? Timestamp { get; set; }

    /// <summary>
    /// Representa el nivel de voltaje medido por el dispositivo en la red eléctrica.
    /// </summary>
    public double Voltage { get; set; }

    /// <summary>
    /// Representa la corriente eléctrica que fluye a través del enchufe hacia la carga conectada.
    /// </summary>
    public double Current { get; set; }

    /// <summary>
    /// Indica la potencia activa consumida instantáneamente por el dispositivo conectado al enchufe.
    /// </summary>
    public double ActivePower { get; set; }

    /// <summary>
    /// Indica la potencia activa consumida instantáneamente por el dispositivo conectado al enchufe.
    /// </summary>
    public double ReactivePower { get; set; }

    /// <summary>
    /// Indica el estado operativo de las cargas conectadas al enchufe inteligente
    /// </summary>
    public List<int> Status { get; set; } = [];
}
