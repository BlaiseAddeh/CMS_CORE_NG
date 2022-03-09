using System;
using System.Threading.Tasks;
using ModelService;

namespace AuthService
{
    public interface IAuthSvc
    {
        Task<TokenResponseModel> Auth(LoginViewModel model);
    }
}
