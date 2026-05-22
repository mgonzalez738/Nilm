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
    public double Power { get; set; }

    /// <summary>
    /// Indica el estado operativo del relé interno del enchufe, señalando si la salida de corriente está activada o desactivada.
    /// </summary>
    public double Status { get; set; }
}
