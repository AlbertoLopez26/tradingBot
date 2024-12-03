using System.ComponentModel;
using Alpaca.Markets;

class Program
{
    static async Task Main(string[] args)
    {
        // Credenciales para el uso de la api
        var apiKey = "PK006K12UPB7S7B470CU";
        var apiSecret = "wIzVu3ZPPD1RoFt3ELcgKenV6TRsMhzfuJgpUUn7";

        // Este cliente sirve para colocar ordenes de compra venta Etc.
        var alpacaClient = Environments.Paper.GetAlpacaTradingClient(new SecretKey(apiKey, apiSecret));
        // Este cliente sirve para solictar datos historicos.
        var historicalClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(apiKey, apiSecret));

        //accion a evaluar
        string asset = "TSLA";

        //obtener 50 datos de cierre
        var startDate = DateTime.Now.AddDays(-60);
        var endDate = DateTime.Now.AddMinutes(-15);
        var historicalBars = await historicalClient.ListHistoricalBarsAsync(new HistoricalBarsRequest(asset, startDate, endDate, BarTimeFrame.Day));
        var closingPrices = historicalBars.Items.Select(bar => bar.Close).ToList();
        int initialPeriod = 60;
        while (closingPrices.Count < 50){
            initialPeriod += 10;
            startDate = DateTime.Now.AddDays(-initialPeriod);
            historicalBars = await historicalClient.ListHistoricalBarsAsync(new HistoricalBarsRequest(asset, startDate, endDate, BarTimeFrame.Day));
            closingPrices = historicalBars.Items.Select(bar => bar.Close).ToList();
        }

        Console.WriteLine(asset);
        int i = 0;
        foreach(var price in closingPrices){
            i++;
            Console.WriteLine($"{i}. {price}");
        }
        Console.WriteLine(closingPrices.Count);

        
    }

}