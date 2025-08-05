using Elavon.Requests;
using Elavon.Response;
using MaktabDataContracts.Models;

namespace Elavon.Services
{
    public interface IElavonService
    {
        Task<ElavonTransferMoneyResponse> TransferMoney(ElavonTransferMoneyRequest transferMoney);
    }
}
