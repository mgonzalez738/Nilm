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

    private readonly string _meterTopicRoot;
    private readonly string _plugTopicRoot;

    private readonly string _ingestHost;
    private readonly int _ingestPort;
    private readonly string _influxBucket;
    private readonly string _influxOrg;

    // Solo se usa para el medidor de energía ahora
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MqttIngestionWorker(ILogger<MqttIngestionWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ingestMqttOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_ingestHost, _ingestPort)
            .WithClientId("Unified_IngestionWorker")
            .WithCleanSession()
            .Build();

        _mqttIngestionClient.ApplicationMessageReceivedAsync += async e =>
        {
            if (e.ApplicationMessage?.Payload == null || e.ApplicationMessage.Payload.Length == 0)
                return;

            string topic = e.ApplicationMessage.Topic;
            var rawPayload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            try
            {
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

        _mqttIngestionClient.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("Conexión perdida con el broker MQTT. Razón: {Reason}", e.Reason);
            await Task.CompletedTask;
        };

        _logger.LogInformation("Iniciando MqttIngestionWorker unificado...");

        try
        {
            await _mqttIngestionClient.ConnectAsync(ingestMqttOptions, stoppingToken);

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
                    // Reintento silencioso
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        if (_mqttIngestionClient.IsConnected)
        {
            await _mqttIngestionClient.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        }
    }

    private async Task ProcessEnergyMeterPayload(string topic, string rawPayload, CancellationToken stoppingToken)
    {
        var data = JsonSerializer.Deserialize<EnergyMeterPayload>(rawPayload, _jsonOptions);
        if (data == null) return;

        string meterId = topic.Split('/')[1];

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

        if (data.DftV != null)
        {
            for (int i = 0; i < data.DftV.Count; i++) point = point.Field($"dftV{i}", data.DftV[i]);
        }

        if (data.DftI != null)
        {
            for (int i = 0; i < data.DftI.Count; i++) point = point.Field($"dftI{i}", data.DftI[i]);
        }

        DateTime deviceTime = DateTime.TryParse(data.Timestamp, out var dt) ? dt : DateTime.UtcNow;
        point = point.Timestamp(deviceTime, WritePrecision.Ns);

        await _writeApiAsync.WritePointAsync(point, _influxBucket, _influxOrg, stoppingToken);
    }

    private async Task ProcessSmartPlugPayload(string topic, string rawPayload, CancellationToken stoppingToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            string plugId = topic.Split('/')[1];

            var point = PointData.Measurement("smart-plugs")
                .Tag("deviceId", plugId);

            // Mapeo dinámico directo
            if (root.TryGetProperty("voltage", out var v)) point = point.Field("voltage", v.GetDouble());
            if (root.TryGetProperty("current", out var c)) point = point.Field("current", c.GetDouble());
            if (root.TryGetProperty("activePower", out var ap)) point = point.Field("activePower", ap.GetDouble());
            if (root.TryGetProperty("reactivePower", out var rp)) point = point.Field("reactivePower", rp.GetDouble());

            // Aplanamiento del array de estados a status0, status1, etc.
            if (root.TryGetProperty("status", out var statusArray) && statusArray.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var status in statusArray.EnumerateArray())
                {
                    point = point.Field($"status{index}", status.GetInt32());
                    index++;
                }
            }

            DateTime deviceTime = DateTime.UtcNow;
            if (root.TryGetProperty("timestamp", out var ts) && DateTime.TryParse(ts.GetString(), out var dt))
            {
                deviceTime = dt.ToUniversalTime();
            }

            point = point.Timestamp(deviceTime, WritePrecision.Ns);

            await _writeApiAsync.WritePointAsync(point, _influxBucket, _influxOrg, stoppingToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error al deserializar dinámicamente el payload del enchufe en el tópico {Topic}", topic);
        }
    }

    public override void Dispose()
    {
        _mqttIngestionClient?.Dispose();
        _influxClient?.Dispose();
        base.Dispose();
    }
}