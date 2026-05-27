/*
  El presente documento contiene el programa principal del firmware encargado de gestionar
  el monitor de energía. El sistema está diseñado exclusivamente para la adquisición
  y reporte continuo de telemetría vía red a alta frecuencia.
*/

#include <Arduino.h>
#include <ArduinoOTA.h>
#include <SPI.h>
#include <Wire.h>
#include <Hardware.h>
#include <ETH.h>
#include <WiFi.h>
#include <PubSubClient.h>  
#include <ArduinoJson.h>  
#include <RTClib.h>
#include "time.h"
#include "ATM90E36.h" 

/*
  Definiciones globales para la identificación del equipo dentro de la red
  y el control de la salida de depuración por puerto serie.
*/
#define DEBUG_ENABLE 0

#define DEVICE_TYPE "ESP32_ENERGY_METER"
#define DEVICE_SERIAL "001"

/*
  Parámetros estáticos de red que aseguran la asignación de una dirección
  fija en la red local y determinan los servidores de resolución de nombres
  y sincronización horaria.
*/
IPAddress local_IP(192, 168, 80, 60);
IPAddress gateway(192, 168, 80, 1);
IPAddress subnet(255, 255, 255, 0);
IPAddress primaryDNS(8, 8, 8, 8);
IPAddress secondaryDNS(1, 1, 1, 1);
const char* ntpServer = "pool.ntp.org";

/*
  Instancias para manejar conexiones remotas de diagnóstico mediante terminal de red.
*/
WiFiServer telnetServer(23);
WiFiClient telnetClient;

/*
  Configuración del agente de mensajería responsable de enrutar la telemetría.
  Se establecen las direcciones, puertos y los temas específicos para el envío
  de datos y la recepción de comandos externos.
*/
const char* mqtt_broker     = "192.168.80.50"; 
const int mqtt_port         = 1883;
const char* mqtt_topic_data = "energy-meter/" DEVICE_SERIAL "/data";
const char* mqtt_topic_cmd  = "energy-meter/" DEVICE_SERIAL "/cmd";  
const char* mqtt_client_id  = DEVICE_TYPE "_" DEVICE_SERIAL;

WiFiClient ethClient; 
PubSubClient mqttClient(ethClient);

/*
  Instancia para el control del reloj de tiempo real externo.
*/
RTC_DS3231 rtc;

/*
  Variables de control lógico que registran el estado actual de las conexiones,
  la validez de la hora y administran los retardos no bloqueantes del ciclo principal.
*/
static bool eth_connected    = false;
static bool rtc_synchronized = false;
bool ledIsOn                 = false;

unsigned long lastNtpSync       = 0;
unsigned long lastAcquisition   = 0;
unsigned long ledTurnOffTime    = 0;
unsigned long lastMqttReconnect = 0;

/*
  Espacios de memoria asignados temporalmente para albergar el 
  desglose armónico de la señal eléctrica durante cada lectura.
*/
float vSpec[32];
float iSpec[32]; 

/*
  Declaración anticipada de funciones para organizar la estructura del código.
*/
void remoteLog(const char *format, ...);
bool syncRTCwithNTP();
void processAndPublishTelemetry();
void imprimirEstadoTelnet(WiFiClient &cliente);

/*
  Formatea y distribuye mensajes de estado del sistema. Evalúa si el reloj interno
  posee hora válida para estampar la marca temporal y envía el texto resultante
  tanto a la conexión serial local como a los clientes de terminal remoto conectados.
*/
void remoteLog(const char *format, ...) {
  char loc_buf[256];
  va_list arg;
  va_start(arg, format);
  vsnprintf(loc_buf, sizeof(loc_buf), format, arg);
  va_end(arg);

  char timeBuf[25] = "Sin Sincronizar";
  if (rtc_synchronized) {
    DateTime now = rtc.now();
    snprintf(timeBuf, sizeof(timeBuf), "%02d/%02d/%04d %02d:%02d:%02d", 
             now.day(), now.month(), now.year(), now.hour(), now.minute(), now.second());
  }

  char final_buf[300];
  snprintf(final_buf, sizeof(final_buf), "[%s] %s\r\n", timeBuf, loc_buf);

  #if DEBUG_ENABLE
    Serial.print(final_buf); 
  #endif
  if (telnetClient && telnetClient.connected()) {
    telnetClient.print(final_buf);
  }
}

/*
  Consulta la hora brindada por el servidor externo de red e intenta ajustar
  el reloj de tiempo real del hardware. Incluye un tiempo de espera máximo de 500ms
  para garantizar la continuidad del ciclo de ejecución principal.
*/
bool syncRTCwithNTP() {
  struct tm timeinfo;
  if (!getLocalTime(&timeinfo, 500)) return false;
  rtc.adjust(DateTime(timeinfo.tm_year + 1900, timeinfo.tm_mon + 1, timeinfo.tm_mday, 
                      timeinfo.tm_hour, timeinfo.tm_min, timeinfo.tm_sec));
  return true;
}

