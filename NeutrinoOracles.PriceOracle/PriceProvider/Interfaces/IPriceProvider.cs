using System.Threading.Tasks;

namespace NeutrinoOracles.PriceOracle.PriceProvider.Interfaces
{
    public interface IPriceProvider
    {
        int Weight { get; }
        Task<decimal> GetPrice();
    }
}