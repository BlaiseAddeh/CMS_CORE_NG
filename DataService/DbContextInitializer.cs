using System;
using System.Linq;
using System.Threading.Tasks;
using FunctionalService;

namespace DataService
{
    public static class DbContextInitializer
    {
        public static async Task Initialize(DataProtectionKeysContext dataProtectionKeysContext,
                                             ApplicationDbContext applicationDbContext,
                                             IFunctionalSvc functionalSvc)
        {
            // check if db DataProtectionKeysContext is created
            // check if db ApplicationDbContext is created

            await dataProtectionKeysContext.Database.EnsureCreatedAsync();
            await applicationDbContext.Database.EnsureCreatedAsync();

            // check if db contains any users, If db is not empty, the db has been already seeded
            if (applicationDbContext.ApplicationUsers.Any())
            {
                return;
            }

            //If empty, create Admin User and App User
            await functionalSvc.CreateDefaultAdminUser();
            await functionalSvc.CreateDefaultUser();

        }
    }
}
