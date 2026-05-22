using InfluxDB.Client;
using MQTTnet;
using System.Text.Json;
using System.Collections.Concurrent;

namespace EnergyMeterWorkerService
{
    /// <summary>
    /// Representa un servicio en segundo plano encargado de consultar periódicamente métricas desde InfluxDB 
    /// y publicarlas agrupadas por lotes hacia un broker MQTT de destino.
    /// Gestiona la publicación tanto para medidores de energía como para enchufes inteligentes, 
    /// manteniendo el estado temporal de los envíos para evitar datos duplicados.
    /// </summary>
    public class MqttPublisherWorker : BackgroundService
    {
        private readonly ILogger<MqttPublisherWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly InfluxDBClient _influxClient;
        private readonly IMqttClient _mqttPublisherClient;

        // Variables de configuración de red y base de datos
        private readonly string _pubHost;
        private readonly int _pubPort;
        private readonly string _pubMeterBaseTopic;
        private readonly string _pubPlugBaseTopic;
        private readonly int _publishIntervalMinutes;
        private readonly string _influxBucket;
        private readonly string _influxOrg;

        // Gestión de estado en disco y caché en memoria para rastrear el último registro enviado
        private readonly string _stateFilePath = Path.Combine(AppContext.BaseDirectory, "publisher_state.json");
        private ConcurrentDictionary<string, DateTime> _lastSentTimestamps = new();

        // Mantiene una instancia estática de las opciones de serialización para optimizar el rendimiento
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Inicializa una nueva instancia del servicio publicador, configurando las dependencias, 
        /// estableciendo los parámetros de conexión y recuperando el estado de envíos previo.
        /// </summary>
        public MqttPublisherWorker(ILogger<MqttPublisherWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Recupera las bases de tópicos para clasificar el destino de ambos dispositivos
            _pubMeterBaseTopic = _configuration["MqttSettings:MeterTopic"] ?? "energy-meter";
            _pubPlugBaseTopic = _configuration["MqttSettings:PlugTopic"] ?? "smart-plug";

            _pubHost = _configuration["MqttSettings:PublisherBroker:Host"] ?? "localhost";
            _pubPort = _configuration.GetValue<int>("MqttSettings:PublisherBroker:Port");
            _publishIntervalMinutes = _configuration.GetValue<int>("MqttSettings:PublisherBroker:PublishIntervalMinutes", 5);

            string influxUrl = _configuration["InfluxDbSettings:Url"] ?? "http://localhost:8086";
            string influxToken = _configuration["InfluxDbSettings:Token"] ?? string.Empty;
            _influxBucket = _configuration["InfluxDbSettings:Bucket"] ?? "default";
            _influxOrg = _configuration["InfluxDbSettings:Org"] ?? "default";

            var factory = new MqttClientFactory();
            _mqttPublisherClient = factory.CreateMqttClient();
            _influxClient = new InfluxDBClient(influxUrl, influxToken);

            LoadStateFromDisk();
        }

        /// <summary>
        /// Ejecuta el ciclo de vida principal del servicio, conectando al broker MQTT de destino 
        /// e iterando en intervalos regulares para extraer, procesar y publicar los datos.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var pubMqttOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_pubHost, _pubPort)
                .WithClientId("PublisherWorker_Sender")
                .WithCleanSession()
                .Build();

            _logger.LogInformation("Iniciando MqttPublisherWorker. Conectando al Broker Destino en {Host}:{Port}...", _pubHost, _pubPort);

            try
            {
                await _mqttPublisherClient.ConnectAsync(pubMqttOptions, stoppingToken);
                _logger.LogInformation("Conectado al Broker de Publicación exitosamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo inicial al conectar con el Broker de Publicación.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan delayToNextRun = GetDelayToNextInterval(_publishIntervalMinutes);

                _logger.LogInformation("Próximo envío programado en {Minutos}m {Segundos}s...",
                    delayToNextRun.Minutes, delayToNextRun.Seconds);

                await Task.Delay(delayToNextRun, stoppingToken);

                if (!_mqttPublisherClient.IsConnected)
                {
                    _logger.LogWarning("Broker de Publicación desconectado. Intentando reconexión antes de publicar...");
                    try { await _mqttPublisherClient.ConnectAsync(pubMqttOptions, stoppingToken); }
                    catch { continue; }
                }

                // Publica de forma independiente los datos de medidores y de enchufes
                bool metersChanged = await PublishEnergyMetersAsync(stoppingToken);
                bool plugsChanged = await PublishSmartPlugsAsync(stoppingToken);

                // Persiste el estado solo si hubo datos nuevos enviados
                if (metersChanged || plugsChanged)
                {
                    SaveStateToDisk();
                }
            }

            if (_mqttPublisherClient.IsConnected)
            {
                await _mqttPublisherClient.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
            }
        }

