/*
  El presente código constituye la cabecera principal para el circuito integrado ATM90E36.
  Se distribuye como código abierto y define todas las constantes, direcciones de memoria
  y la estructura de la clase necesaria para controlar el medidor de energía.
*/

#ifndef ATM90E36_h
#define ATM90E36_h
#include <Arduino.h>
#include <SPI.h>

/* Definiciones para el bus de comunicación SPI */
#define WRITE 0 /* Indica una operación de escritura en el bus SPI */
#define READ 1  /* Indica una operación de lectura en el bus SPI */

#define MISO 20 /* Pin asignado para la entrada de datos del bus SPI */
#define MOSI 18 /* Pin asignado para la salida de datos del bus SPI */
#define SCLK 19 /* Pin asignado para la señal de reloj del bus SPI */

#define CS_ATM 21 /* Pin asignado para la selección del chip en el bus SPI */

/* Definiciones de arquitectura y referencia de hardware */

#define ATM90E36_DEVICE 36 /* Identificador constante del dispositivo utilizado */
#define ATM90DEVICE ATM90E36_DEVICE

/* Configura el uso de una única referencia de voltaje para todas las fases si se establece como verdadero */
#define ATM_SINGLEVOLTAGE true 

/* Configura el dispositivo para redes de fase dividida típicas de ciertas regiones si se establece como verdadero */
#define ATM_SPLITPHASE false 

/* Direcciones de memoria para los registros de estado general y control */
#define SoftReset 0x00   /* Registro para forzar un reinicio por software */
#define SysStatus0 0x01  /* Registro principal de estado del sistema */
#define SysStatus1 0x02  /* Registro secundario de estado del sistema */
#define FuncEn0 0x03     /* Registro principal de habilitación de funciones */
#define FuncEn1 0x04     /* Registro secundario de habilitación de funciones */
#define ZXConfig 0x07    /* Configuración para la detección de cruce por cero */
#define SagTh 0x08       /* Umbral para la detección de caídas de tensión */
#define PhaseLossTh 0x09 /* Umbral para la detección de pérdida de fase */
#define INWarnTh0 0x0A   /* Umbral de advertencia para la corriente del neutro */
#define INWarnTh1 0x0B   /* Umbral de advertencia para el conversor analógico a digital */
#define THDNUTh 0x0C     /* Umbral para la distorsión armónica total de tensión */
#define THDNITh 0x0D     /* Umbral para la distorsión armónica total de corriente */
#define DMACtrl 0x0E     /* Control de interrupciones para acceso directo a memoria */
#define LastSPIData 0x0F /* Almacena el último valor transmitido o recibido por SPI */

/* Direcciones de memoria para los registros del modo de bajo consumo energético */
#define DetectCtrl 0x10   /* Controlador de detección de corriente */
#define DetectTh1 0x11    /* Umbral de detección de corriente para la primera fase */
#define DetectTh2 0x12    /* Umbral de detección de corriente para la segunda fase */
#define DetectTh3 0x13    /* Umbral de detección de corriente para la tercera fase */
#define PMoffsetCT1 0x14  /* Compensación para la primera fase en medición parcial */
#define PMoffsetCT2 0x15  /* Compensación para la segunda fase en medición parcial */
#define PMoffsetCT3 0x16  /* Compensación para la tercera fase en medición parcial */
#define PMPGA 0x17        /* Configuración de ganancia en medición parcial */
#define PMIRMSCT1 0x18    /* Corriente eficaz de la primera fase en medición parcial */
#define PMIRMSCT2 0x19    /* Corriente eficaz de la segunda fase en medición parcial */
#define PMIRMSCT3 0x1A    /* Corriente eficaz de la tercera fase en medición parcial */
#define PMConfig 0x1B     /* Configuración general para el modo de medición parcial */
#define PMAvgSamples 0x1C /* Cantidad de muestras a promediar en el cálculo eficaz */
#define PMIRMSLSB 0x1D    /* Bits menos significativos para los registros de corriente del modo parcial */

