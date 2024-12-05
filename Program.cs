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
        string apiKey = "apiKey";
        string apiSecret = "apiSecret";

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
                // 1. Generar nombre de archivo donde se va a agregar la informacion de compra.
                string buyAlertPath = $"{buyDirectory}" + @"\" + $"{asset}.txt";
                // 2. Obtener la ultima cotizacion.
                var lastQuote = await historicalClient.GetLatestQuoteAsync(new LatestMarketDataRequest(asset));
                // 3. llamar a la funcion que crea el archivo de texto.
                func.createAlertTxt(buyAlertPath, asset, "buy", closingPrices[closingPrices.Count-1], lastQuote.AskPrice);
            }
            else if (shortEMA > longEMA && rsi > 80)
            {
                // Acciones para alertas de compra
                // 1. Generar nombre de archivo donde se va a agregar la informacion de compra.
                string sellAlertPath = $"{sellDirectory}" + @"\" + $"{asset}.txt";
                // 2. Obtener la ultima cotizacion.
                var lastQuote = await historicalClient.GetLatestQuoteAsync(new LatestMarketDataRequest(asset));
                // 3. llamar a la funcion que crea el archivo de texto.
                func.createAlertTxt(sellAlertPath, asset, "sell", closingPrices[closingPrices.Count-1], lastQuote.BidPrice);
            }
        }
    }

    // Funcion para calcular la media movil simple
    public decimal calculateSMA(List<decimal> closingPrices, int periodos, int indiceDeComienzo = 0)
    {
        decimal suma = 0;
        for (int i = 0; i < periodos; i++)
        {
            suma += closingPrices[closingPrices.Count - (periodos * 2) + i];
        }
        return suma / periodos;
    }

    // Funcion para calcular la media movil exponencial
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

    // Funcion para calcular el RSI
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


    // Funcion para crear el archivo de texto para cada una de las acciones en  las que se presente una alerta
    public void createAlertTxt(string alertPath, string asset, string buyOrSell, decimal closingPrices, decimal lastQuote)
    {
        // Si el archivo no existe se crea y se le da un formato para hacer mas entendible la informacion
        if(!File.Exists(alertPath))
        {
            File.AppendAllText(alertPath,"--------------------------------------------------------------------------------------\n");
            File.AppendAllText(alertPath,$"------------------------------------------{asset}----------------------------------------\n");
            File.AppendAllText(alertPath,"--------------------------------------------------------------------------------------\n");
            File.AppendAllText(alertPath,"Oportunity".PadRight(20));
            File.AppendAllText(alertPath,"Last Closing".PadRight(20));
            File.AppendAllText(alertPath,"ask/bid".PadRight(20));
            File.AppendAllText(alertPath,"Date".PadRight(20));
            File.AppendAllText(alertPath,"\n");
        }

        /*
        Se verifica si la alerta es de compra o venta, en ambos casos se plasma informacion
        como el tipo de alerta, el ultimo precio de cierre, el ultimo precio ofertado o demandado
        y por ultimo la fecha.
        */

        // Si la alerta es de compra se indica y se obtiene el ultimpo precio demandado.
        if(buyOrSell == "buy")
        {
            File.AppendAllText(alertPath,"Buy".PadRight(20));
            File.AppendAllText(alertPath,$"{closingPrices}".PadRight(20));
            File.AppendAllText(alertPath,$"{lastQuote}".PadRight(20));
            File.AppendAllText(alertPath,$"{DateTime.Now}".PadRight(20));
            File.AppendAllText(alertPath,"\n");
        }
        // Si la alerta es de venta se indica y se obtiene el ultimo precio ofertado.
        else if(buyOrSell == "sell")
        {
            File.AppendAllText(alertPath,"Sell".PadRight(20));
            File.AppendAllText(alertPath,$"{closingPrices}".PadRight(20));
            File.AppendAllText(alertPath,$"{lastQuote}".PadRight(20));
            File.AppendAllText(alertPath,$"{DateTime.Now}".PadRight(20));
            File.AppendAllText(alertPath,"\n");
        }
    }
}
