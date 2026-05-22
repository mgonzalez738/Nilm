using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using MQTTnet;
using System.Text;
using System.Text.Json;

namespace EnergyMeterWorkerService;

/// <summary>
/// Representa un servicio en segundo plano encargado de escuchar mensajes desde un broker MQTT, 
/// procesar los payloads y almacenar las métricas resultantes en InfluxDB.
/// Gestiona simultáneamente los datos de medidores de energía y enchufes inteligentes.
/// </summary>
public class MqttIngestionWorker : BackgroundService
{
    private readonly ILogger<MqttIngestionWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly InfluxDBClient _influxClient;
    private readonly WriteApiAsync _writeApiAsync;
    private readonly IMqttClient _mqttIngestionClient;

    // Tópicos base utilizados para identificar la procedencia de los mensajes
    private readonly string _meterTopicRoot;
    private readonly string _plugTopicRoot;

    private readonly string _ingestHost;
    private readonly int _ingestPort;
    private readonly string _influxBucket;
    private readonly string _influxOrg;

    // Mantiene una instancia estática de las opciones de serialización para optimizar el rendimiento
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Inicializa una nueva instancia del worker, configurando las dependencias y conexiones necesarias.
    /// </summary>
    public MqttIngestionWorker(ILogger<MqttIngestionWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Recupera la configuración de los tópicos para diferenciar los dispositivos
        _meterTopicRoot = _configuration["MqttSettings:MeterTopic"] ?? "energy-meter";
        _plugTopicRoot = _configuration["MqttSettings:PlugTopic"] ?? "smart-plug";

        _ingestHost = _configuration["MqttSettings:IngestionBroker:Host"] ?? "localhost";
        _ingestPort = _configuration.GetValue<int>("MqttSettings:IngestionBroker:Port");

        string influxUrl = _configuration["InfluxDbSettings:Url"] ?? "http://localhost:8086";
        string influxToken = _configuration["InfluxDbSettings:Token"] ?? string.Empty;
        _influxBucket = _configuration["InfluxDbSettings:Bucket"] ?? "default";
        _influxOrg = _configuration["InfluxDbSettings:Org"] ?? "default";

        var factory = new MqttClientFactory();
        _mqttIngestionClient = factory.CreateMqttClient();

        _influxClient = new InfluxDBClient(influxUrl, influxToken);
        _writeApiAsync = _influxClient.GetWriteApiAsync();
    }

    /// <summary>
    /// Ejecuta el ciclo de vida principal del servicio, conectando al broker MQTT y estableciendo el bucle de reconexión.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ingestMqttOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_ingestHost, _ingestPort)
            .WithClientId("Unified_IngestionWorker")
            .WithCleanSession()
            .Build();

        // Configura el manejador de eventos para procesar los mensajes MQTT entrantes
        _mqttIngestionClient.ApplicationMessageReceivedAsync += async e =>
        {
            // Valida que el mensaje contenga un payload procesable
            if (e.ApplicationMessage?.Payload == null || e.ApplicationMessage.Payload.Length == 0)
                return;

            string topic = e.ApplicationMessage.Topic;
            var rawPayload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            try
            {
                // Deriva el mensaje al método de procesamiento correspondiente según su tópico
                if (topic.StartsWith(_meterTopicRoot))
                {
                    await ProcessEnergyMeterPayload(topic, rawPayload, stoppingToken);
                }
                else if (topic.StartsWith(_plugTopicRoot))
                {
                    await ProcessSmartPlugPayload(topic, rawPayload, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enrutar o procesar el payload del tópico: {Topic}", topic);
            }
        };

        // Registra advertencias cuando se pierde la conexión con el broker
        _mqttIngestionClient.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("Conexión perdida con el broker MQTT. Razón: {Reason}", e.Reason);
            await Task.CompletedTask;
        };

        _logger.LogInformation("Iniciando MqttIngestionWorker unificado...");

        try
        {
            await _mqttIngestionClient.ConnectAsync(ingestMqttOptions, stoppingToken);

            // Genera la suscripción múltiple para escuchar ambos comodines al mismo tiempo
            var mqttFactory = new MqttClientFactory();
            var subscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"{_meterTopicRoot}/+/data"))
                .WithTopicFilter(f => f.WithTopic($"{_plugTopicRoot}/+/data"))
                .Build();

