/*
  El presente documento contiene el programa principal del firmware encargado de gestionar
  el monitor de energía. Integra múltiples subsistemas, incluyendo conectividad de red por cable,
  sincronización de tiempo, persistencia de datos en memoria flash y publicación de métricas
  de telemetría hacia un servidor remoto utilizando el protocolo de mensajería ligera.
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
#include <Preferences.h>

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
  Instancias para el control de periféricos externos y el manejo de
  almacenamiento en memoria no volátil.
*/
RTC_DS3231 rtc;
Preferences preferences;

/*
  Variables de control lógico que registran el estado actual de las conexiones,
  la validez de la hora y administran los retardos no bloqueantes del ciclo principal.
*/
static bool eth_connected   = false;
static bool rtc_synchronized = false;
bool ledIsOn                 = false;

unsigned long lastNtpSync       = 0;
unsigned long lastAcquisition   = 0;
unsigned long ledTurnOffTime    = 0;
unsigned long lastMqttReconnect = 0;

/*
  Espacios de memoria asignados temporalmente para conservar las lecturas eléctricas,
  incluyendo arreglos para albergar el desglose armónico de la señal y los
  totalizadores históricos de energía consumida y generada.
*/
float vSpec[32];
float iSpec[32]; 
int energyUpdateCounter = 0;    

double totalActiveForward = 0.0; 
double totalActiveReverse = 0.0; 
double totalReactiveForward = 0.0;
double totalReactiveReverse   = 0.0;

/*
  Declaración anticipada de funciones para facilitar la compilación y organizar el código.
*/
void remoteLog(const char *format, ...);
void saveAccumulatorsToFlash();
void loadAccumulatorsFromFlash();
bool syncRTCwithNTP();
void processAndPublishTelemetry();
void imprimirEstadoTelnet(WiFiClient &cliente);

/*
  Almacena de manera persistente los valores acumulados de energía en el área
  de preferencias no volátiles. Garantiza que la medición histórica sobreviva
  a cortes imprevistos de suministro eléctrico.
*/
void saveAccumulatorsToFlash() {
  preferences.begin("energy", false);
  
  preferences.putDouble("actForward", totalActiveForward);
  preferences.putDouble("actReverse", totalActiveReverse);
  preferences.putDouble("reactForward", totalReactiveForward);
  preferences.putDouble("reactReverse", totalReactiveReverse);
  preferences.putInt("energyCounter", energyUpdateCounter);
  
  preferences.end();
  remoteLog("Accumulators saved to Flash NVS.");
}

/*
  Recupera los registros de energía almacenados previamente en la memoria flash
  durante la secuencia de inicio del sistema.
*/
void loadAccumulatorsFromFlash() {
  preferences.begin("energy", true);
  
  totalActiveForward   = preferences.getDouble("actForward", 0.0);
  totalActiveReverse   = preferences.getDouble("actReverse", 0.0);
  totalReactiveForward  = preferences.getDouble("reactForward", 0.0);
  totalReactiveReverse  = preferences.getDouble("reactReverse", 0.0);
  energyUpdateCounter   = preferences.getInt("energyCounter", 0);
  
  preferences.end();
  remoteLog("Accumulators retrieved from Flash NVS: AF:%.2f, AR:%.2f, RF:%.2f, RR:%.2f", 
            totalActiveForward, totalActiveReverse, totalReactiveForward, totalReactiveReverse);
}

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
  el reloj de tiempo real del hardware para mantener métricas temporales exactas.
*/
bool syncRTCwithNTP() {
  struct tm timeinfo;
  if (!getLocalTime(&timeinfo)) return false;
  rtc.adjust(DateTime(timeinfo.tm_year + 1900, timeinfo.tm_mon + 1, timeinfo.tm_mday, 
                      timeinfo.tm_hour, timeinfo.tm_min, timeinfo.tm_sec));
  return true;
}

/*
  Intercepta y procesa comandos entrantes desde el servidor de mensajería.
  Permite ejecutar acciones remotas, tales como restablecer a cero los 
  acumuladores históricos de energía bajo demanda.
*/
void mqttCallback(char* topic, byte* payload, unsigned int length) {
  String message = "";
  for (unsigned int i = 0; i < length; i++) {
    message += (char)payload[i];
  }

  remoteLog("Mensaje recibido en %s: %s", topic, message.c_str());

  if (String(topic) == mqtt_topic_cmd) {
    if (message == "reset") {
      totalActiveForward = 0.0;
      totalActiveReverse = 0.0;
      totalReactiveForward = 0.0;
      totalReactiveReverse = 0.0;
      energyUpdateCounter = 0;

      saveAccumulatorsToFlash();

      remoteLog("ACUMULADORES RESETEADOS A CERO");
    }
  }
}

