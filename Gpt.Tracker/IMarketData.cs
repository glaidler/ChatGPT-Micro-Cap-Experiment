using System.Threading.Tasks;

public interface IMarketData
{
    Task<decimal> GetLastCloseAsync(string symbol, DateOnly asOf);
}