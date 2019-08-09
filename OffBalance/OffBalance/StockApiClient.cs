using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;

namespace OffBalance {
  public class StockApiClient
  {
    private readonly string _apiKey;
    public StockApiClient() {}
    public StockApiClient(string apiKey) {
      _apiKey = apiKey;
    }

    public virtual Dictionary<string, decimal> GetPrices(IEnumerable<string> stocks, DateTime date) {
      var prices = new Dictionary<string, decimal>();

      // quick and dirty throttling since can only make 5 API calls per minute with AlphaVantage
      foreach (var stock in stocks) {
        prices[stock] = GetStockPrice(stock, date);
        Thread.Sleep(60000 / 5);
      }
      return prices;
    }

    private decimal GetStockPrice(string symbol, DateTime date) {
      var client = new HttpClient();
      var url = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={symbol}&outputsize=compact&apikey={_apiKey}";
      var response = client.GetAsync(url).Result;
      var stockData = JsonConvert.DeserializeObject<ApiStock>(response.Content.ReadAsStringAsync().Result);

      var searchDate = date.ToString("yyyy-MM-dd");
      if (!stockData.TimeSeries.ContainsKey(searchDate))
        throw new Exception($"No data found for {symbol} on {searchDate}");
      return stockData.TimeSeries[searchDate]["4. close"];
    }
  }

  public class ApiStock
  {
    public ApiStock(decimal closePrice, DateTime date) {
      var dateKey = date.ToString("yyyy-MM-dd");
      TimeSeries[dateKey] = new Dictionary<string, decimal> {
        {"4. close", closePrice }
      };
    }

    [JsonProperty(PropertyName = "Time Series (Daily)")]
    public Dictionary<string, Dictionary<string, decimal>> TimeSeries { get; set; } =
      new Dictionary<string, Dictionary<string, decimal>>();
  }
}