/*
  Centraliza la recolección de variables eléctricas leyendo el medidor.
  Calcula valores derivados, solicita el espectro armónico y finalmente estructura
  un documento JSON que engloba toda la telemetría, el cual se transmite al agente de red.
*/
void processAndPublishTelemetry() {
  float raw_v1 = eic.GetLineVoltage1();
  float raw_i1 = eic.GetLineCurrentCT1();
  float temp   = eic.GetTemperature(); 

  float vRms = 0.0, iRms = 0.0, freq = 0.0;
  float pActive = 0.0, pActiveF = 0.0, pActiveH = 0.0;
  float qReactive = 0.0, sApparent = 0.0, pf = 0.0, phase = 0.0;
  float thdV = 0.0, thdI = 0.0;
  double vRmsFund = 0.0, iRmsFund = 0.0;

  // Limpia los arreglos previos antes de insertar nuevos datos
  memset(vSpec, 0, sizeof(vSpec));
  memset(iSpec, 0, sizeof(iSpec));

  // Aplica filtros simples para ignorar ruido eléctrico cuando no existe carga
  bool validVoltage = (raw_v1 >= 10.0);   
  bool validCurrent = (raw_i1 >= 0.001);  

  if (validVoltage) {

    // Ejecuta el coprocesador matemático del medidor
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

  remoteLog("Vrms: %.2f V | Irms: %.3f A | P: %+.2f W | Q: %+.2f VAr | PF: %+.2f | THDv: %.2f %% | THDi: %.2f %%",
            vRms, iRms, pActive, qReactive, pf, thdV, thdI);

  // Obtiene el momento temporal preciso para adjuntarlo al bloque de datos
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
  doc["totalActiveForward"]    = totalActiveForward;
  doc["totalActiveReverse"]    = totalActiveReverse;
  doc["totalReactiveForward"]  = totalReactiveForward;
  doc["totalReactiveReverse"]  = totalReactiveReverse;
  doc["vRmsFund"]   = vRmsFund;
  doc["iRmsFund"]   = iRmsFund;
  
  // Transforma los datos armónicos en matrices serializables
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
  Establece rutinas como solicitar el tiempo por red, iniciar servidores 
  y configurar actualizaciones inalámbricas de firmware tras asegurar el enlace.
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

    loadAccumulatorsFromFlash();

  } else if (event == ARDUINO_EVENT_ETH_DISCONNECTED) {
    eth_connected = rtc_synchronized = false;
  }
}

/*
  Emite un informe detallado con el estatus operativo general.
  Abarca conectividad de red, salud del servicio de publicación, sincronización
  del reloj interno y la métrica de tiempo de actividad del microcontrolador.
*/
void imprimirEstadoTelnet(WiFiClient &cliente) {
  cliente.print("\r\n--- REGISTRO DE ESTADO ACTUAL ---\r\n\n");
  
  cliente.print("[RED]\r\n");
  cliente.print("  Ethernet: "); cliente.print(eth_connected ? "CONECTADO\r\n" : "DESCONECTADO\r\n");
  cliente.print("  IP:       "); cliente.println(ETH.localIP()); 
  cliente.print("  Gateway:  "); cliente.println(ETH.gatewayIP());
  cliente.print("  DNS 1:    "); cliente.println(ETH.dnsIP(0));
  
  cliente.print("\r\n[MQTT]\r\n");
  cliente.print("  Conexión: "); cliente.print(mqttClient.connected() ? "CONECTADA\r\n" : "DESCONECTADA\r\n");
  cliente.print("  Broker:   "); cliente.print(mqtt_broker); cliente.print("\r\n");
  
  cliente.print("\r\n[TIEMPO]\r\n");
  cliente.print("  RTC Sincronizado: "); cliente.print(rtc_synchronized ? "SI\r\n" : "NO\r\n");
  
  if (rtc_synchronized) {
    DateTime now = rtc.now();
    char timeBuf[60];
    snprintf(timeBuf, sizeof(timeBuf), "  Hora Actual: %02d/%02d/%04d %02d:%02d:%02d\r\n",
             now.day(), now.month(), now.year(), now.hour(), now.minute(), now.second());
    cliente.print(timeBuf);
    
    unsigned long tiempoDesdeNtp = millis() - lastNtpSync;
    cliente.print("  Última act. NTP:  Hace "); 
    cliente.print(tiempoDesdeNtp / 1000); 
    cliente.print(" segundos\r\n");
  }

  cliente.print("\r\n[SISTEMA]\r\n");
  cliente.print("  Uptime (ms): "); cliente.print(millis()); cliente.print("\r\n");
  cliente.print("\n---------------------------------\r\n\r\n");
}


