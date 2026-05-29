import os
import pandas as pd
from influxdb_client import InfluxDBClient
import plotly.graph_objects as go
from plotly.subplots import make_subplots

URL = "http://192.168.80.50:8086"  
TOKEN = "7LwCyFbTTzZG4a6WIL-NfMD8Xu8u8FgBqGvd2EMEvf6it51pDmKytNz78T00VLcBEDteLg6cS9quDELvLB8z-w=="
ORG = "ceia"
BUCKET = "energy_meters"
SENSOR_NAME = "energy-meters" 

def energy_meter_load_influx(
    start_time: str, 
    stop_time: str, 
    serial_number: str,
    subdirectory: str,
    csv_filename: str
):
    """
    Se conecta a InfluxDB utilizando las credenciales globales, descarga los datos 
    de telemetría del medidor, los limpia y los guarda en un archivo CSV local.
    """
    
    # INICIALIZACIÓN DEL CLIENTE
    client = InfluxDBClient(url=URL, token=TOKEN, org=ORG, timeout=120000)
    query_api = client.query_api()

    # CONSULTA FLUX
    # Utiliza las variables globales para definir el bucket y la medición
    flux_query = f'''
        from(bucket: "{BUCKET}")
            |> range(start: {start_time}, stop: {stop_time})
            |> filter(fn: (r) => r["_measurement"] == "{SENSOR_NAME}")
            |> filter(fn: (r) => r["deviceId"] == "{serial_number}")
            |> pivot(rowKey:["_time"], columnKey: ["_field"], valueColumn: "_value")
    '''

    print(f"Descargando datos de InfluxDB para el dispositivo {serial_number}...")

    try:
        # EJECUCIÓN DE LA CONSULTA
        df_result = query_api.query_data_frame(query=flux_query, org=ORG)

        if df_result.empty:
            print("No se encontraron datos para los parámetros especificados.")
            return

        # UNIFICACIÓN DE FRAGMENTOS
        # Concatena los resultados si la api devuelve una lista de dataframes
        if isinstance(df_result, list):
            df_result = pd.concat(df_result, ignore_index=True)

        # LIMPIEZA DE COLUMNAS INTERNAS
        columnas_a_eliminar = ['result', 'table', '_start', '_stop', '_measurement', 'deviceId']
        df_result = df_result.drop(columns=[col for col in columnas_a_eliminar if col in df_result.columns])

        # REORDENAMIENTO DE TIMESTAMP
        df_result = df_result.rename(columns={'_time': 'timestamp'})
        columnas_ordenadas = ['timestamp'] + [col for col in df_result.columns if col != 'timestamp']
        df_result = df_result[columnas_ordenadas]

        # GESTIÓN DE DIRECTORIOS Y GUARDADO
        # Crea el directorio especificado si no existe
        if subdirectory:
            os.makedirs(subdirectory, exist_ok=True)
            
        full_path = os.path.join(subdirectory, csv_filename)
        
        df_result.to_csv(full_path, index=False)
        
        print(f"¡Datos guardados con éxito en '{full_path}'! Dimensiones: {df_result.shape[0]} filas x {df_result.shape[1]} columnas")

    except Exception as e:
        print(f"Ocurrió un error al consultar la base de datos o guardar el archivo: {e}")
        
    finally:
        # CIERRE DE CONEXIÓN
        client.close()

def energy_meter_load_csv(subdirectory: str, csv_filename: str) -> pd.DataFrame:
    """
    Carga los datos del medidor de energía desde un archivo CSV local
    y devuelve un DataFrame de Pandas listo para ser graficado.
    """

    # GESTIÓN DE RUTAS
    # Construye la ruta completa de forma segura según el sistema operativo
    full_path = os.path.join(subdirectory, csv_filename)

    print(f"Cargando datos locales desde '{full_path}'...")

    # LECTURA DE DATOS
    try:
        df = pd.read_csv(full_path)

        print(
            f"¡DataFrame cargado con éxito! Dimensiones: {df.shape[0]} filas x {df.shape[1]} columnas"
        )
        return df

    except FileNotFoundError:
        print(
            f"Error: No se encontró ningún archivo en la ruta especificada: '{full_path}'"
        )
        return pd.DataFrame()

    except Exception as e:
        print(f"Ocurrió un error al intentar leer el archivo CSV: {e}")
        return pd.DataFrame()