/* Direcciones de memoria para los registros de configuración operativa */
#define ConfigStart 0x30 /* Inicia el bloque de configuración del medidor */
#define PLconstH 0x31    /* Parte alta de la constante de pulsos del medidor */
#define PLconstL 0x32    /* Parte baja de la constante de pulsos del medidor */
#define MMode0 0x33      /* Primer registro de modo de medición */
#define MMode1 0x34      /* Segundo registro de modo de medición */
#define PStartTh 0x35    /* Umbral de arranque para la potencia activa */
#define QStartTh 0x36    /* Umbral de arranque para la potencia reactiva */
#define SStartTh 0x37    /* Umbral de arranque para la potencia aparente */
#define PPhaseTh 0x38    /* Umbral de acumulación para la potencia activa */
#define QPhaseTh 0x39    /* Umbral de acumulación para la potencia reactiva */
#define SPhaseTh 0x3A    /* Umbral de acumulación para la potencia aparente */
#define CSZero 0x3B      /* Primer registro de suma de comprobación interna */

/* Direcciones de memoria para los registros de calibración de potencia y fase */
#define CalStart 0x40   /* Inicia el bloque de calibración base */
#define PoffsetCT1 0x41 /* Compensación de potencia activa para la primera línea */
#define QoffsetCT1 0x42 /* Compensación de potencia reactiva para la primera línea */
#define PoffsetCT2 0x43 /* Compensación de potencia activa para la segunda línea */
#define QoffsetCT2 0x44 /* Compensación de potencia reactiva para la segunda línea */
#define PoffsetCT3 0x45 /* Compensación de potencia activa para la tercera línea */
#define QoffsetCT3 0x46 /* Compensación de potencia reactiva para la tercera línea */
#define GainCT1 0x47    /* Ganancia de calibración general para la primera línea */
#define PhiCT1 0x48     /* Ángulo de calibración para la primera línea */
#define GainCT2 0x49    /* Ganancia de calibración general para la segunda línea */
#define PhiCT2 0x4A     /* Ángulo de calibración para la segunda línea */
#define GainCT3 0x4B    /* Ganancia de calibración general para la tercera línea */
#define PhiCT3 0x4C     /* Ángulo de calibración para la tercera línea */
#define CSOne 0x4D      /* Segundo registro de suma de comprobación interna */

/* Direcciones de memoria para la calibración de componentes armónicos y fundamentales */
#define HaRMStart 0x50   /* Inicia el bloque de calibración de armónicos */
#define PoffsetCT1F 0x51 /* Compensación de potencia activa fundamental en la primera fase */
#define PoffsetCT2F 0x52 /* Compensación de potencia activa fundamental en la segunda fase */
#define PoffsetCT3F 0x53 /* Compensación de potencia activa fundamental en la tercera fase */
#define PGainCT1F 0x54   /* Ganancia de potencia activa fundamental en la primera fase */
#define PGainCT2F 0x55   /* Ganancia de potencia activa fundamental en la segunda fase */
#define PGainCT3F 0x56   /* Ganancia de potencia activa fundamental en la tercera fase */
#define CSTwo 0x57       /* Tercer registro de suma de comprobación interna */

/* Direcciones de memoria para la calibración directa de mediciones analógicas */
#define AdjStart 0x60   /* Inicia el bloque de ajuste de mediciones */
#define UGainCT1 0x61   /* Ganancia de tensión eficaz para la primera fase */
#define IGainCT1 0x62   /* Ganancia de corriente eficaz para la primera fase */
#define UoffsetCT1 0x63 /* Compensación de tensión para la primera fase */
#define IoffsetCT1 0x64 /* Compensación de corriente para la primera fase */
#define UGainCT2 0x65   /* Ganancia de tensión eficaz para la segunda fase */
#define IGainCT2 0x66   /* Ganancia de corriente eficaz para la segunda fase */
#define UoffsetCT2 0x67 /* Compensación de tensión para la segunda fase */
#define IoffsetCT2 0x68 /* Compensación de corriente para la segunda fase */
#define UGainCT3 0x69   /* Ganancia de tensión eficaz para la tercera fase */
#define IGainCT3 0x6A   /* Ganancia de corriente eficaz para la tercera fase */
#define UoffsetCT3 0x6B /* Compensación de tensión para la tercera fase */
#define IoffsetCT3 0x6C /* Compensación de corriente para la tercera fase */
#define IgainN 0x6D     /* Ganancia de corriente para la línea del neutro */
#define IoffsetN 0x6E   /* Compensación de corriente para la línea del neutro */
#define CSThree 0x6F    /* Cuarto registro de suma de comprobación interna */