/*
  Intercepta y procesa comandos entrantes desde el servidor de mensajería
  para propósitos de depuración y registro en la terminal remota.
*/
void mqttCallback(char* topic, byte* payload, unsigned int length) {
  String message = "";
  for (unsigned int i = 0; i < length; i++) {
    message += (char)payload[i];
  }
  remoteLog("Mensaje recibido en %s: %s", topic, message.c_str());
}

/*
  Centraliza la recolección de variables eléctricas interactuando con el medidor.
  Calcula valores derivados, solicita el espectro armónico y estructura
  un documento JSON de telemetría para su transmisión hacia el agente de red.
*/
void processAndPublishTelemetry() {
  float raw_v1 = eic.GetLineVoltage1();
  float raw_i1 = eic.GetLineCurrentCT1();
  float temp   = eic.GetTemperature(); 

  float vRms = 0.0, iRms = 0.0, freq = 0.0;
  float pActive = 0.0, pActiveF = 0.0, pActiveH = 0.0;
  float qReactive = 0.0, sApparent = 0.0, pf = 1.0, phase = 0.0;
  float thdV = 0.0, thdI = 0.0;
  double vRmsFund = 0.0, iRmsFund = 0.0;

  memset(vSpec, 0, sizeof(vSpec));
  memset(iSpec, 0, sizeof(iSpec));

  bool validVoltage = (raw_v1 >= 10.0);   
  bool validCurrent = (raw_i1 >= 0.010);  

  if (validVoltage) {
    eic.RunHarmonicsEngine();
    freq = eic.GetFrequency();
    vRms = raw_v1; 
    vRmsFund = eic.GetFundamentalVoltage1();
    thdV  = eic.GetVHarmCT1();
    eic.GetHarmonicsVoltage1(vSpec);

    if (validCurrent) {
      iRms = raw_i1; 
      iRmsFund = eic.GetFundamentalCurrent1();
      thdI  = eic.GetCHarmCT1();
      eic.GetHarmonicsCurrent1(iSpec);

      pActive   = eic.GetActivePowerCT1();
      pActiveF  = eic.GetActiveFundamentalPowerCT1();
      pActiveH  = eic.GetActiveHarmonicPowerCT1();
      qReactive = eic.GetReactivePowerCT1();
      sApparent = eic.GetApparentPowerCT1();

      pf    = eic.GetPowerFactorCT1();
      phase = eic.GetPhaseCT1(); 
    }
  }

  DateTime now = rtc.now();
  char timeISO[25];
  snprintf(timeISO, sizeof(timeISO), "%04d-%02d-%02dT%02d:%02d:%02dZ", 
           now.year(), now.month(), now.day(), now.hour(), now.minute(), now.second());

  JsonDocument doc; 

  doc["timestamp"] = timeISO;
  doc["vRms"]      = vRms;
  doc["iRms"]      = iRms;
  doc["freq"]      = freq;
  doc["temp"]      = temp;
  doc["pActive"]   = pActive;
  doc["pActiveF"]  = pActiveF;
  doc["pActiveH"]  = pActiveH;
  doc["qReactive"] = qReactive;
  doc["sApparent"] = sApparent;
  doc["pf"]        = pf;
  doc["phase"]     = phase;
  doc["thdV"]      = thdV;
  doc["thdI"]      = thdI;
  doc["vRmsFund"]  = vRmsFund;
  doc["iRmsFund"]  = iRmsFund;
  
  JsonArray array_dft_v = doc["dftV"].to<JsonArray>();
  JsonArray array_dft_i = doc["dftI"].to<JsonArray>();
  for(int h=0; h<15; h++) {
    array_dft_v.add(serialized(String(vSpec[h], 3))); 
    array_dft_i.add(serialized(String(iSpec[h], 3)));
  }

  char payload[1024];
  size_t n = serializeJson(doc, payload);

  if (!mqttClient.publish(mqtt_topic_data, payload, n)) {
    remoteLog("Fallo al enviar el payload MQTT.");
  }
}

/*
  Responde a los eventos generados por el subsistema de red.
  Inicia el servidor Telnet, solicita el tiempo base por red y establece 
  la escucha para actualizaciones inalámbricas de firmware (OTA).
*/
void onEvent(arduino_event_id_t event, arduino_event_info_t info) {
  if (event == ARDUINO_EVENT_ETH_GOT_IP) {
    Serial.print("ETH OK: "); Serial.println(ETH.localIP());
    
    configTime(0, 0, ntpServer);
    eth_connected = true;

    telnetServer.begin();
    telnetServer.setNoDelay(true);

    ArduinoOTA.setHostname("esp32-power-meter");
    ArduinoOTA.onStart([]() { remoteLog("OTA Iniciando actualización..."); });
    ArduinoOTA.onEnd([]() { remoteLog("OTA Actualización finalizada exitosamente."); });
    ArduinoOTA.onError([](ota_error_t error) { remoteLog("OTA Error: %u", error); });
    ArduinoOTA.begin();

  } else if (event == ARDUINO_EVENT_ETH_DISCONNECTED) {
    eth_connected = rtc_synchronized = false;
  }
}

