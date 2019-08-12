using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace OffBalance
{
  public class RebalancingRequestedTest
  {
    private readonly ITestOutputHelper _output;
    private readonly Mock<StockApiClient> _stockClient;
    public RebalancingRequestedTest(ITestOutputHelper output) {
      this._output = output;
      _stockClient = new Mock<StockApiClient>();
    }

    [Fact]
    public void Simple() {
      var today = DateTime.Parse("1/1/2019");
      var stockPrices = new List<(string stock, decimal price)> {("ONE", 10), ("TWO", 10), ("THREE", 10)};
      SetupStockPrices(stockPrices, today);

      var balancer = new Balancer(_stockClient.Object);
      var currentPortfolio = new Portfolio(new List<PortfolioStock> {
        new PortfolioStock(symbol:"ONE", shares:1),
        new PortfolioStock(symbol:"TWO", shares:1)
      });
      var requestedPortfolio = new Portfolio(new List<PortfolioStock> {
        new PortfolioStock(symbol:"ONE", allocationPercentage: 50),
        new PortfolioStock(symbol:"THREE", allocationPercentage: 50)
      });

      var (actions, updatedPortfolio) = balancer.Balance(currentPortfolio, requestedPortfolio, today);

      WriteOutput(actions, updatedPortfolio);

      Assert.Equal(3, actions.Count);
      AssertActionForStock(actions, "ONE", expectedPercentage: 50, shareDiff: 0);
      AssertActionForStock(actions, "TWO", expectedPercentage: 0, shareDiff: -1);
      AssertActionForStock(actions, "THREE", expectedPercentage: 50, shareDiff: 1);

      Assert.Equal(2, updatedPortfolio.Stocks.Count);
      AssertPortfolioStock(updatedPortfolio, "ONE", expectedPercentage:50, expectedShares:1);
      AssertPortfolioStock(updatedPortfolio, "THREE", expectedPercentage:50, expectedShares:1);

    }

    [Fact]
    public void More_complex() {
      var today = DateTime.Parse("1/1/2019");
      var stockPrices = new List<(string stock, decimal price)> {
        ("AAPL", 195.85m),
        ("GOOG", 1169.59m),
        ("CYBR", 127.65m),
        ("ABB", 17.88m),
        ("GFN", 700.51m),
        ("ACAD", 28.64m)
      };
      SetupStockPrices(stockPrices, today);

      var balancer = new Balancer(_stockClient.Object);
      var currentPortfolio = new Portfolio(new List<PortfolioStock> {
        new PortfolioStock(symbol:"AAPL", shares:50),
        new PortfolioStock(symbol:"GOOG", shares:200),
        new PortfolioStock(symbol:"CYBR", shares:150),
        new PortfolioStock(symbol:"ABB", shares:900)
      });
      var requestedPortfolio = new Portfolio(new List<PortfolioStock> {
        new PortfolioStock(symbol:"AAPL", allocationPercentage: 22),
        new PortfolioStock(symbol:"GOOG", allocationPercentage: 38),
        new PortfolioStock(symbol:"GFN", allocationPercentage: 25),
        new PortfolioStock(symbol:"ACAD", allocationPercentage: 15)
      });

      var (actions, updatedPortfolio) = balancer.Balance(currentPortfolio, requestedPortfolio, today);
      WriteOutput(actions, updatedPortfolio);

      Assert.Equal(6, actions.Count);
      AssertActionForStock(actions, "AAPL", expectedPercentage: 22, shareDiff: 263);
      AssertActionForStock(actions, "GOOG", expectedPercentage: 38, shareDiff: -109);
      AssertActionForStock(actions, "CYBR", expectedPercentage: 0, shareDiff: -150);
      AssertActionForStock(actions, "ABB", expectedPercentage: 0, shareDiff: -900);
      AssertActionForStock(actions, "GFN", expectedPercentage: 25, shareDiff: 9286);
      AssertActionForStock(actions, "ACAD", expectedPercentage: 15, shareDiff: 1461);

      Assert.Equal(4, updatedPortfolio.Stocks.Count);
      AssertPortfolioStock(updatedPortfolio, "AAPL", expectedPercentage: 22, expectedShares: 313);
      AssertPortfolioStock(updatedPortfolio, "GOOG", expectedPercentage: 38, expectedShares: 91);
      AssertPortfolioStock(updatedPortfolio, "GFN", expectedPercentage: 25, expectedShares: 9286);
      AssertPortfolioStock(updatedPortfolio, "ACAD", expectedPercentage: 15, expectedShares: 1461);

    }

    [Fact (Skip = "end-to-end, makes external api call")]
    //[Fact]
    public void For_real() {
      // remove Skip property and add api key below to run against live API
      var client = new StockApiClient("Api key goes here");
      var balancer = new Balancer(client);
      var currentPortfolio = new Portfolio(new List<PortfolioStock> {
        new PortfolioStock(symbol: "AAPL", shares: 50),
        new PortfolioStock(symbol: "GOOG", shares: 200),
        new PortfolioStock(symbol: "CYBR", shares: 150),
        new PortfolioStock(symbol: "ABB", shares: 900)
      });
      var requestedPortfolio = new Portfolio(new List<PortfolioStock> {
        new PortfolioStock(symbol: "AAPL", allocationPercentage: 22),
        new PortfolioStock(symbol: "GOOG", allocationPercentage: 38),
        new PortfolioStock(symbol: "GFN", allocationPercentage: 25),
        new PortfolioStock(symbol: "ACAD", allocationPercentage: 15)
      });
      var today = DateTime.Now.Date;
      var (actions, updatedPortfolio) = balancer.Balance(currentPortfolio, requestedPortfolio, today);

      WriteOutput(actions, updatedPortfolio);
    }

    private void WriteOutput(List<ActionResult> actions, Portfolio updatedPortfolio) {
      foreach (var action in actions.OrderBy(x => x.Shares)) {
        var verb = action.Shares > 0 ? "buy" : "sell";
        _output.WriteLine($"{action.Symbol}: {verb} {Math.Abs(action.Shares)} shares");
      }

      _output.WriteLine("");
      foreach (var stock in updatedPortfolio.Stocks.OrderByDescending(x => x.AllocationPercentage)) {
        _output.WriteLine($"{stock.Symbol}, {stock.Shares} shares, {Math.Round(stock.AllocationPercentage, 0)}%");
      }
    }

    private void SetupStockPrices(List<(string stock, decimal price)> stockPrices, DateTime today) {
      var stocks = stockPrices.Select(x => x.stock);
      var returnValue = stockPrices.ToDictionary(x => x.stock, x => x.price);
      _stockClient.Setup(x => x.GetPrices(stocks, today)).Returns(returnValue);
    }

    private static void AssertActionForStock(IEnumerable<ActionResult> result, string symbol, decimal expectedPercentage,
      int shareDiff) {
      var stock = result.SingleOrDefault(x => x.Symbol == symbol);
      Assert.NotNull(stock);
      Assert.Equal(expectedPercentage, Math.Round(stock.AllocationPercentage, 0));
      Assert.Equal(shareDiff, stock.Shares);
    }

    private static void AssertPortfolioStock(Portfolio updatedPortfolio, string symbol, decimal expectedPercentage,
      int expectedShares) {
      var stock = updatedPortfolio.Stocks.SingleOrDefault(x => x.Symbol == symbol);
      Assert.NotNull(stock);
      Assert.Equal(expectedPercentage, Math.Round(stock.AllocationPercentage, 0));
      Assert.Equal(expectedShares, stock.Shares);
    }

  }
}