            await _mqttIngestionClient.SubscribeAsync(subscribeOptions, stoppingToken);
            _logger.LogInformation("Suscrito exitosamente a métricas principales y telemetría de enchufes.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fallo inicial de conexión en el worker unificado.");
        }

        // Mantiene el servicio activo e intenta reconectar si la conexión se pierde
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_mqttIngestionClient.IsConnected)
            {
                try
                {
                    await _mqttIngestionClient.ConnectAsync(ingestMqttOptions, stoppingToken);
                    var mqttFactory = new MqttClientFactory();
                    var subscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic($"{_meterTopicRoot}/+/data"))
                        .WithTopicFilter(f => f.WithTopic($"{_plugTopicRoot}/+/data"))
                        .Build();
                    await _mqttIngestionClient.SubscribeAsync(subscribeOptions, stoppingToken);
                }
                catch
                {
                    // Permite que el bucle continúe reintentando silenciosamente
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        // Realiza una desconexión limpia al detener el servicio
        if (_mqttIngestionClient.IsConnected)
        {
            await _mqttIngestionClient.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        }
    }

    /// <summary>
    /// Deserializa, estructura y almacena los datos provenientes de un medidor de energía principal.
    /// </summary>
    private async Task ProcessEnergyMeterPayload(string topic, string rawPayload, CancellationToken stoppingToken)
    {
        var data = JsonSerializer.Deserialize<EnergyMeterPayload>(rawPayload, _jsonOptions);
        if (data == null) return;

        // Extrae el identificador del dispositivo desde el tópico
        string meterId = topic.Split('/')[1];

        // Construye el punto de datos para InfluxDB con todos los campos de medición
        var point = PointData.Measurement("energy-meters")
            .Tag("deviceId", meterId)
            .Field("vRms", data.VRms)
            .Field("iRms", data.IRms)
            .Field("freq", data.Freq)
            .Field("temp", data.Temp)
            .Field("pActive", data.PActive)
            .Field("pActiveF", data.PActiveF)
            .Field("pActiveH", data.PActiveH)
            .Field("qReactive", data.QReactive)
            .Field("sApparent", data.SApparent)
            .Field("pf", data.Pf)
            .Field("phase", data.Phase)
            .Field("thdV", data.ThdV)
            .Field("thdI", data.ThdI)
            .Field("totalActiveForward", data.TotalActiveForward)
            .Field("totalActiveReverse", data.TotalActiveReverse)
            .Field("totalReactiveForward", data.TotalReactiveForward)
            .Field("totalReactiveReverse", data.TotalReactiveReverse)
            .Field("vRmsFund", data.VRmsFund)
            .Field("iRmsFund", data.IRmsFund);

        // Agrega dinámicamente los valores de armónicos de tensión si están presentes
        if (data.DftV != null)
        {
            for (int i = 0; i < data.DftV.Count; i++)
            {
                point = point.Field($"dftV{i}", data.DftV[i]);
            }
        }

        // Agrega dinámicamente los valores de armónicos de corriente si están presentes
        if (data.DftI != null)
        {
            for (int i = 0; i < data.DftI.Count; i++)
            {
                point = point.Field($"dftI{i}", data.DftI[i]);
            }
        }

        // Asegura una marca de tiempo de alta precisión, utilizando el tiempo actual como respaldo
        DateTime deviceTime = DateTime.TryParse(data.Timestamp, out var dt) ? dt : DateTime.UtcNow;
        point = point.Timestamp(deviceTime, WritePrecision.Ns);

        // Envía el punto de datos estructurado a InfluxDB
        await _writeApiAsync.WritePointAsync(point, _influxBucket, _influxOrg, stoppingToken);
    }

    /// <summary>
    /// Deserializa, estructura y almacena los datos provenientes de un enchufe inteligente.
    /// </summary>
    private async Task ProcessSmartPlugPayload(string topic, string rawPayload, CancellationToken stoppingToken)
    {
        var data = JsonSerializer.Deserialize<WSmartPlugPayload>(rawPayload, _jsonOptions);
        if (data == null) return;

        // Extrae el identificador del dispositivo desde el tópico
        string plugId = topic.Split('/')[1];

        // Construye el punto de datos para InfluxDB con los parámetros del enchufe
        var point = PointData.Measurement("smart-plugs")
            .Tag("deviceId", plugId)
            .Field("voltage", data.Voltage)
            .Field("current", data.Current)
            .Field("power", data.Power)
            .Field("status", (int)data.Status);

        // Asegura una marca de tiempo en formato universal
        DateTime deviceTime = DateTime.TryParse(data.Timestamp, out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow;
        point = point.Timestamp(deviceTime, WritePrecision.Ns);

        // Envía el punto de datos estructurado a InfluxDB
        await _writeApiAsync.WritePointAsync(point, _influxBucket, _influxOrg, stoppingToken);
    }

    /// <summary>
    /// Libera los recursos no administrados y finaliza las conexiones de cliente.
    /// </summary>
    public override void Dispose()
    {
        _mqttIngestionClient?.Dispose();
        _influxClient?.Dispose();
        base.Dispose();
    }
}