using System;
using System.Collections.Generic;
using System.Linq;

namespace OffBalance {
  public class Balancer
  {
    private readonly StockApiClient _stockClient;

    public Balancer(StockApiClient stockClient) {
      _stockClient = stockClient;
    }

    public (List<ActionResult>, Portfolio) Balance(Portfolio currentPortfolio, Portfolio requestedPortfolio, DateTime date) {
      var stocksOfInterest = currentPortfolio.Stocks.Select(x => x.Symbol)
        .Union(requestedPortfolio.Stocks.Select(x => x.Symbol))
        .ToList();
      var prices = _stockClient.GetPrices(stocksOfInterest, date);

      var actions = new List<ActionResult>();

      var stocksToRemove =
        currentPortfolio.Stocks.Where(x => !requestedPortfolio.Stocks.Select(r => r.Symbol).Contains(x.Symbol));
      actions.AddRange(
        stocksToRemove.Select(stock => new ActionResult(stock.Symbol, stock.Shares, stock.Shares * -1, 0)));

      var totalValue =
        currentPortfolio.Stocks.Sum(x => x.Shares * prices[x.Symbol]);

      foreach (var stock in requestedPortfolio.Stocks) {
        var price = prices[stock.Symbol];
        var percentage = stock.AllocationPercentage / 100;
        var newShares = Convert.ToInt32(percentage * totalValue / price);
        var actualPercentage = newShares * price / totalValue;
        var originalShares = currentPortfolio.Stocks.FirstOrDefault(x => x.Symbol.Equals(stock.Symbol))?.Shares ?? 0;
        newShares = newShares - originalShares;
        actions.Add(new ActionResult(stock.Symbol, originalShares, newShares, actualPercentage * 100));
      }

      var updatedPortfolio = new Portfolio(actions
        .Select(x => new PortfolioStock(x.Symbol, x.OriginalShares + x.Shares, x.AllocationPercentage))
        .Where(x => x.AllocationPercentage > 0)
        .ToList());

      return (actions, updatedPortfolio);
    }
  }

  public class Portfolio
  {
    public Portfolio(List<PortfolioStock> stocks) {
      Stocks = stocks;
    }
    public List<PortfolioStock> Stocks { get; }
  }

  public class PortfolioStock
  {
    public PortfolioStock(string symbol, int shares) {
      Symbol = symbol;
      Shares = shares;
    }
    public PortfolioStock(string symbol, decimal allocationPercentage) {
      Symbol = symbol;
      AllocationPercentage = allocationPercentage;
    }
    public PortfolioStock(string symbol, int shares, decimal allocationPercentage) {
      Symbol = symbol;
      Shares = shares;
      AllocationPercentage = allocationPercentage;
    }
    public string Symbol { get; }
    public int Shares { get; set; }
    public decimal AllocationPercentage { get; set; }
  }

  public class ActionResult
  {
    public ActionResult(string symbol, int originalShares, int shares, decimal allocationPercentage) {
      Symbol = symbol;
      OriginalShares = originalShares;
      Shares = shares;
      AllocationPercentage = allocationPercentage;
    }

    public string Symbol { get; }
    public int OriginalShares { get; set; }
    public int Shares { get; }
    public decimal AllocationPercentage { get; }
  }

}