/* Direcciones de memoria para los acumuladores de energía total y por fase */
#define APenergyT 0x80   /* Energía activa directa total del sistema */
#define APenergyCT1 0x81 /* Energía activa directa acumulada en la primera fase */
#define APenergyCT2 0x82 /* Energía activa directa acumulada en la segunda fase */
#define APenergyCT3 0x83 /* Energía activa directa acumulada en la tercera fase */
#define ANenergyT 0x84   /* Energía activa inversa total del sistema */
#define ANenergyCT1 0x85 /* Energía activa inversa acumulada en la primera fase */
#define ANenergyCT2 0x86 /* Energía activa inversa acumulada en la segunda fase */
#define ANenergyCT3 0x87 /* Energía activa inversa acumulada en la tercera fase */
#define RPenergyT 0x88   /* Energía reactiva directa total del sistema */
#define RPenergyCT1 0x89 /* Energía reactiva directa acumulada en la primera fase */
#define RPenergyCT2 0x8A /* Energía reactiva directa acumulada en la segunda fase */
#define RPenergyCT3 0x8B /* Energía reactiva directa acumulada en la tercera fase */
#define RNenergyT 0x8C   /* Energía reactiva inversa total del sistema */
#define RNenergyCT1 0x8D /* Energía reactiva inversa acumulada en la primera fase */
#define RNenergyCT2 0x8E /* Energía reactiva inversa acumulada en la segunda fase */
#define RNenergyCT3 0x8F /* Energía reactiva inversa acumulada en la tercera fase */

#define SAenergyT 0x90  /* Energía aparente total del sistema */
#define SenergyCT1 0x91 /* Energía aparente acumulada en la primera fase */
#define SenergyCT2 0x92 /* Energía aparente acumulada en la segunda fase */
#define SenergyCT3 0x93 /* Energía aparente acumulada en la tercera fase */

#define SVenergyT 0x94 /* Energía aparente total calculada aritméticamente */

#define EnStatus0 0x95 /* Primer registro de estado del bloque de medición */
#define EnStatus1 0x96 /* Segundo registro de estado del bloque de medición */

#define SVmeanT 0x98    /* Energía aparente total calculada vectorialmente */
#define SVmeanTLSB 0x99 /* Fracción menos significativa de la suma vectorial */

/* Direcciones de memoria para acumuladores de energía armónica y fundamental */
#define APenergyTF 0xA0   /* Energía activa fundamental directa total */
#define APenergyCT1F 0xA1 /* Energía activa fundamental directa en la primera fase */
#define APenergyCT2F 0xA2 /* Energía activa fundamental directa en la segunda fase */
#define APenergyCT3F 0xA3 /* Energía activa fundamental directa en la tercera fase */
#define ANenergyTF 0xA4   /* Energía activa fundamental inversa total */
#define ANenergyCT1F 0xA5 /* Energía activa fundamental inversa en la primera fase */
#define ANenergyCT2F 0xA6 /* Energía activa fundamental inversa en la segunda fase */
#define ANenergyCT3F 0xA7 /* Energía activa fundamental inversa en la tercera fase */
#define APenergyTH 0xA8   /* Energía activa armónica directa total */
#define APenergyCT1H 0xA9 /* Energía activa armónica directa en la primera fase */
#define APenergyCT2H 0xAA /* Energía activa armónica directa en la segunda fase */
#define APenergyCT3H 0xAB /* Energía activa armónica directa en la tercera fase */
#define ANenergyTH 0xAC   /* Energía activa armónica inversa total */
#define ANenergyCT1H 0xAD /* Energía activa armónica inversa en la primera fase */
#define ANenergyCT2H 0xAE /* Energía activa armónica inversa en la segunda fase */
#define ANenergyCT3H 0xAF /* Energía activa armónica inversa en la tercera fase */