/*
  Emite un informe conciso con el estatus operativo general hacia el cliente remoto,
  minimizando el uso de ciclos de CPU dedicados a la visualización de estado.
*/
void imprimirEstadoTelnet(WiFiClient &cliente) {
  cliente.print("\r\n--- ESTADO EXPRÉS ---\r\n\n");
  cliente.print("ETH: "); cliente.print(eth_connected ? "OK\r\n" : "FAIL\r\n");
  cliente.print("MQTT: "); cliente.print(mqttClient.connected() ? "OK\r\n" : "FAIL\r\n");
  cliente.print("RTC: "); cliente.print(rtc_synchronized ? "OK\r\n" : "FAIL\r\n");
  cliente.print("\n----------------------\r\n");
}

/*
  Rutina de inicialización primaria.
  Configura los adaptadores físicos de red y fija las direcciones predeterminadas,
  prepara el protocolo MQTT y establece la calibración inicial del medidor de energía.
*/
void setup() {
  Serial.begin(115200);
  ConfigureBoard(); 
  rtc.begin();
  
  WiFi.onEvent(onEvent);
  SPI.begin(19, 20, 18);

  ETH.begin(ETH_PHY_W5500, 1, 14, 15, -1, SPI);
  
  if (!ETH.config(local_IP, gateway, subnet, primaryDNS, secondaryDNS)) {
    Serial.println("¡Error al configurar IP Fija!");
  } else {
    Serial.println("Configuración de IP Fija aplicada.");
  }
  
  mqttClient.setServer(mqtt_broker, mqtt_port);
  mqttClient.setCallback(mqttCallback); 
  
  // Se establece un tamaño de buffer adecuado para contener el documento JSON completo
  mqttClient.setBufferSize(1024); 
  
  eic.begin(CAL_LINE_FREQ, CAL_PGA_GAIN, CAL_VOLT_GAIN_1, 0, 0, CAL_VOLT_OFFSET_1, 0, 0, CAL_CURR_GAIN_1, 0, 0, CAL_CURR_OFFSET_1, 0, 0, 0, 0);

  int16_t offsetValue = eic.CalculateVIOffset(IRMSCT1, IRMSCT1LSB);
  Serial.printf("Offset HEX: 0x%04X\n", (uint16_t)offsetValue); 

  lastAcquisition = millis();
}

/*
  Bucle infinito del microcontrolador.
  Sostiene la comunicación de red, asegura la estabilidad de las conexiones 
  y ejecuta la adquisición de mediciones garantizando una cadencia exacta y continua.
*/
void loop() {
  unsigned long currentMillis = millis();

  // Gestión de solicitudes entrantes para terminales remotas
  if (telnetServer.hasClient()) {
    if (!telnetClient || !telnetClient.connected()) {
      if (telnetClient) telnetClient.stop(); 
      telnetClient = telnetServer.accept();
      
      imprimirEstadoTelnet(telnetClient);
    } else {
      telnetServer.accept().stop(); 
    }
  }

  // Despacho de procesos de actualización en línea
  if (eth_connected) {
    ArduinoOTA.handle();
  }

  // Verificación y resincronización horaria controlada para prevenir bloqueos de ejecución
  if (eth_connected && (!rtc_synchronized || currentMillis - lastNtpSync >= 3600000)) {
    if (syncRTCwithNTP()) {
      rtc_synchronized = true;
      lastNtpSync = currentMillis;
    }
  }

  // Rutina de reconexión automática del cliente de mensajería MQTT
  if (eth_connected && !mqttClient.connected()) {
    if (currentMillis - lastMqttReconnect >= 5000) {
      lastMqttReconnect = currentMillis;    
      if (mqttClient.connect(mqtt_client_id)) {
        remoteLog("Broker MQTT CONECTADO");
        mqttClient.subscribe(mqtt_topic_cmd);
        remoteLog("Suscripto a comandos en: %s", mqtt_topic_cmd);
      } else {
        remoteLog("Fallo al conectar broker, estado=%d", mqttClient.state());
      }
    }
  }

  // Procesamiento de colas y mantenimiento de la conexión de red
  if (mqttClient.connected()) {
    mqttClient.loop();
  }

  // Ejecución del muestreo condicionada a intervalos estrictos de un segundo
  if (currentMillis - lastAcquisition >= 1000) {
    
    // La asignación absoluta del tiempo actual previene derivas de sincronización
    lastAcquisition = currentMillis; 

    // Se efectúa la transmisión únicamente bajo condiciones operativas estables
    if (mqttClient.connected() && rtc_synchronized) {
      processAndPublishTelemetry();
      
      digitalWrite(PIN_LED_RED, HIGH);
      ledTurnOffTime = currentMillis + 100;
      ledIsOn = true;
    } 
  }

  // Restauración del estado de señalización LED mediante evaluación no bloqueante
  if (ledIsOn && currentMillis >= ledTurnOffTime) {
    digitalWrite(PIN_LED_RED, LOW);
    ledIsOn = false;
  }
}