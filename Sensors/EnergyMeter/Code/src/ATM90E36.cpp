/*
  Clase para el control y lectura del circuito integrado ATM90E36.
  El código base ha sido adaptado exclusivamente para este dispositivo,
  incorporando funcionalidades como la lectura de armónicos de tensión y corriente.
  Gestiona la comunicación SPI, la calibración de sensores y el cálculo
  de múltiples parámetros eléctricos en redes polifásicas.
*/

#include <ATM90E36.h>

/*
  Constructor de la clase.
  Prepara la instancia para su uso posterior.
*/
ATM90E36::ATM90E36(void)
{
  // Inicia la instancia
}

/*
  Destructor de la clase.
  Libera la instancia una vez finalizado su ciclo de vida.
*/
ATM90E36::~ATM90E36()
{
  // Finaliza la instancia
}

/*
  Establece la comunicación principal con el circuito integrado.
  Aplica máscaras a los registros, maneja la transmisión y recepción a través
  del bus SPI y devuelve el valor correspondiente al registro procesado.
*/
unsigned short ATM90E36::CommEnergyIC(unsigned char RW, unsigned short address, unsigned short val)
{
  unsigned char *data = (unsigned char *)&val;
  unsigned char *adata = (unsigned char *)&address;
  unsigned short output;
  unsigned short address1;

  // Configura la velocidad, el orden de los bits y el modo del bus SPI
  SPISettings settings(200000, MSBFIRST, SPI_MODE3);

  // Intercambia el byte más significativo con el menos significativo
  output = (val >> 8) | (val << 8);
  val = output;

  // Aplica la bandera de lectura o escritura a la dirección
  address |= RW << 15;

  // Intercambia los bytes de la dirección
  address1 = (address >> 8) | (address << 8);
  address = address1;

  // Inicia la transacción de datos
  SPI.beginTransaction(settings);

  // Habilita el chip y espera el tiempo requerido para su activación
  digitalWrite(CS_ATM, LOW);
  delayMicroseconds(10);

  // Escribe la dirección de memoria byte por byte
  for (byte i = 0; i < 2; i++)
  {
    SPI.transfer(*adata);
    adata++;
  }

  /* Aplica una espera obligatoria para validar los datos */
  delayMicroseconds(4);

  // Procesa la lectura o escritura según la operación solicitada
  if (RW)
  {
    for (byte i = 0; i < 2; i++)
    {
      *data = SPI.transfer(0x00);
      data++;
    }
  }
  else
  {
    for (byte i = 0; i < 2; i++)
    {
      SPI.transfer(*data);
      data++;
    }
  }

  // Deshabilita el chip y espera la finalización del proceso
  digitalWrite(CS_ATM, HIGH);
  delayMicroseconds(10);

  SPI.endTransaction();

  // Revierte el orden de los bytes para retornar el valor correcto
  output = (val >> 8) | (val << 8);
  return output;
}

/*
  Realiza la lectura de registros de treinta y dos bits.
  Lee la parte alta y la parte baja por separado y luego las concatena
  para formar y retornar un único valor numérico.
*/
int ATM90E36::Read32Register(signed short regh_addr, signed short regl_addr)
{
  int val, val_h, val_l;
  val_h = CommEnergyIC(READ, regh_addr, 0xFFFF);
  val_l = CommEnergyIC(READ, regl_addr, 0xFFFF);
  val = CommEnergyIC(READ, regh_addr, 0xFFFF);

  // Desplaza la parte alta y la une con la baja
  val = val_h << 16;
  val |= val_l; 

  return (val);
}

/*
  Calcula la compensación de error para voltaje o corriente.
  Lee los registros inferiores y obtiene un valor de desplazamiento.
  Debe ejecutarse únicamente cuando los transformadores están conectados al medidor
  pero sin rodear cables con carga.
*/
int16_t ATM90E36::CalculateVIOffset(unsigned short regh_addr, unsigned short regl_addr)
{
  uint32_t val, val_h, val_l;
  uint16_t offset;
  val_h = CommEnergyIC(READ, regh_addr, 0xFFFF);
  val_l = CommEnergyIC(READ, regl_addr, 0xFFFF);
  val = CommEnergyIC(READ, regh_addr, 0xFFFF);

  // Combina registros y elimina los bits menos significativos ignorados por el hardware
  val = val_h << 16;
  val |= val_l;      
  val = val >> 7;    
  
  // Aplica el complemento a dos
  val = (~val) + 1;  

  // Conserva únicamente los dieciséis bits inferiores
  offset = val; 
  return int16_t(offset);
}