/* Direcciones de memoria para lectura de potencias medias y factor de potencia */
#define PmeanT 0xB0    /* Potencia activa media total */
#define PmeanCT1 0xB1  /* Potencia activa media en la primera fase */
#define PmeanCT2 0xB2  /* Potencia activa media en la segunda fase */
#define PmeanCT3 0xB3  /* Potencia activa media en la tercera fase */
#define QmeanT 0xB4    /* Potencia reactiva media total */
#define QmeanCT1 0xB5  /* Potencia reactiva media en la primera fase */
#define QmeanCT2 0xB6  /* Potencia reactiva media en la segunda fase */
#define QmeanCT3 0xB7  /* Potencia reactiva media en la tercera fase */
#define SAmeanT 0xB8   /* Potencia aparente media total */
#define SmeanCT1 0xB9  /* Potencia aparente media en la primera fase */
#define SmeanCT2 0xBA  /* Potencia aparente media en la segunda fase */
#define SmeanCT3 0xBB  /* Potencia aparente media en la tercera fase */
#define PFmeanT 0xBC   /* Factor de potencia medio total del sistema */
#define PFmeanCT1 0xBD /* Factor de potencia medio en la primera fase */
#define PFmeanCT2 0xBE /* Factor de potencia medio en la segunda fase */
#define PFmeanCT3 0xBF /* Factor de potencia medio en la tercera fase */

/* Direcciones de memoria para fracciones de potencias medias de alta resolución */
#define PmeanTLSB 0xC0   /* Parte fraccionaria de la potencia activa total */
#define PmeanCT1LSB 0xC1 /* Parte fraccionaria de la potencia activa de la primera fase */
#define PmeanCT2LSB 0xC2 /* Parte fraccionaria de la potencia activa de la segunda fase */
#define PmeanCT3LSB 0xC3 /* Parte fraccionaria de la potencia activa de la tercera fase */
#define QmeanTLSB 0xC4   /* Parte fraccionaria de la potencia reactiva total */
#define QmeanCT1LSB 0xC5 /* Parte fraccionaria de la potencia reactiva de la primera fase */
#define QmeanCT2LSB 0xC6 /* Parte fraccionaria de la potencia reactiva de la segunda fase */
#define QmeanCT3LSB 0xC7 /* Parte fraccionaria de la potencia reactiva de la tercera fase */
#define SAmeanTLSB 0xC8  /* Parte fraccionaria de la potencia aparente total */
#define SmeanCT1LSB 0xC9 /* Parte fraccionaria de la potencia aparente de la primera fase */
#define SmeanCT2LSB 0xCA /* Parte fraccionaria de la potencia aparente de la segunda fase */
#define SmeanCT3LSB 0xCB /* Parte fraccionaria de la potencia aparente de la tercera fase */

/* Direcciones de memoria para mediciones de potencia descompuesta y valores eficaces */
#define PmeanTF 0xD0   /* Potencia activa fundamental media total */
#define PmeanCT1F 0xD1 /* Potencia activa fundamental media en la primera fase */
#define PmeanCT2F 0xD2 /* Potencia activa fundamental media en la segunda fase */
#define PmeanCT3F 0xD3 /* Potencia activa fundamental media en la tercera fase */

#define PmeanTH 0xD4   /* Potencia activa armónica media total */
#define PmeanCT1H 0xD5 /* Potencia activa armónica media en la primera fase */
#define PmeanCT2H 0xD6 /* Potencia activa armónica media en la segunda fase */
#define PmeanCT3H 0xD7 /* Potencia activa armónica media en la tercera fase */

