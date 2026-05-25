/*
  El presente código configura la placa principal del monitor de energía,
  integrando el microcontrolador de control con el circuito integrado de medición.
  Establece el mapeo de pines físicos, define los parámetros de calibración 
  específicos de los transformadores utilizados e inicializa los buses de comunicación.
*/

#include <Arduino.h>
#include <SPI.h>
#include <Wire.h>
#include <ATM90E36.h>

/* 
  Asignación de pines físicos del microcontrolador.
  Se definen las conexiones para los buses de comunicación en serie,
  así como los puertos de entrada y salida para indicadores luminosos,
  botones de usuario y señales de control externas.
*/

#define PIN_I2C_SDA      6  /* Línea de datos bidireccional del bus I2C */
#define PIN_I2C_SCL      7  /* Línea de sincronización de reloj del bus I2C */
#define PIN_WS2812B      8  /* Salida digital para el control del indicador LED RGB direccionable */
#define PIN_INT_ETH      15 /* Entrada de interrupción de hardware para el controlador de red Ethernet */

#define PIN_RS485_RX     4  /* Línea de recepción de datos para el transceptor industrial RS485 */
#define PIN_RS485_TX     5  /* Línea de transmisión de datos para el transceptor industrial RS485 */
#define PIN_RS485_EN     23 /* Pin de habilitación de transmisión para administrar la comunicación semi-dúplex */

#define PIN_USER_BTN     9  /* Entrada digital configurada para leer pulsaciones del operador */
#define PIN_ATM_WO       1  /* Pin de entrada para recibir alertas de umbral directamente desde el medidor */
#define PIN_DCV_IN       3  /* Pin analógico designado para medir la tensión continua de alimentación del sistema */
#define PIN_LED_RED      22 /* Salida digital para encender o apagar el indicador luminoso de advertencia */
#define PIN_FET_CTRL     2  /* Salida de control para conmutar un transistor de efecto de campo externo */

/* 
  Definición de constantes de calibración para el circuito medidor.
  Se establecen los valores base para la frecuencia de línea de la red eléctrica
  y la configuración del amplificador de ganancia programable interno.
  Se declaran explícitamente los factores de corrección obtenidos empíricamente 
  para el transformador de voltaje de corriente alterna y el transformador de núcleo partido.
*/

const unsigned short CAL_LINE_FREQ     = 389;    /* Representa una configuración para redes de cincuenta hercios */
const unsigned short CAL_PGA_GAIN      = 0x5555; /* Configura el amplificador interno para aplicar una ganancia doble a la señal */

const unsigned short CAL_VOLT_GAIN_1   = 32031;  /* Factor multiplicador calibrado para el transformador de voltaje de ocho voltios */
const unsigned short CAL_VOLT_OFFSET_1 = 64608;  /* Compensación matemática para corregir el error de cero en el canal de tensión */

const unsigned short CAL_CURR_GAIN_1   = 31905;   //33500;  /* Factor multiplicador calibrado para el transformador de corriente de cien amperios */
const unsigned short CAL_CURR_OFFSET_1 = 64072;  /* Compensación matemática para corregir el error de cero en el canal de corriente */

/* 
  Declaración de la instancia principal de la biblioteca del medidor de energía y 
  creación de variables globales destinadas a la identificación unívoca del procesador.
*/

ATM90E36 eic;
uint64_t chipId = 0;

/*
  Prepara el entorno de ejecución fundamental del microcontrolador.
  Inicializa la comunicación por puerto serie, recupera la dirección física incrustada en el silicio,
  configura la dirección del flujo de datos de los pines de hardware y establece un estado inicial
  conocido y seguro para evitar el accionamiento involuntario de componentes.
*/
void ConfigureBoard() {
    Serial.begin(115200);
    while(!Serial); 

    // Recupera la dirección física programada de fábrica en la memoria del procesador
    chipId = ESP.getEfuseMac();

    // Define el comportamiento eléctrico de los puertos físicos
    pinMode(PIN_USER_BTN, INPUT_PULLUP);
    pinMode(PIN_ATM_WO,   INPUT);
    pinMode(PIN_LED_RED,  OUTPUT);
    pinMode(PIN_INT_ETH,  OUTPUT);
    pinMode(PIN_FET_CTRL, OUTPUT);
    
    // Aplica un nivel lógico bajo por defecto para mantener desactivados los actuadores
    digitalWrite(PIN_LED_RED, LOW);
    digitalWrite(PIN_FET_CTRL, LOW);

    // Habilita el controlador interno del bus I2C para permitir la conexión de placas de expansión
    Wire.begin(PIN_I2C_SDA, PIN_I2C_SCL);

    // Formatea e imprime en la consola de depuración el identificador exclusivo del microcontrolador
    Serial.printf("[SYSTEM] ESP32-C6 ID: %04X%08X\n", 
                  (uint16_t)(chipId >> 32), 
                  (uint32_t)chipId);
}

/*
  Consulta de manera directa el estado operativo primario del circuito integrado de medición.
  Extrae de la memoria del dispositivo los registros de estado del sistema general y de las funciones
  de medición pura. Funciona adicionalmente como un mecanismo de validación para asegurar
  que el canal de comunicación serial de alta velocidad opera de manera correcta y estable.
*/
void DisplayBoardConfiguration() {
    delay(100); 

    // Recupera secuencialmente los cuatro registros de estado críticos
    unsigned short s0 = eic.GetSysStatus0();
    unsigned short s1 = eic.GetSysStatus1();
    unsigned short e0 = eic.GetMeterStatus0();
    unsigned short e1 = eic.GetMeterStatus1();

    // Formatea la salida y expone los valores en formato hexadecimal a través del terminal
    Serial.println("\n[ATM90E36 REGISTER DUMP]");
    Serial.printf("System: S0:0x%04X | S1:0x%04X\n", s0, s1);
    Serial.printf("Meter:  E0:0x%04X | E1:0x%04X\n", e0, e1);

    // Evalúa si el chip responde con datos vacíos o con la línea de transmisión saturada, 
    // lo cual evidencia una desconexión o falla eléctrica en el bus de comunicación
    if (s0 == 0xFFFF || s0 == 0x0000) {
        Serial.println("CRITICAL ERROR: No comms with ATM90E36");
    }
    Serial.println("");
}