/*
  Calcula la compensación de error para la potencia.
  Lee los registros asociados y guarda el resultado directamente en el circuito.
  Requiere las mismas condiciones de vacío que la compensación de voltaje.
*/
uint16_t ATM90E36::CalculatePowerOffset(unsigned short regh_addr, unsigned short regl_addr, unsigned short offset_reg)
{
  uint32_t val, val_h, val_l;
  uint16_t offset;
  val_h = CommEnergyIC(READ, regh_addr, 0xFFFF);
  val_l = CommEnergyIC(READ, regl_addr, 0xFFFF);
  val = CommEnergyIC(READ, regh_addr, 0xFFFF);

  // Combina registros
  val = val_h << 16; 
  val |= val_l;      
  
  // Aplica el complemento a dos
  val = (~val) + 1;  

  offset = val; 
  // Escribe la compensación calculada en el registro correspondiente
  CommEnergyIC(WRITE, offset_reg, (signed short)val);
  return uint16_t(offset);
}

/*
  Ejecuta una calibración en base a un valor de referencia conocido.
  Toma múltiples muestras del registro, calcula un nuevo factor de ganancia
  y lo escribe en la memoria del dispositivo.
*/
double ATM90E36::CalibrateVI(unsigned short reg, unsigned short actualVal)
{
  uint16_t gain, val, m, gainReg;
  
  // Realiza varias lecturas consecutivas para obtener una muestra estable
  val = CommEnergyIC(READ, reg, 0xFFFF);
  val += CommEnergyIC(READ, reg, 0xFFFF);
  val += CommEnergyIC(READ, reg, 0xFFFF);
  val += CommEnergyIC(READ, reg, 0xFFFF);

  // Asigna el registro de ganancia adecuado según el canal evaluado
  switch (reg)
  {
  case URMSCT1:
  {
    gainReg = UGainCT1;
  }
  case URMSCT2:
  {
    gainReg = UGainCT2;
  }
  case URMSCT3:
  {
    gainReg = UGainCT3;
  }
  case IRMSCT1:
  {
    gainReg = IGainCT1;
  }
  case IRMSCT2:
  {
    gainReg = IGainCT2;
  }
  case IRMSCT3:
  {
    gainReg = IGainCT3;
  }
  }

  // Recupera la ganancia actual, calcula la proporción y define el nuevo valor
  gain = CommEnergyIC(READ, gainReg, 0xFFFF);
  m = actualVal;
  m = ((m * gain) / val);
  gain = m;

  // Guarda el ajuste en el circuito
  CommEnergyIC(WRITE, gainReg, gain);

  return (gain);
}

/*
  Evalúa el estado del sistema en busca de errores de calibración.
  Revisa el registro de estado y verifica cada suma de comprobación interna.
  Retorna verdadero si encuentra alguna discrepancia.
*/
bool ATM90E36::calibrationError()
{
  bool CS0, CS1, CS2, CS3;
  unsigned short systemstatus0 = GetSysStatus0();

  if (systemstatus0 & 0x4000)
  {
    CS0 = true;
  }
  else
  {
    CS0 = false;
  }

  if (systemstatus0 & 0x1000)
  {
    CS1 = true;
  }
  else
  {
    CS1 = false;
  }
  if (systemstatus0 & 0x0400)
  {
    CS2 = true;
  }
  else
  {
    CS2 = false;
  }
  if (systemstatus0 & 0x0100)
  {
    CS3 = true;
  }
  else
  {
    CS3 = false;
  }

  Serial.print("Checksum 0: ");
  Serial.print(CS0);
  Serial.print("\tChecksum 1: ");
  Serial.println(CS1);
  Serial.print("Checksum 2: ");
  Serial.print(CS2);
  Serial.print("\tChecksum 3: ");
  Serial.println(CS3);

  if (CS0 || CS1 || CS2 || CS3)
    return (true);
  else
    return (false);
}


/*
  Obtiene la tensión de la primera fase.
  Extrae la parte entera y la fraccionaria de los registros asociados,
  combina los resultados y retorna el valor en voltios.
*/
double ATM90E36::GetLineVoltage1()
{
  unsigned short high = CommEnergyIC(READ, URMSCT1, 0xFFFF);
  unsigned short lowReg = CommEnergyIC(READ, URMSCT1LSB, 0xFFFF); 
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullVoltageRaw = (double)high + ((double)lowValid / 256.0);
  
  return fullVoltageRaw / 100.0;
}

/*
  Obtiene la tensión de la segunda fase utilizando la misma lógica de cálculo.
*/
double ATM90E36::GetLineVoltage2()
{
  unsigned short high = CommEnergyIC(READ, URMSCT2, 0xFFFF);
  unsigned short lowReg = CommEnergyIC(READ, URMSCT2LSB, 0xFFFF); 
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullVoltageRaw = (double)high + ((double)lowValid / 256.0);
  
  return fullVoltageRaw / 100.0;
}

/*
  Obtiene la tensión de la tercera fase utilizando la misma lógica de cálculo.
*/
double ATM90E36::GetLineVoltage3()
{
  unsigned short high = CommEnergyIC(READ, URMSCT3, 0xFFFF);
  unsigned short lowReg = CommEnergyIC(READ, URMSCT3LSB, 0xFFFF); 
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullVoltageRaw = (double)high + ((double)lowValid / 256.0);
  
  return fullVoltageRaw / 100.0;
}