/*
  Rutina de inicialización primaria.
  Prepara el hardware subyacente, configura los adaptadores físicos de red,
  fija las direcciones predeterminadas, prepara el protocolo MQTT y transmite
  todos los parámetros de configuración fundamentales hacia el procesador de energía.
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
  mqttClient.setBufferSize(1024); 
  
  eic.begin(CAL_LINE_FREQ, CAL_PGA_GAIN, CAL_VOLT_GAIN_1, 0, 0, CAL_VOLT_OFFSET_1, 0, 0, CAL_CURR_GAIN_1, 0, 0, CAL_CURR_OFFSET_1, 0, 0, 0, 0);

  int16_t offsetValue = eic.CalculateVIOffset(IRMSCT1, IRMSCT1LSB);
  Serial.printf("Offset HEX: 0x%04X\n", (uint16_t)offsetValue); 

  lastAcquisition = millis();
}

/*
  Bucle infinito del microcontrolador.
  Mantiene en segundo plano las conexiones Telnet y OTA, asegura que la hora del
  reloj interno y el vínculo con el servidor MQTT se mantengan estables, e interroga
  de manera cadenciada al medidor para acumular la energía y publicar telemetría.
*/
void loop() {
  unsigned long currentMillis = millis();

  // Verifica solicitudes entrantes para terminales remotas
  if (telnetServer.hasClient()) {
    if (!telnetClient || !telnetClient.connected()) {
      if (telnetClient) telnetClient.stop(); 
      telnetClient = telnetServer.accept();
      
      imprimirEstadoTelnet(telnetClient);
    } else {
      telnetServer.accept().stop(); 
    }
  }

  if (eth_connected) {
    ArduinoOTA.handle();
  }

  // Comprueba la vigencia temporal e inicia la resincronización cuando transcurre el ciclo predefinido
  if (eth_connected && (!rtc_synchronized || currentMillis - lastNtpSync >= 3600000)) {
    if (syncRTCwithNTP()) {
      rtc_synchronized = true;
      lastNtpSync = currentMillis;
    }
  }

  // Administra la reconexión automática en caso de perder enlace con el agente de mensajes
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

  // Procesa colas de red pendientes si el enlace está operativo
  if (mqttClient.connected()) {
    mqttClient.loop();
  }

  // Rutina de interrupción programada por software para ejecutar la toma de mediciones
  if (currentMillis - lastAcquisition >= 1000) {
    lastAcquisition += 1000; 

    // Bloquea la recolección si las dependencias críticas no operan correctamente
    if (mqttClient.connected() && rtc_synchronized) {
      energyUpdateCounter++;

      // Realiza respaldos periódicos de la energía acumulada hacia la memoria permanente
      if (energyUpdateCounter >= 300) {
        totalActiveForward    += eic.GetForwardActiveEnergyCT1();
        totalActiveReverse    += eic.GetReverseActiveEnergyCT1();
        totalReactiveForward   += eic.GetForwardReactiveEnergyCT1();
        totalReactiveReverse   += eic.GetReverseReactiveEnergyCT1();
        energyUpdateCounter = 0;

        saveAccumulatorsToFlash();
      }

      processAndPublishTelemetry();
      
      // Controla el parpadeo de actividad en el indicador luminoso de la placa
      digitalWrite(PIN_LED_RED, HIGH);
      ledTurnOffTime = currentMillis + 100;
      ledIsOn = true;
    } else {
      remoteLog("Esperando condiciones -> MQTT: %s | RTC: %s", 
                mqttClient.connected() ? "OK" : "FAIL", 
                rtc_synchronized ? "OK" : "FAIL");
    }
  }

  // Restaura el estado del indicador luminoso de actividad de forma no bloqueante
  if (ledIsOn && currentMillis >= ledTurnOffTime) {
    digitalWrite(PIN_LED_RED, LOW);
    ledIsOn = false;
  }
}