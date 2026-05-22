namespace EnergyMeterWorkerService;

/// <summary>
/// Representa el modelo de datos para el payload recibido desde el medidor de energía principal.
/// Se utiliza para deserializar los mensajes JSON provenientes de MQTT antes de su procesamiento.
/// </summary>
public class EnergyMeterPayload
{
    /// <summary>
    /// Indica la marca de tiempo original en la que el dispositivo registró la medición.
    /// </summary>
    public string? Timestamp { get; set; }

    /// <summary>
    /// Representa el valor eficaz de la tensión (Voltaje RMS).
    /// </summary>
    public double VRms { get; set; }

    /// <summary>
    /// Representa el valor eficaz de la corriente (Corriente RMS).
    /// </summary>
    public double IRms { get; set; }

    /// <summary>
    /// Indica la frecuencia de la red eléctrica medida en hercios.
    /// </summary>
    public double Freq { get; set; }

    /// <summary>
    /// Indica la temperatura de operación del circuito integrado del medidor.
    /// </summary>
    public double Temp { get; set; }

    /// <summary>
    /// Representa la potencia activa total consumida o generada.
    /// </summary>
    public double PActive { get; set; }

    /// <summary>
    /// Representa la potencia activa fundamental, excluyendo los armónicos.
    /// </summary>
    public double PActiveF { get; set; }

    /// <summary>
    /// Representa la potencia activa armónica, derivada de la distorsión en la red.
    /// </summary>
    public double PActiveH { get; set; }

    /// <summary>
    /// Representa la potencia reactiva presente en el sistema.
    /// </summary>
    public double QReactive { get; set; }

    /// <summary>
    /// Representa la potencia aparente total.
    /// </summary>
    public double SApparent { get; set; }

    /// <summary>
    /// Indica el factor de potencia, mostrando la relación entre la potencia activa y la aparente.
    /// </summary>
    public double Pf { get; set; }

    /// <summary>
    /// Indica el ángulo de fase entre las ondas de tensión y corriente.
    /// </summary>
    public double Phase { get; set; }

    /// <summary>
    /// Representa la tasa de distorsión armónica total para la onda de tensión.
    /// </summary>
    public double ThdV { get; set; }

    /// <summary>
    /// Representa la tasa de distorsión armónica total para la onda de corriente.
    /// </summary>
    public double ThdI { get; set; }

    /// <summary>
    /// Acumula el total de energía activa consumida desde la red eléctrica.
    /// </summary>
    public double TotalActiveForward { get; set; }

    /// <summary>
    /// Acumula el total de energía activa inyectada hacia la red eléctrica.
    /// </summary>
    public double TotalActiveReverse { get; set; }

    /// <summary>
    /// Acumula el total de energía reactiva en el espectro inductivo.
    /// </summary>
    public double TotalReactiveForward { get; set; }

    /// <summary>
    /// Acumula el total de energía reactiva en el espectro capacitivo.
    /// </summary>
    public double TotalReactiveReverse { get; set; }

    /// <summary>
    /// Representa el valor eficaz de la tensión considerando únicamente la frecuencia fundamental.
    /// </summary>
    public double VRmsFund { get; set; }

    /// <summary>
    /// Representa el valor eficaz de la corriente considerando únicamente la frecuencia fundamental.
    /// </summary>
    public double IRmsFund { get; set; }

    /// <summary>
    /// Contiene la lista de componentes armónicos de tensión calculados mediante la Transformada Discreta de Fourier.
    /// </summary>
    public List<double>? DftV { get; set; }

    /// <summary>
    /// Contiene la lista de componentes armónicos de corriente calculados mediante la Transformada Discreta de Fourier.
    /// </summary>
    public List<double>? DftI { get; set; }
}
