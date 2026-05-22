namespace EnergyMeterWorkerService;

/// <summary>
/// Representa la clase principal de la aplicación, responsable de configurar y arrancar 
/// el entorno de alojamiento para los servicios en segundo plano.
/// </summary>
public class Program
{
    /// <summary>
    /// Actúa como el punto de entrada principal del programa. 
    /// Registra los workers necesarios e inicia el ciclo de vida de la aplicación.
    /// </summary>
    public static void Main(string[] args)
    {
        // Inicializa el constructor del host utilizando la configuración predeterminada del sistema
        var builder = Host.CreateApplicationBuilder(args);

        // Registra el servicio encargado de la ingesta de datos MQTT hacia InfluxDB
        builder.Services.AddHostedService<MqttIngestionWorker>();

        // Registra el servicio encargado de extraer los datos desde InfluxDB y publicarlos por lotes
        builder.Services.AddHostedService<MqttPublisherWorker>();

        // Construye la aplicación con los servicios registrados
        var host = builder.Build();

        // Inicia la ejecución del host y bloquea el hilo principal hasta que se reciba una señal de detención
        host.Run();
    }
}