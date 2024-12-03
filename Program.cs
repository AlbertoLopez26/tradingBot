﻿using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using Alpaca.Markets;
using Microsoft.VisualBasic;

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

        //requerido para usar funciones
        var func = new Program();

        /* 
        Se requieren 100 datos de cierre para calcular la sma con los primeros 50
        y con los ultimos 50 calcular la EMA 
        */

        int longEMA = 50;

        //obtener 100 datos de cierre
        int initialPeriod = longEMA*3;
        var startDate = DateTime.Now.AddDays(-initialPeriod);
        var endDate = DateTime.Now.AddMinutes(-15);
        var historicalBars = await historicalClient.ListHistoricalBarsAsync(new HistoricalBarsRequest(asset, startDate, endDate, BarTimeFrame.Day));
        var closingPrices = historicalBars.Items.Select(bar => bar.Close).ToList();
        
        //Ciclo para asegurar por lo menos 100 valores
        while (closingPrices.Count < longEMA*2){
            initialPeriod += 10;
            startDate = DateTime.Now.AddDays(-initialPeriod);
            historicalBars = await historicalClient.ListHistoricalBarsAsync(new HistoricalBarsRequest(asset, startDate, endDate, BarTimeFrame.Day));
            closingPrices = historicalBars.Items.Select(bar => bar.Close).ToList();
        }

        //obtener SMA
        decimal sma = func.calculateSMA(closingPrices, longEMA);

        //obtener EMA
        decimal ema = func.calculateEMA(sma, longEMA, closingPrices);

        Console.WriteLine(asset);
        int i = 0;
        foreach(var price in closingPrices){
            i++;
            Console.WriteLine($"{i}. {price}");
        }
        Console.WriteLine(closingPrices.Count);

        
    }
    public decimal calculateSMA(List<decimal> closingPrices, int periodos, int indiceDeComienzo = 0)
    {
        decimal suma = 0;
        for (int i = 0; i < periodos; i++)
        {
            suma += closingPrices[indiceDeComienzo + i];
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
}