def energy_meter_plot_time_series(df):
    """
    Genera un panel interactivo con 7 subgráficos para analizar
    las variables eléctricas a partir de un DataFrame.
    """
    
    # ESTRUCTURA DE SUBGRÁFICOS
    fig = make_subplots(
        rows=7, cols=1,
        shared_xaxes=True, 
        vertical_spacing=0.04,
        subplot_titles=(
            "Voltaje (V)", 
            "Corriente (A)", 
            "Potencia Activa (W)", 
            "Potencia Reactiva (VAr)", 
            "Potencia Aparente (VA)",
            "Factor de Potencia",
            "Distorsión Armónica Total (%)"
        )
    )

    # AGREGADO DE TRAZOS Y DATOS
    
    # Voltaje
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['vRms'], name='vRms', mode='lines', legend="legend"), row=1, col=1)
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['vRmsFund'], name='vRmsFund', mode='lines', legend="legend"), row=1, col=1)

    # Corriente
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['iRms'], name='iRms', mode='lines', legend="legend2"), row=2, col=1)
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['iRmsFund'], name='iRmsFund', mode='lines', legend="legend2"), row=2, col=1)

    # Potencia activa
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['pActive'], name='pActive', mode='lines', legend="legend3"), row=3, col=1)
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['pActiveF'], name='pActiveF', mode='lines', legend="legend3"), row=3, col=1)
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['pActiveH'], name='pActiveH', mode='lines', legend="legend3"), row=3, col=1)

    # Potencia reactiva
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['qReactive'], name='qReactive', mode='lines', legend="legend4"), row=4, col=1)

    # Potencia aparente
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['sApparent'], name='sApparent', mode='lines', legend="legend5"), row=5, col=1)

    # Factor de potencia
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['pf'], name='pf', mode='lines', legend="legend6"), row=6, col=1)

    # Distorsión armónica total
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['thdV'], name='thdV', mode='lines', legend="legend7"), row=7, col=1)
    fig.add_trace(go.Scatter(x=df['timestamp'], y=df['thdI'], name='thdI', mode='lines', legend="legend7"), row=7, col=1)

    # CONFIGURACIÓN DE DISEÑO
    
    # Layout general y posicionamiento de leyendas
    fig.update_layout(
        template='plotly_dark',
        hovermode='x unified',
        margin=dict(t=40, b=60),
        height=1950,
        legend=dict(orientation="h", yanchor="top", y=0.87, xanchor="left", x=0.0),
        legend2=dict(orientation="h", yanchor="top", y=0.72, xanchor="left", x=0.0),
        legend3=dict(orientation="h", yanchor="top", y=0.57, xanchor="left", x=0.0),
        legend4=dict(orientation="h", yanchor="top", y=0.42, xanchor="left", x=0.0),
        legend5=dict(orientation="h", yanchor="top", y=0.27, xanchor="left", x=0.0),
        legend6=dict(orientation="h", yanchor="top", y=0.12, xanchor="left", x=0.0),
        legend7=dict(orientation="h", yanchor="top", y=-0.03, xanchor="left", x=0.0)
    )

    # AJUSTES DE EJES
    
    # Escalas y títulos del eje y
    fig.update_yaxes(range=[0, 260], title_text="Voltaje (V)", row=1, col=1)
    fig.update_yaxes(range=[0, 10], title_text="Corriente (A)", row=2, col=1)
    fig.update_yaxes(range=[0, 2500], title_text="Potencia Activa (W)", row=3, col=1)
    fig.update_yaxes(range=[0, 2500], title_text="Potencia Reactiva (VAr)", row=4, col=1)
    fig.update_yaxes(range=[0, 2500], title_text="Potencia Aparente (VA)", row=5, col=1)
    fig.update_yaxes(range=[-1.1, 1.1], title_text="PF", row=6, col=1)
    fig.update_yaxes(range=[0, 100], title_text="THD (%)", row=7, col=1) 

    # Visibilidad del eje x
    fig.update_xaxes(rangeslider_visible=False, row=7, col=1) 
    
    for i in range(1, 8):
        fig.update_xaxes(showticklabels=True, row=i, col=1)

    # RENDERIZADO
    fig.show()

def plot_harmonic_bars(df: pd.DataFrame, variable: str, target_timestamp: str):
    """
    Busca el registro más cercano al timestamp especificado y grafica 
    sus componentes armónicos (del H2 al H16) como un diagrama de barras.
    """

    # VALIDACIÓN DE VARIABLE
    variable = variable.strip().lower()
    
    if variable == "voltage":
        prefix = "dftV"
        title = "Componentes Armónicos de Voltaje"
        y_axis_title = "Amplitud (%)"
        bar_color = "#00CC96"
    elif variable == "current":
        prefix = "dftI"
        title = "Componentes Armónicos de Corriente"
        y_axis_title = "Amplitud (%)"
        bar_color = "#EF553B"
    else:
        print("Error: El parámetro 'variable' debe ser exactamente 'voltage' o 'current'.")
        return

    # BÚSQUEDA INTELIGENTE DEL TIMESTAMP MÁS CERCANO
    # Convierte las columnas a objetos datetime para permitir la resta matemática
    df['timestamp'] = pd.to_datetime(df['timestamp'])
    target_dt = pd.to_datetime(target_timestamp)

    indice_mas_cercano = (df['timestamp'] - target_dt).abs().idxmin()
    row = df.loc[indice_mas_cercano]
    
    tiempo_encontrado = row['timestamp']
    print(f"Solicitado: {target_timestamp} | Graficando el más cercano: {tiempo_encontrado}")

    # EXTRACCIÓN DE ARMÓNICOS
    columnas_armonicos = [f"{prefix}{i}" for i in range(15)]
    etiquetas_x = [f"H{i+2}" for i in range(15)] 
    
    valores_y = []
    
    for col in columnas_armonicos:
        if col in df.columns:
            valores_y.append(row[col])
        else:
            valores_y.append(0)

    # CONSTRUCCIÓN DEL GRÁFICO
    fig = go.Figure()

    fig.add_trace(go.Bar(
        x=etiquetas_x,
        y=valores_y,
        name=variable.capitalize(),
        marker_color=bar_color,
        text=[f"{val:.2f}" if val != 0 else "" for val in valores_y],
        textposition='outside' 
    ))

    # DISEÑO Y FORMATO
    fig.update_layout(
        title=f"{title}<br><sup>Timestamp analizado: {tiempo_encontrado}</sup>",
        template='plotly_dark',
        xaxis_title="Orden del Armónico",
        yaxis_title=y_axis_title,
        margin=dict(t=80, b=40, l=60, r=40),
        height=350, 
        showlegend=False 
    )

    # AJUSTES DE EJES
    fig.update_yaxes(range=[0, 100])
    
    # RENDERIZADO
    fig.show()