#define URMSCT1 0xD9   /* Valor eficaz de la tensión en la primera fase */
#define URMSCT2 0xDA   /* Valor eficaz de la tensión en la segunda fase */
#define URMSCT3 0xDB   /* Valor eficaz de la tensión en la tercera fase */

#define IRMSN 0xDC     /* Valor eficaz calculado para la corriente del neutro */
#define IRMSCT1 0xDD   /* Valor eficaz de la corriente en la primera fase */
#define IRMSCT2 0xDE   /* Valor eficaz de la corriente en la segunda fase */
#define IRMSCT3 0xDF   /* Valor eficaz de la corriente en la tercera fase */

/* Direcciones de memoria para fracciones de medición de alta resolución */
#define PmeanTFLSB 0xE0   /* Parte fraccionaria de la potencia activa fundamental total */
#define PmeanCT1FLSB 0xE1 /* Parte fraccionaria de la potencia fundamental de la primera fase */
#define PmeanCT2FLSB 0xE2 /* Parte fraccionaria de la potencia fundamental de la segunda fase */
#define PmeanCT3FLSB 0xE3 /* Parte fraccionaria de la potencia fundamental de la tercera fase */
#define PmeanTHLSB 0xE4   /* Parte fraccionaria de la potencia activa armónica total */
#define PmeanCT1HLSB 0xE5 /* Parte fraccionaria de la potencia armónica de la primera fase */
#define PmeanCT2HLSB 0xE6 /* Parte fraccionaria de la potencia armónica de la segunda fase */
#define PmeanCT3HLSB 0xE7 /* Parte fraccionaria de la potencia armónica de la tercera fase */
#define URMSCT1LSB 0xE9   /* Parte fraccionaria del valor eficaz de tensión de la primera fase */
#define URMSCT2LSB 0xEA   /* Parte fraccionaria del valor eficaz de tensión de la segunda fase */
#define URMSCT3LSB 0xEB   /* Parte fraccionaria del valor eficaz de tensión de la tercera fase */
#define IRMSCT1LSB 0xED   /* Parte fraccionaria del valor eficaz de corriente de la primera fase */
#define IRMSCT2LSB 0xEE   /* Parte fraccionaria del valor eficaz de corriente de la segunda fase */
#define IRMSCT3LSB 0xEF   /* Parte fraccionaria del valor eficaz de corriente de la tercera fase */

/* Direcciones de memoria para parámetros adicionales de calidad eléctrica y sistema */
#define THDNUCT1 0xF1  /* Nivel de distorsión armónica total de tensión en la primera fase */
#define THDNUCT2 0xF2  /* Nivel de distorsión armónica total de tensión en la segunda fase */
#define THDNUCT3 0xF3  /* Nivel de distorsión armónica total de tensión en la tercera fase */

#define THDNICT1 0xF5  /* Nivel de distorsión armónica total de corriente en la primera fase */
#define THDNICT2 0xF6  /* Nivel de distorsión armónica total de corriente en la segunda fase */
#define THDNICT3 0xF7  /* Nivel de distorsión armónica total de corriente en la tercera fase */
#define Freq 0xF8      /* Medición de la frecuencia de la línea eléctrica */
#define PAngleCT1 0xF9 /* Ángulo medio de fase para la primera línea */
#define PAngleCT2 0xFA /* Ángulo medio de fase para la segunda línea */
#define PAngleCT3 0xFB /* Ángulo medio de fase para la tercera línea */
#define Temp 0xFC      /* Medición de la temperatura interna del circuito integrado */
#define UangleCT1 0xFD /* Ángulo de fase de la tensión para la primera línea */
#define UangleCT2 0xFE /* Ángulo de fase de la tensión para la segunda línea */
#define UangleCT3 0xFF /* Ángulo de fase de la tensión para la tercera línea */

/* Direcciones de memoria asociadas al análisis de Fourier y extracción de armónicos */
#define DftScale 0x1D0 /* Registro para escalar los resultados de la transformada discreta */
#define DftCtrl  0x1D1 /* Registro de control para iniciar y detener el análisis armónico */

