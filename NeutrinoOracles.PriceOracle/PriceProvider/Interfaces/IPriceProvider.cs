using System.Threading.Tasks;

namespace NeutrinoOracles.PriceOracle.PriceProvider.Interfaces
{
    public interface IPriceProvider
    {
        Task<decimal> GetPrice();
    }
}