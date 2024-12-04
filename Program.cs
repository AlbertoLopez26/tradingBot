using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using Alpaca.Markets;
using Microsoft.VisualBasic;

class Program
{
    static async Task Main(string[] args)
    {
        // Rutas de carpetas y archivos
        string buyDirectory = @"C:\Trading\alerts\compra";
        string sellDirectory = @"C:\Trading\alerts\venta";
        string assetsPath = @"C:\Trading\assets.txt";

        // Si no existen crearlos
        if (!Directory.Exists(buyDirectory))
        {
            Directory.CreateDirectory(buyDirectory);
            Directory.CreateDirectory(sellDirectory);
        }

        // Si no existe el archivo de texto para las acciones crearlo
        if (!File.Exists(assetsPath))
        {
            File.Create(assetsPath);
        }

        // Credenciales para el uso de la api
        string apiKey = "PK006K12UPB7S7B470CU";
        string apiSecret = "wIzVu3ZPPD1RoFt3ELcgKenV6TRsMhzfuJgpUUn7";

        // Este cliente sirve para colocar ordenes de compra venta Etc.
        var alpacaClient = Environments.Paper.GetAlpacaTradingClient(new SecretKey(apiKey, apiSecret));
        // Este cliente sirve para solictar datos historicos.
        var historicalClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(apiKey, apiSecret));

        // leer archivo con acciones a analizar y crear una lista con ellas
        List<string> assetList = [.. File.ReadLines(assetsPath)];

        // requerido para usar funciones
        var func = new Program();

        // El la EMA larga es de 50 dias mientras que la corta es de 12 dias
        // el periodo para el rsi es de 14 dias.
        int longPeriod = 50;
        int shortPeriod = 12;
        int periodosRSI = 14;

        // Ciclo para el analisis de cada una de las acciones ubicadas en el archivo assets.txt
        foreach (string asset in assetList)
        {
            /* 
            Se requieren 100 datos de cierre para calcular la sma con los primeros 50
            y con los ultimos 50 calcular la EMA 
            */

            //obtener 100 datos de cierre
            int initialPeriod = longPeriod * 3;
            var startDate = DateTime.Now.AddDays(-initialPeriod);
            var endDate = DateTime.Now.AddMinutes(-16);
            var historicalBars = await historicalClient.ListHistoricalBarsAsync(new HistoricalBarsRequest(asset, startDate, endDate, BarTimeFrame.Day));
            var closingPrices = historicalBars.Items.Select(bar => bar.Close).ToList();

            //Ciclo para asegurar por lo menos 100 valores
            while (closingPrices.Count < longPeriod * 2)
            {
                initialPeriod += 10;
                startDate = DateTime.Now.AddDays(-initialPeriod);
                historicalBars = await historicalClient.ListHistoricalBarsAsync(new HistoricalBarsRequest(asset, startDate, endDate, BarTimeFrame.Day));
                closingPrices = historicalBars.Items.Select(bar => bar.Close).ToList();
            }

            //obtener las SMAs
            decimal longSMA = func.calculateSMA(closingPrices, longPeriod);
            decimal shortSMA = func.calculateSMA(closingPrices, shortPeriod);

            //obtener las EMAs
            decimal longEMA = func.calculateEMA(longSMA, longPeriod, closingPrices);
            decimal shortEMA = func.calculateEMA(shortSMA, shortPeriod, closingPrices);

            //obtener el RSI
            decimal rsi = func.calculateRSI(closingPrices, periodosRSI);

            /* 
            - Si la EMA corta esta por debajo de la EMA larga y el RSI por debajo de 20: 
            indica que el precio esta debajo del promedio y hay sobreventa del activo.
            Oportunidad para COMPRAR

            - Si la EMA corta esta por arriba de la EMA larga y el RSI por arriba del 80:
            indica que el precio esta arriba del promedio y hay sobrecompra del activo.
            Oportunidad para VENDER  
            */

            if (shortEMA < longEMA && rsi < 20)
            {
                // Acciones para alertas de compra
                Console.WriteLine($"Oportunidad de compra para {asset}");
            }
            else if (shortEMA > longEMA && rsi > 80)
            {
                // Acciones para alerta de venta
                Console.WriteLine($"Oportunidad de venta para {asset}");
            }
            else
            {
                // Acciones para ninguna alerta
                Console.WriteLine($"Ninguna oportunidad por el momento para {asset}");
            }
        }
    }
    public decimal calculateSMA(List<decimal> closingPrices, int periodos, int indiceDeComienzo = 0)
    {
        decimal suma = 0;
        for (int i = 0; i < periodos; i++)
        {
            suma += closingPrices[closingPrices.Count - (periodos * 2) + i];
        }
        return suma / periodos;
    }

    public decimal calculateEMA(decimal sma, int periodos, List<decimal> closingPrices)
    {
        decimal alpha = 10m / (periodos + 1m);
        decimal ema = sma;

        for (int i = 0; i < periodos; i++)
        {
            ema = (alpha * closingPrices[closingPrices.Count - periodos + i]) + ((1 - alpha) * ema);
        }

        return ema;
    }

    public decimal calculateRSI(List<decimal> closingPrices, int periodos)
    {
        decimal ganancia = 0;
        decimal perdida = 0;
        decimal mediaG;
        decimal mediaP;
        decimal variacion;

        for (int i = 0; i < periodos - 1; i++)
        {
            variacion = closingPrices[closingPrices.Count - periodos + 1 + i] - closingPrices[closingPrices.Count - periodos + i];

            if (variacion > 0)
            {
                ganancia += variacion;
            }
            else if (variacion < 0)
            {
                perdida += Math.Abs(variacion);
            };
        }

        mediaG = ganancia / periodos;
        mediaP = perdida / periodos;

        return 100 - 100 / (1 + mediaG / mediaP);
    }
}