        /// <summary>
        /// Consulta, reconstruye y publica los datos históricos recientes correspondientes a los medidores de energía.
        /// Retorna verdadero si se publicaron nuevos lotes, indicando un cambio de estado.
        /// </summary>
        private async Task<bool> PublishEnergyMetersAsync(CancellationToken stoppingToken)
        {
            bool stateChanged = false;
            try
            {
                // Limita la búsqueda temporal para optimizar la consulta a la base de datos
                DateTime queryStart = DateTime.UtcNow.AddMinutes(-(_publishIntervalMinutes * 2));

                string fluxQuery = $@"
                    import ""influxdata/influxdb/schema""
                    from(bucket: ""{_influxBucket}"")
                    |> range(start: {queryStart:yyyy-MM-ddTHH:mm:ss.fffZ})
                    |> filter(fn: (r) => r[""_measurement""] == ""energy-meters"")
                    |> schema.fieldsAsCols()";

                var queryApi = _influxClient.GetQueryApi();
                var tables = await queryApi.QueryAsync(fluxQuery, _influxOrg, stoppingToken);

                var groupedData = new Dictionary<string, List<Dictionary<string, object>>>();

                foreach (var table in tables)
                {
                    foreach (var record in table.Records)
                    {
                        var meterId = record.GetValueByKey("deviceId")?.ToString();
                        var recordTime = record.GetTimeInDateTime();

                        if (string.IsNullOrEmpty(meterId) || recordTime == null) continue;

                        string stateKey = $"meter_{meterId}";
                        _lastSentTimestamps.TryGetValue(stateKey, out DateTime lastSentForMeter);

                        // Omite registros que ya fueron enviados previamente
                        if (recordTime.Value <= lastSentForMeter.ToUniversalTime()) continue;

                        if (!groupedData.ContainsKey(meterId))
                            groupedData[meterId] = new List<Dictionary<string, object>>();

                        var dataPoint = new Dictionary<string, object> { ["timestamp"] = recordTime.Value.ToString("O") };

                        // Diccionarios temporales para reconstruir las listas de armónicos ordenadas
                        var tempDftV = new SortedDictionary<int, double>();
                        var tempDftI = new SortedDictionary<int, double>();

                        foreach (var row in record.Values)
                        {
                            if (row.Key.StartsWith("_") || row.Key == "table" || row.Key == "result" || row.Key == "deviceId") continue;

                            if (row.Key.StartsWith("dftV") && int.TryParse(row.Key.AsSpan(4), out int vIndex))
                            {
                                tempDftV[vIndex] = Convert.ToDouble(row.Value ?? 0);
                            }
                            else if (row.Key.StartsWith("dftI") && int.TryParse(row.Key.AsSpan(4), out int iIndex))
                            {
                                tempDftI[iIndex] = Convert.ToDouble(row.Value ?? 0);
                            }
                            else
                            {
                                dataPoint[row.Key] = row.Value!;
                            }
                        }

                        if (tempDftV.Count != 0) dataPoint["dftV"] = tempDftV.Values.ToList();
                        if (tempDftI.Count != 0) dataPoint["dftI"] = tempDftI.Values.ToList();

                        groupedData[meterId].Add(dataPoint);
                    }
                }

                foreach (var meterGroup in groupedData)
                {
                    string deviceId = meterGroup.Key;
                    var sortedRecords = meterGroup.Value.OrderBy(x => DateTime.Parse(x["timestamp"].ToString()!)).ToList();

                    if (!sortedRecords.Any()) continue;

                    string payloadJson = JsonSerializer.Serialize(new { batch = sortedRecords }, _jsonOptions);
                    string publishTopic = $"{_pubMeterBaseTopic}/{deviceId}/data";

                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic(publishTopic)
                        .WithPayload(payloadJson)
                        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build();

                    await _mqttPublisherClient.PublishAsync(message, stoppingToken);

                    DateTime newestRecordTime = DateTime.Parse(
                        sortedRecords.Last()["timestamp"].ToString()!, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

                    _lastSentTimestamps[$"meter_{deviceId}"] = newestRecordTime;
                    stateChanged = true;

                    _logger.LogInformation("[Meter] Lote de {Count} registros publicados para {deviceId}.", sortedRecords.Count, deviceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la extracción y publicación de medidores de energía.");
            }
            return stateChanged;
        }

        /// <summary>
        /// Consulta, agrupa y publica los datos históricos recientes correspondientes a los enchufes inteligentes.
        /// Retorna verdadero si se publicaron nuevos lotes, indicando un cambio de estado.
        /// </summary>
        private async Task<bool> PublishSmartPlugsAsync(CancellationToken stoppingToken)
        {
            bool stateChanged = false;
            try
            {
                DateTime queryStart = DateTime.UtcNow.AddMinutes(-(_publishIntervalMinutes * 2));

                string fluxQuery = $@"
                    import ""influxdata/influxdb/schema""
                    from(bucket: ""{_influxBucket}"")
                    |> range(start: {queryStart:yyyy-MM-ddTHH:mm:ss.fffZ})
                    |> filter(fn: (r) => r[""_measurement""] == ""smart-plugs"")
                    |> schema.fieldsAsCols()";

                var queryApi = _influxClient.GetQueryApi();
                var tables = await queryApi.QueryAsync(fluxQuery, _influxOrg, stoppingToken);

                var groupedData = new Dictionary<string, List<Dictionary<string, object>>>();

                foreach (var table in tables)
                {
                    foreach (var record in table.Records)
                    {
                        var plugId = record.GetValueByKey("deviceId")?.ToString();
                        var recordTime = record.GetTimeInDateTime();

                        if (string.IsNullOrEmpty(plugId) || recordTime == null) continue;

                        string stateKey = $"plug_{plugId}";
                        _lastSentTimestamps.TryGetValue(stateKey, out DateTime lastSentForPlug);

                        if (recordTime.Value <= lastSentForPlug.ToUniversalTime()) continue;

                        if (!groupedData.ContainsKey(plugId))
                            groupedData[plugId] = new List<Dictionary<string, object>>();

                        var dataPoint = new Dictionary<string, object> { ["timestamp"] = recordTime.Value.ToString("O") };

                        foreach (var row in record.Values)
                        {
                            // Ignora los metadatos internos propios de InfluxDB
                            if (row.Key.StartsWith("_") || row.Key == "table" || row.Key == "result" || row.Key == "deviceId") continue;
                            dataPoint[row.Key] = row.Value!;
                        }

                        groupedData[plugId].Add(dataPoint);
                    }
                }

                foreach (var plugGroup in groupedData)
                {
                    string deviceId = plugGroup.Key;
                    var sortedRecords = plugGroup.Value.OrderBy(x => DateTime.Parse(x["timestamp"].ToString()!)).ToList();

                    if (!sortedRecords.Any()) continue;

                    string payloadJson = JsonSerializer.Serialize(new { batch = sortedRecords }, _jsonOptions);
                    string publishTopic = $"{_pubPlugBaseTopic}/{deviceId}/data";

                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic(publishTopic)
                        .WithPayload(payloadJson)
                        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build();

                    await _mqttPublisherClient.PublishAsync(message, stoppingToken);

                    DateTime newestRecordTime = DateTime.Parse(
                        sortedRecords.Last()["timestamp"].ToString()!, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

                    _lastSentTimestamps[$"plug_{deviceId}"] = newestRecordTime;
                    stateChanged = true;

                    _logger.LogInformation("[Plug] Lote de {Count} registros publicados para {deviceId}.", sortedRecords.Count, deviceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la extracción y publicación de enchufes inteligentes.");
            }
            return stateChanged;
        }

        /// <summary>
        /// Calcula el tiempo de espera necesario para sincronizar la próxima ejecución 
        /// con el inicio exacto del siguiente intervalo configurado.
        /// </summary>
        private TimeSpan GetDelayToNextInterval(int intervalMinutes)
        {
            var now = DateTime.UtcNow;
            int minutesToNext = intervalMinutes - (now.Minute % intervalMinutes);

            DateTime nextBoundary = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc)
                .AddMinutes(minutesToNext);

            return nextBoundary - now;
        }

        /// <summary>
        /// Carga el estado temporal de publicación desde el disco, realizando una migración 
        /// automática de las claves heredadas para incorporar los nuevos prefijos de dispositivo.
        /// </summary>
        private void LoadStateFromDisk()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            if (!kvp.Key.StartsWith("meter_") && !kvp.Key.StartsWith("plug_"))
                                _lastSentTimestamps[$"meter_{kvp.Key}"] = kvp.Value;
                            else
                                _lastSentTimestamps[kvp.Key] = kvp.Value;
                        }

                        _logger.LogInformation("Estado de publicación cargado. Rastreando {Count} dispositivos.", _lastSentTimestamps.Count);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al leer el estado de publicación. Se iniciará desde los últimos minutos.");
            }
        }

        /// <summary>
        /// Guarda el estado actual de los últimos envíos en el disco para preservar 
        /// la continuidad operativa frente a posibles reinicios del servicio.
        /// </summary>
        private void SaveStateToDisk()
        {
            try
            {
                var dictToSave = new Dictionary<string, DateTime>(_lastSentTimestamps);
                var json = JsonSerializer.Serialize(dictToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo guardar el estado de publicación en el disco.");
            }
        }

        /// <summary>
        /// Libera los recursos no administrados y cierra las conexiones abiertas de red.
        /// </summary>
        public override void Dispose()
        {
            _mqttPublisherClient?.Dispose();
            _influxClient?.Dispose();
            base.Dispose();
        }
    }
}