#define HarmQty 30     /* Cantidad de órdenes armónicos procesados por el motor matemático */

#define AIHR2 0x100    /* Registro base del resultado armónico de corriente de la primera fase */
#define BIHR2 0x120    /* Registro base del resultado armónico de corriente de la segunda fase */
#define CIHR2 0x140    /* Registro base del resultado armónico de corriente de la tercera fase */
#define AVHR2 0x160    /* Registro base del resultado armónico de tensión de la primera fase */
#define BVHR2 0x180    /* Registro base del resultado armónico de tensión de la segunda fase */
#define CVHR2 0x1A0    /* Registro base del resultado armónico de tensión de la tercera fase */

#define AITHD 0x11F    /* Resultado procesado de distorsión armónica de corriente en la primera fase */
#define BITHD 0x13F    /* Resultado procesado de distorsión armónica de corriente en la segunda fase */
#define CITHD 0x15F    /* Resultado procesado de distorsión armónica de corriente en la tercera fase */
#define AVTHD 0x17F    /* Resultado procesado de distorsión armónica de tensión en la primera fase */
#define BVTHD 0x19F    /* Resultado procesado de distorsión armónica de tensión en la segunda fase */
#define CVTHD 0x1BF    /* Resultado procesado de distorsión armónica de tensión en la tercera fase */

#define AIFUND 0x1C0   /* Resultado fundamental puro de la corriente en la primera fase */
#define AVFUND 0x1C1   /* Resultado fundamental puro de la tensión en la primera fase */
#define BIFUND 0x1C2   /* Resultado fundamental puro de la corriente en la segunda fase */
#define BVFUND 0x1C3   /* Resultado fundamental puro de la tensión en la segunda fase */
#define CIFUND 0x1C4   /* Resultado fundamental puro de la corriente en la tercera fase */
#define CVFUND 0x1C5   /* Resultado fundamental puro de la tensión en la tercera fase */


/* 
  Clase principal que define el comportamiento del medidor de energía.
  Contiene las propiedades y métodos para gestionar la comunicación, calibrar el hardware
  y abstraer la lectura de registros en valores flotantes comprensibles.
*/
class ATM90E36
{
protected:
    /* Rutina central que ejecuta transferencias SPI directas hacia y desde el chip */
    unsigned short CommEnergyIC(unsigned char RW, unsigned short address, unsigned short val);
    
    /* Variables de instancia ocultas que conservan los valores de configuración en memoria */
    int _cs;
    unsigned short _lineFreq;
    unsigned short _pgagain;
    unsigned short _uGainCT1;
    unsigned short _uGainCT2;
    unsigned short _uGainCT3;
    unsigned short _uOffsetCT1;
    unsigned short _uOffsetCT2;
    unsigned short _uOffsetCT3;
    unsigned short _iGainCT1;
    unsigned short _iGainCT2;
    unsigned short _iGainCT3;
    unsigned short _iOffsetCT1;
    unsigned short _iOffsetCT2;
    unsigned short _iOffsetCT3;
    unsigned short _igainN;
    unsigned short _iOffsetN;

    /* Ensambla la lectura de dos registros de dieciséis bits en una sola variable de treinta y dos bits */
    int Read32Register(signed short regh_addr, signed short regl_addr);

    /* Imprime el contenido hexadecimal de un registro a través del puerto serie */
    int ReadRegister(signed short regh_addr, signed short regl_addr, String reg_desc);

public:
    /* Constructor que prepara la instancia del medidor */
    ATM90E36(void);
    
    /* Destructor que limpia la instancia finalizada */
    ~ATM90E36(void);

    /* Rutina pública que inyecta todos los parámetros iniciales en el circuito integrado */
    void begin(unsigned short lineFreq, unsigned short pgagain, 
               unsigned short uGainCT1, unsigned short uGainCT2, unsigned short uGainCT3, 
               unsigned short uOffsetCT1, unsigned short uOffsetCT2, unsigned short uOffsetCT3,
               unsigned short iGainCT1, unsigned short iGainCT2, unsigned short iGainCT3, 
               unsigned short iOffsetCT1, unsigned short iOffsetCT2, unsigned short iOffsetCT3,
               unsigned short igainN, unsigned short iOffsetN);