/*
  Obtiene la corriente de la primera fase.
  Une la porción de miliamperios enteros con sus fracciones y
  devuelve el valor convertido a amperios.
*/
double ATM90E36::GetLineCurrentCT1()
{
  unsigned short high = CommEnergyIC(READ, IRMSCT1, 0xFFFF);
  unsigned short lowReg = CommEnergyIC(READ, IRMSCT1LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullCurrentRaw = (double)high + ((double)lowValid / 256.0);
  
  return fullCurrentRaw / 1000.0;
}

/*
  Obtiene la corriente de la segunda fase utilizando la misma lógica.
*/
double ATM90E36::GetLineCurrentCT2()
{
  unsigned short high = CommEnergyIC(READ, IRMSCT2, 0xFFFF);
  unsigned short lowReg = CommEnergyIC(READ, IRMSCT2LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullCurrentRaw = (double)high + ((double)lowValid / 256.0);
  
  return fullCurrentRaw / 1000.0;
}

/*
  Obtiene la corriente de la tercera fase utilizando la misma lógica.
*/
double ATM90E36::GetLineCurrentCT3()
{
  unsigned short high = CommEnergyIC(READ, IRMSCT3, 0xFFFF);
  unsigned short lowReg = CommEnergyIC(READ, IRMSCT3LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullCurrentRaw = (double)high + ((double)lowValid / 256.0);
  
  return fullCurrentRaw / 1000.0;
}

/*
  Obtiene la corriente circulante por el cable neutro.
*/
double ATM90E36::GetLineCurrentCTN()
{
  unsigned short current = CommEnergyIC(READ, IRMSN, 0xFFFF);
  return (double)current / 1000;
}

/*
  Calcula la potencia activa en la primera fase.
  Extrae e integra el valor entero y fraccionario para obtener los vatios reales.
*/
double ATM90E36::GetActivePowerCT1()
{
  short high = (short)CommEnergyIC(READ, PmeanCT1, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanCT1LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Calcula la potencia activa en la segunda fase.
*/
double ATM90E36::GetActivePowerCT2()
{
  short high = (short)CommEnergyIC(READ, PmeanCT2, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanCT2LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Calcula la potencia activa en la tercera fase.
*/
double ATM90E36::GetActivePowerCT3()
{
  short high = (short)CommEnergyIC(READ, PmeanCT3, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanCT3LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Recupera la potencia activa total del sistema.
  Aplica un multiplicador específico dictado por la arquitectura del chip
  sobre los registros combinados.
*/
double ATM90E36::GetTotalActivePower()
{
  short high = (short)CommEnergyIC(READ, PmeanT, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanTLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = 4 * ((double)high + ((double)lowValid / 256.0));
  
  return fullPower;
}

/*
  Retorna la potencia fundamental activa de la primera fase,
  excluyendo el componente armónico.
*/
double ATM90E36::GetActiveFundamentalPowerCT1()
{
  short high = (short)CommEnergyIC(READ, PmeanCT1F, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanCT1FLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Retorna la potencia fundamental activa de la segunda fase.
*/
double ATM90E36::GetActiveFundamentalPowerCT2()
{
  short high = (short)CommEnergyIC(READ, PmeanCT2F, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanCT2FLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Retorna la potencia fundamental activa de la tercera fase.
*/
double ATM90E36::GetActiveFundamentalPowerCT3()
{
  short high = (short)CommEnergyIC(READ, PmeanCT3F, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanCT3FLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Calcula la sumatoria de las potencias fundamentales de todas las fases.
*/
double ATM90E36::GetTotalActiveFundamentalPower()
{
  short high = (short)CommEnergyIC(READ, PmeanTF, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanTFLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = 4 * ((double)high + ((double)lowValid / 256.0));
  
  return fullPower;
}

/*
  Recupera la potencia activa atribuible únicamente a los armónicos en la primera fase.
*/
double ATM90E36::GetActiveHarmonicPowerCT1()
{
  short high = (short)CommEnergyIC(READ, PmeanCT1H, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanCT1HLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Recupera la potencia activa armónica en la segunda fase.
*/
double ATM90E36::GetActiveHarmonicPowerCT2()
{
  short high = (short)CommEnergyIC(READ, PmeanCT2H, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanCT2HLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Recupera la potencia activa armónica en la tercera fase.
*/
double ATM90E36::GetActiveHarmonicPowerCT3()
{
  short high = (short)CommEnergyIC(READ, PmeanCT3H, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanCT3HLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Obtiene la cantidad total de potencia activa originada por distorsiones armónicas en el sistema.
*/
double ATM90E36::GetTotalActiveHarmonicPower()
{
  short high = (short)CommEnergyIC(READ, PmeanTH, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, PmeanTHLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = 4 * ((double)high + ((double)lowValid / 256.0));
  
  return fullPower;
}

/*
  Extrae la potencia reactiva de la primera fase.
*/
double ATM90E36::GetReactivePowerCT1()
{
  short high = (short)CommEnergyIC(READ, QmeanCT1, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, QmeanCT1LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Extrae la potencia reactiva de la segunda fase.
*/
double ATM90E36::GetReactivePowerCT2()
{
  short high = (short)CommEnergyIC(READ, QmeanCT2, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, QmeanCT2LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Extrae la potencia reactiva de la tercera fase.
*/
double ATM90E36::GetReactivePowerCT3()
{
  short high = (short)CommEnergyIC(READ, QmeanCT3, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, QmeanCT3LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Calcula el monto global de potencia reactiva presente en el sistema.
*/
double ATM90E36::GetTotalReactivePower()
{
  short high = (short)CommEnergyIC(READ, QmeanT, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, QmeanTLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = 4 * ((double)high + ((double)lowValid / 256.0));
  
  return fullPower;
}

/*
  Obtiene la medición de potencia aparente en la primera fase.
*/
double ATM90E36::GetApparentPowerCT1()
{
  short high = (short)CommEnergyIC(READ, SmeanCT1, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, SmeanCT1LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Obtiene la medición de potencia aparente en la segunda fase.
*/
double ATM90E36::GetApparentPowerCT2()
{
  short high = (short)CommEnergyIC(READ, SmeanCT2, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, SmeanCT2LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Obtiene la medición de potencia aparente en la tercera fase.
*/
double ATM90E36::GetApparentPowerCT3()
{
  short high = (short)CommEnergyIC(READ, SmeanCT3, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, SmeanCT3LSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = (double)high + ((double)lowValid / 256.0);
  
  return fullPower;
}

/*
  Calcula el total de la potencia aparente entregada a la carga combinada.
*/
double ATM90E36::GetTotalApparentPower()
{
  short high = (short)CommEnergyIC(READ, SAmeanT, 0xFFFF);
  unsigned short lowReg = (unsigned short)CommEnergyIC(READ, SAmeanTLSB, 0xFFFF);
  
  unsigned char lowValid = (unsigned char)(lowReg >> 8);
  double fullPower = 4 * ((double)high + ((double)lowValid / 256.0));
  
  return fullPower;
}

/*
  Lee la frecuencia de la línea eléctrica medida por el circuito.
*/
double ATM90E36::GetFrequency()
{
  unsigned short freq = CommEnergyIC(READ, Freq, 0xFFFF);
  return (double)freq / 100;
}

/*
  Consulta el factor de potencia calculado para la primera fase.
*/
double ATM90E36::GetPowerFactorCT1()
{
  signed short pf = (signed short)CommEnergyIC(READ, PFmeanCT1, 0xFFFF);
  return (double)pf / 1000;
}

/*
  Consulta el factor de potencia calculado para la segunda fase.
*/
double ATM90E36::GetPowerFactorCT2()
{
  signed short pf = (signed short)CommEnergyIC(READ, PFmeanCT2, 0xFFFF);
  return (double)pf / 1000;
}

/*
  Consulta el factor de potencia calculado para la tercera fase.
*/
double ATM90E36::GetPowerFactorCT3()
{
  signed short pf = (signed short)CommEnergyIC(READ, PFmeanCT3, 0xFFFF);
  return (double)pf / 1000;
}

/*
  Consulta el factor de potencia global de la instalación.
*/
double ATM90E36::GetTotalPowerFactor()
{
  signed short pf = (signed short)CommEnergyIC(READ, PFmeanT, 0xFFFF);
  return (double)pf / 1000;
}

/*
  Mide el nivel de distorsión armónica total presente en el voltaje de la primera fase.
*/
double ATM90E36::GetVHarmCT1()
{
  unsigned short value = CommEnergyIC(READ, THDNUCT1, 0xFFFF);
  return (double)value / 100;
}

/*
  Mide el nivel de distorsión armónica total presente en el voltaje de la segunda fase.
*/
double ATM90E36::GetVHarmCT2()
{
  unsigned short value = CommEnergyIC(READ, THDNUCT2, 0xFFFF);
  return (double)value / 100;
}

/*
  Mide el nivel de distorsión armónica total presente en el voltaje de la tercera fase.
*/
double ATM90E36::GetVHarmCT3()
{
  unsigned short value = CommEnergyIC(READ, THDNUCT3, 0xFFFF);
  return (double)value / 100;
}

/*
  Mide la tasa de distorsión armónica detectada en la corriente de la primera fase.
*/
double ATM90E36::GetCHarmCT1()
{
  unsigned short value = CommEnergyIC(READ, THDNICT1, 0xFFFF);
  return (double)value / 100;
}

/*
  Mide la tasa de distorsión armónica detectada en la corriente de la segunda fase.
*/
double ATM90E36::GetCHarmCT2()
{
  unsigned short value = CommEnergyIC(READ, THDNICT2, 0xFFFF);
  return (double)value / 100;
}

/*
  Mide la tasa de distorsión armónica detectada en la corriente de la tercera fase.
*/
double ATM90E36::GetCHarmCT3()
{
  unsigned short value = CommEnergyIC(READ, THDNICT3, 0xFFFF);
  return (double)value / 100;
}

/*
  Obtiene el ángulo de fase de la primera línea eléctrica.
*/
double ATM90E36::GetPhaseCT1()
{
  short angleA = (short)CommEnergyIC(READ, PAngleCT1, 0xFFFF);
  return (double)angleA / 10;
}

/*
  Obtiene el ángulo de fase de la segunda línea eléctrica.
*/
double ATM90E36::GetPhaseCT2()
{
  short angleB = (short)CommEnergyIC(READ, PAngleCT2, 0xFFFF);
  return (double)angleB / 10;
}

/*
  Obtiene el ángulo de fase de la tercera línea eléctrica.
*/
double ATM90E36::GetPhaseCT3()
{
  short angleC = (short)CommEnergyIC(READ, PAngleCT3, 0xFFFF);
  return (double)angleC / 10;
}

/*
  Entrega la medición del sensor térmico interno del procesador de energía.
*/
double ATM90E36::GetTemperature()
{
  short int atemp = (short int)CommEnergyIC(READ, Temp, 0xFFFF);
  return (double)atemp;
}

/*
  Función utilitaria que lee y retorna el valor de cualquier registro
  pasando directamente la dirección de memoria solicitada.
*/
double ATM90E36::GetValueRegister(unsigned short registerRead)
{
  return (double)CommEnergyIC(READ, registerRead, 0xFFFF); 
}

/*
  Calcula el incremento de energía activa consumida en sentido directo.
  Lee los pulsos generados, verifica la resolución actual y aplica la
  constante del medidor para devolver un valor en unidades estándar.
*/
double ATM90E36::GetForwardActiveEnergyCT1()
{
  const double METER_CONSTANT_IMP_KWH = 3200.0; 

  unsigned short mMode0 = CommEnergyIC(READ, MMode0, 0xFFFF);
  
  // Analiza el bit correspondiente para definir la resolución
  double resolutionCF = (mMode0 & 0x0200) ? 0.01 : 0.1;

  // Consulta el acumulador de energía, el cual se reinicia al leerlo
  unsigned short rawPulses = CommEnergyIC(READ, APenergyCT1, 0xFFFF);
    
  if (rawPulses > 0) {
    double totalCFPulses = (double)rawPulses * resolutionCF;
    double delta = totalCFPulses / METER_CONSTANT_IMP_KWH;

    return delta;
  }

  return 0.0;
}

/*
  Calcula el incremento de energía activa inyectada o devuelta a la red.
  Utiliza el mismo principio de pulsos y resolución que la lectura directa.
*/
double ATM90E36::GetReverseActiveEnergyCT1()
{
  const double METER_CONSTANT_IMP_KWH = 3200.0; 

  unsigned short mMode0 = CommEnergyIC(READ, MMode0, 0xFFFF);
  
  double resolutionCF = (mMode0 & 0x0200) ? 0.01 : 0.1;

  unsigned short rawPulses = CommEnergyIC(READ, ANenergyCT1, 0xFFFF);
    
  if (rawPulses > 0) {
    double totalCFPulses = (double)rawPulses * resolutionCF;
    double delta = totalCFPulses / METER_CONSTANT_IMP_KWH;

    return delta;
  }

  return 0.0;
}

/*
  Obtiene el flujo acumulado de energía reactiva en sentido directo.
*/
double ATM90E36::GetForwardReactiveEnergyCT1()
{
  const double METER_CONSTANT_IMP_KWH = 3200.0; 

  unsigned short mMode0 = CommEnergyIC(READ, MMode0, 0xFFFF);
  
  double resolutionCF = (mMode0 & 0x0200) ? 0.01 : 0.1;

  unsigned short rawPulses = CommEnergyIC(READ, RPenergyCT1, 0xFFFF);
    
  if (rawPulses > 0) {
    double totalCFPulses = (double)rawPulses * resolutionCF;
    double delta = totalCFPulses / METER_CONSTANT_IMP_KWH;

    return delta;
  }

  return 0.0;
}

/*
  Obtiene el flujo acumulado de energía reactiva inversa en el circuito.
*/
double ATM90E36::GetReverseReactiveEnergyCT1()
{
  const double METER_CONSTANT_IMP_KWH = 3200.0; 

  unsigned short mMode0 = CommEnergyIC(READ, MMode0, 0xFFFF);
  
  double resolutionCF = (mMode0 & 0x0200) ? 0.01 : 0.1;

  unsigned short rawPulses = CommEnergyIC(READ, RNenergyCT1, 0xFFFF);
    
  if (rawPulses > 0) {
    double totalCFPulses = (double)rawPulses * resolutionCF;
    double delta = totalCFPulses / METER_CONSTANT_IMP_KWH;

    return delta;
  }

  return 0.0;
}

/*
  Inicia el motor de análisis matemático para los componentes armónicos.
  Detiene el sistema temporalmente, ajusta las configuraciones de escala y ventana,
  reinicia la recolección de datos y aguarda el tiempo necesario para la conversión.
*/
void ATM90E36::RunHarmonicsEngine() {
    CommEnergyIC(WRITE, DftCtrl, 0x000); 
    
    CommEnergyIC(WRITE, DftScale, 0x0000); 

    CommEnergyIC(WRITE, DftCtrl, 0x001); 

    delay(550); 
}

/*
  Puebla un arreglo con las lecturas armónicas de voltaje para la primera fase.
  Convierte los valores en crudo utilizando el factor de división establecido.
*/
void ATM90E36::GetHarmonicsVoltage1(float* harmonicsArray) {
    for (int i = 0; i < HarmQty; i++) {
        unsigned short raw = CommEnergyIC(READ, AVHR2 + i, 0xFFFF);
        harmonicsArray[i] = (float)raw / 163.84;
    }
} 

/*
  Puebla un arreglo con las lecturas armónicas de voltaje para la segunda fase.
*/
void ATM90E36::GetHarmonicsVoltage2(float* harmonicsArray) {
    for (int i = 0; i < HarmQty; i++) {
        unsigned short raw = CommEnergyIC(READ, BVHR2 + i, 0xFFFF);
        harmonicsArray[i] = (float)raw / 163.84;
    }
} 

/*
  Puebla un arreglo con las lecturas armónicas de voltaje para la tercera fase.
*/
void ATM90E36::GetHarmonicsVoltage3(float* harmonicsArray) {
    for (int i = 0; i < HarmQty; i++) {
        unsigned short raw = CommEnergyIC(READ, CVHR2 + i, 0xFFFF);
        harmonicsArray[i] = (float)raw / 163.84;
    }
} 

/*
  Transfiere los datos de corriente armónica de la primera fase al arreglo proporcionado.
*/
void ATM90E36::GetHarmonicsCurrent1(float* harmonicsArray) {
    for (int i = 0; i < HarmQty; i++) {
        unsigned short raw = CommEnergyIC(READ, AIHR2 + i, 0xFFFF);
        harmonicsArray[i] = (float)raw / 163.84;
    }
}

/*
  Transfiere los datos de corriente armónica de la segunda fase al arreglo proporcionado.
*/
void ATM90E36::GetHarmonicsCurrent2(float* harmonicsArray) {
    for (int i = 0; i < HarmQty; i++) {
        unsigned short raw = CommEnergyIC(READ, BIHR2 + i, 0xFFFF);
        harmonicsArray[i] = (float)raw / 163.84;
    }
}

/*
  Transfiere los datos de corriente armónica de la tercera fase al arreglo proporcionado.
*/
void ATM90E36::GetHarmonicsCurrent3(float* harmonicsArray) {
    for (int i = 0; i < HarmQty; i++) {
        unsigned short raw = CommEnergyIC(READ, CIHR2 + i, 0xFFFF);
        harmonicsArray[i] = (float)raw / 163.84;
    }
}

/*
  Retorna la amplitud de la tensión fundamental calculada para la fase uno.
*/
double ATM90E36::GetFundamentalVoltage1()
{
  unsigned short value = CommEnergyIC(READ, AVFUND, 0xFFFF);
  return (double)value * 0.032656;
}

/*
  Retorna la amplitud de la tensión fundamental calculada para la fase dos.
*/
double ATM90E36::GetFundamentalVoltage2()
{
  unsigned short value = CommEnergyIC(READ, BVFUND, 0xFFFF);
  return (double)value * 0.032656;
}

/*
  Retorna la amplitud de la tensión fundamental calculada para la fase tres.
*/
double ATM90E36::GetFundamentalVoltage3()
{
  unsigned short value = CommEnergyIC(READ, CVFUND, 0xFFFF);
  return (double)value * 0.032656;
}

/*
  Retorna la magnitud de la corriente fundamental calculada para la fase uno.
*/
double ATM90E36::GetFundamentalCurrent1()
{
  unsigned short value = CommEnergyIC(READ, AIFUND, 0xFFFF);
  return (double)value * 0.0032656;
}

/*
  Retorna la magnitud de la corriente fundamental calculada para la fase dos.
*/
double ATM90E36::GetFundamentalCurrent2()
{
  unsigned short value = CommEnergyIC(READ, BIFUND, 0xFFFF);
  return (double)value * 0.0032656;
}

/*
  Retorna la magnitud de la corriente fundamental calculada para la fase tres.
*/
double ATM90E36::GetFundamentalCurrent3()
{
  unsigned short value = CommEnergyIC(READ, CIFUND, 0xFFFF);
  return (double)value * 0.0032656;
}

/*
  Consulta el primer registro general de estado del sistema eléctrico.
*/
unsigned short ATM90E36::GetSysStatus0()
{
  return CommEnergyIC(READ, SysStatus0, 0xFFFF);
}

/*
  Consulta el segundo registro general de estado del sistema eléctrico.
*/
unsigned short ATM90E36::GetSysStatus1()
{
  return CommEnergyIC(READ, SysStatus1, 0xFFFF);
}

/*
  Comprueba el registro primario que indica el estado interno del medidor.
*/
unsigned short ATM90E36::GetMeterStatus0()
{
  return CommEnergyIC(READ, EnStatus0, 0xFFFF);
}

/*
  Comprueba el registro secundario que indica el estado interno del medidor.
*/
unsigned short ATM90E36::GetMeterStatus1()
{
  return CommEnergyIC(READ, EnStatus1, 0xFFFF);
}

/*
  Rutina de configuración e inicialización de parámetros.
  Asigna en memoria las ganancias de los sensores, ajusta el hardware
  SPI e inyecta en los registros del chip todos los factores de calibración 
  que definen las umbrales operacionales y métricas eléctricas base.
*/
void ATM90E36::begin(unsigned short lineFreq, unsigned short pgagain, 
                     unsigned short uGainCT1, unsigned short uGainCT2, unsigned short uGainCT3, 
                     unsigned short uOffsetCT1, unsigned short uOffsetCT2, unsigned short uOffsetCT3,
                     unsigned short iGainCT1, unsigned short iGainCT2, unsigned short iGainCT3, 
                     unsigned short iOffsetCT1, unsigned short iOffsetCT2, unsigned short iOffsetCT3,
                     unsigned short igainN, unsigned short iOffsetN)
{
  _lineFreq = lineFreq; 
  _pgagain = pgagain;   

#if ATM_SINGLEVOLTAGE == true
  _uGainCT1 = uGainCT1; 
  _uGainCT2 = uGainCT1; 
  _uGainCT3 = uGainCT1; 
  _uOffsetCT1 = uOffsetCT1; 
  _uOffsetCT2 = uOffsetCT1; 
  _uOffsetCT3 = uOffsetCT1; 
#else
  _uGainCT1 = uGainCT1; 
  _uGainCT2 = uGainCT2; 
  _uGainCT3 = uGainCT3; 
  _uOffsetCT1 = uOffsetCT1; 
  _uOffsetCT2 = uOffsetCT2; 
  _uOffsetCT3 = uOffsetCT3; 
#endif

  _iGainCT1 = iGainCT1; 
  _iGainCT2 = iGainCT2; 
  _iGainCT3 = iGainCT3; 
  _igainN = igainN;     
  _iOffsetCT1 = iOffsetCT1; 
  _iOffsetCT2 = iOffsetCT2; 
  _iOffsetCT3 = iOffsetCT3; 
  _iOffsetN = iOffsetN;     

  pinMode(CS_ATM, OUTPUT);

  // Inicia la transmisión de datos SPI
  SPI.begin(SCLK, MISO, MOSI); 

  Serial.print("Connecting to the ");
  Serial.println("ATM90E36");
  Serial.println("====================================");

  // Estima los límites para la detección de caídas de tensión y anomalías de frecuencia
  unsigned short vSagTh;
  unsigned short sagV;
  unsigned short FreqHiThresh;
  unsigned short FreqLoThresh;
  if (_lineFreq == 4485 || _lineFreq == 5231)
  {
    sagV = 90;
    FreqHiThresh = 61 * 100;
    FreqLoThresh = 59 * 100;
  }
  else
  {
    sagV = 190;
    FreqHiThresh = 51 * 100;
    FreqLoThresh = 49 * 100;
  }

  vSagTh = (sagV * 100 * sqrt(2)) / (2 * _uGainCT1 / 32768);

  // Restaura el procesador de señales a su estado inicial
  CommEnergyIC(WRITE, SoftReset, 0x789A); 

  // Configura parámetros para monitorear depresiones de voltaje
  CommEnergyIC(WRITE, FuncEn0, 0x0000); 
  CommEnergyIC(WRITE, FuncEn1, 0x0000); 
  CommEnergyIC(WRITE, SagTh, 0x0001);   

  // Transfiere datos base para la operación de medición
  CommEnergyIC(WRITE, ConfigStart, 0x5678); 
  CommEnergyIC(WRITE, PLconstH, 0x0861);    
  CommEnergyIC(WRITE, PLconstL, 0xC468);    
  CommEnergyIC(WRITE, MMode0, _lineFreq);   
  CommEnergyIC(WRITE, MMode1, _pgagain);    
  CommEnergyIC(WRITE, PStartTh, 0x0000);    
  CommEnergyIC(WRITE, QStartTh, 0x0000);    
  CommEnergyIC(WRITE, SStartTh, 0x0000);    
  CommEnergyIC(WRITE, PPhaseTh, 0x0000);    
  CommEnergyIC(WRITE, QPhaseTh, 0x0000);    
  CommEnergyIC(WRITE, SPhaseTh, 0x0000);    
  CommEnergyIC(WRITE, CSZero, 0x4741);      

  // Carga valores compensatorios dentro del bloque principal de registros
  CommEnergyIC(WRITE, CalStart, 0x5678); 

  CommEnergyIC(WRITE, PoffsetCT1, 0x0000); 
  CommEnergyIC(WRITE, QoffsetCT1, 0x0000); 
  CommEnergyIC(WRITE, PoffsetCT2, 0x0000); 
  CommEnergyIC(WRITE, QoffsetCT2, 0x0000); 
  CommEnergyIC(WRITE, PoffsetCT3, 0x0000); 
  CommEnergyIC(WRITE, QoffsetCT3, 0x0000); 
  CommEnergyIC(WRITE, GainCT1, 0x0000);    
  CommEnergyIC(WRITE, PhiCT1, 0x0000);     
  CommEnergyIC(WRITE, GainCT2, 0x0000);    
  CommEnergyIC(WRITE, PhiCT2, 0x0000);     
  CommEnergyIC(WRITE, GainCT3, 0x0000);    
  CommEnergyIC(WRITE, PhiCT3, 0x0000);     

  CommEnergyIC(WRITE, CSOne, 0x0000); 

  // Configura ajustes complementarios referidos a análisis de componentes fundamentales
  CommEnergyIC(WRITE, HaRMStart, 0x5678);   
  CommEnergyIC(WRITE, PoffsetCT1F, 0x0000); 
  CommEnergyIC(WRITE, PoffsetCT2F, 0x0000); 
  CommEnergyIC(WRITE, PoffsetCT3F, 0x0000); 
  CommEnergyIC(WRITE, PGainCT1F, 0x0000);   
  CommEnergyIC(WRITE, PGainCT2F, 0x0000);   
  CommEnergyIC(WRITE, PGainCT3F, 0x0000);   
  CommEnergyIC(WRITE, CSTwo, 0x0000);       

  // Transfiere ganancias finales correspondientes a transformadores
  CommEnergyIC(WRITE, AdjStart, 0x5678); 

  CommEnergyIC(WRITE, UGainCT1, _uGainCT1); 
  CommEnergyIC(WRITE, IGainCT1, _iGainCT1); 
  CommEnergyIC(WRITE, UoffsetCT1, _uOffsetCT1);  
  CommEnergyIC(WRITE, IoffsetCT1, _iOffsetCT1);  
  
  CommEnergyIC(WRITE, UGainCT2, _uGainCT2); 
  CommEnergyIC(WRITE, IGainCT2, _iGainCT2); 
  CommEnergyIC(WRITE, UoffsetCT2, _uOffsetCT2);  
  CommEnergyIC(WRITE, IoffsetCT2, _iOffsetCT2);  
  
  CommEnergyIC(WRITE, UGainCT3, _uGainCT3); 
  CommEnergyIC(WRITE, IGainCT3, _iGainCT3); 
  CommEnergyIC(WRITE, UoffsetCT3, _uOffsetCT3);  
  CommEnergyIC(WRITE, IoffsetCT3, _iOffsetCT3);  
  
  CommEnergyIC(WRITE, IgainN, _igainN); 
  CommEnergyIC(WRITE, IoffsetN, _iOffsetN);  

  CommEnergyIC(WRITE, CSThree, 0x02F6); 

  Serial.print("ATM90E36");
  Serial.println(" Connected - OK");
}

/*
  Imprime en el terminal el contenido de un registro acompañado de una breve descripción.
  Prepara la lectura y envía los resultados formateados en hexadecimal.
*/
int ATM90E36::ReadRegister(signed short regh_addr, signed short regl_addr, String reg_desc)
{

  Serial.print("0x");
  if (regh_addr < 0x10)
    Serial.print('0');
  Serial.print(regh_addr, HEX);

  Serial.print("\t\t");

  Serial.print(Read32Register(regh_addr, 0xFFFF), HEX);

  Serial.print("\t\t\t" + reg_desc);
  Serial.print("\n\n");

  return (0);
}

/*
  Ejecuta una impresión demostrativa de un registro específico a modo de depuración.
*/
unsigned short ATM90E36::GetRegisters()
{

  Serial.println("ATM Specific Registers");
  Serial.println("Address\t\tValue\t\tDescription");

  int val = 0;
  val = ReadRegister(0x30, 0xffff, "#");

  return (0);
}