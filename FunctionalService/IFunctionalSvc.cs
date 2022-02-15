using System;
using System.Threading.Tasks;

namespace FunctionalService
{
    public interface IFunctionalSvc
    {
        public Task CreateDefaultAdminUser();
        public Task CreateDefaultUser();
    }
}