    /* Métodos de calibración dinámica para corregir variaciones en los sensores analógicos */
    int16_t CalculateVIOffset(unsigned short regh_addr, unsigned short regl_addr);
    uint16_t CalculatePowerOffset(unsigned short regh_addr, unsigned short regl_addr, unsigned short offset_reg);
    double CalibrateVI(unsigned short reg, unsigned short actualVal);

    /* Funciones públicas para recuperar los valores eléctricos fundamentales de la red */
    double GetLineVoltage1();
    double GetLineVoltage2();
    double GetLineVoltage3();

    double GetLineCurrentCT1();
    double GetLineCurrentCT2();
    double GetLineCurrentCT3();
    double GetLineCurrentCTN();

    double GetActivePowerCT1();
    double GetActivePowerCT2();
    double GetActivePowerCT3();
    double GetTotalActivePower();

    double GetActiveFundamentalPowerCT1();
    double GetActiveFundamentalPowerCT2();
    double GetActiveFundamentalPowerCT3();
    double GetTotalActiveFundamentalPower();

    double GetActiveHarmonicPowerCT1();
    double GetActiveHarmonicPowerCT2();
    double GetActiveHarmonicPowerCT3();
    double GetTotalActiveHarmonicPower();

    double GetReactivePowerCT1();
    double GetReactivePowerCT2();
    double GetReactivePowerCT3();
    double GetTotalReactivePower();

    double GetApparentPowerCT1();
    double GetApparentPowerCT2();
    double GetApparentPowerCT3();
    double GetTotalApparentPower();

    double GetFrequency();

    double GetPowerFactorCT1();
    double GetPowerFactorCT2();
    double GetPowerFactorCT3();
    double GetTotalPowerFactor();

    double GetVHarmCT1();
    double GetVHarmCT2();
    double GetVHarmCT3();

    double GetCHarmCT1();
    double GetCHarmCT2();
    double GetCHarmCT3();

    double GetPhaseCT1();
    double GetPhaseCT2();
    double GetPhaseCT3();

    double GetTemperature();

    /* Permite solicitar de manera directa cualquier valor bruto almacenado en el chip */
    double GetValueRegister(unsigned short registerRead);

    /* Extraen los acumuladores procesados de consumo o inyección de energía */
    double GetForwardActiveEnergyCT1();
    double GetReverseActiveEnergyCT1();
    double GetForwardReactiveEnergyCT1();
    double GetReverseReactiveEnergyCT1();

    /* Proveen información de diagnóstico interna del medidor */
    unsigned short GetSysStatus0();
    unsigned short GetSysStatus1();
    unsigned short GetMeterStatus0();
    unsigned short GetMeterStatus1();

    unsigned short GetRegisters();

    /* Activa y controla el motor matemático para el procesamiento digital de señales armónicas */
    void RunHarmonicsEngine();

    /* Recuperan en bloques completos las magnitudes espectrales procesadas por el motor matemático */
    void GetHarmonicsVoltage1(float* harmonicsArray);
    void GetHarmonicsVoltage2(float* harmonicsArray);
    void GetHarmonicsVoltage3(float* harmonicsArray);
    void GetHarmonicsCurrent1(float* harmonicsArray);
    void GetHarmonicsCurrent2(float* harmonicsArray);
    void GetHarmonicsCurrent3(float* harmonicsArray);

    double GetFundamentalVoltage1();
    double GetFundamentalVoltage2();
    double GetFundamentalVoltage3();
    double GetFundamentalCurrent1();
    double GetFundamentalCurrent2();
    double GetFundamentalCurrent3();

    /* Verifica si los registros internos del circuito presentan anomalías que afecten la lectura */
    bool calibrationError();

};
#endif