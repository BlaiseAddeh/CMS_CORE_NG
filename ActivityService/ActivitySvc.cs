using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataService;
using Microsoft.EntityFrameworkCore;
using ModelService;
using Serilog;

namespace ActivityService
{
    public class ActivitySvc : IActivitySvc
    {
        private readonly ApplicationDbContext _db;

        public ActivitySvc(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task AddUserActivity(ActivityModel model)
        {
            // Gestion de la transaction sur l'opération

            await using var dbContextTransaction = await _db.Database.BeginTransactionAsync();

            try
            {

                await _db.Activities.AddAsync(model);
                await _db.SaveChangesAsync();
                await dbContextTransaction.CommitAsync();

            }
            catch (Exception ex)
            {
                Log.Error($"Une erreur est survenue lors de l'ajout dans la base de données" +
                    $"{ex.Message}, {ex.StackTrace}, {ex.InnerException}, {ex.Source}");

                await dbContextTransaction.RollbackAsync();
            }
        }

        public async Task<List<ActivityModel>> GetUserActivity(string userId)
        {
            List<ActivityModel> userActivities = new List<ActivityModel>();

            await using var dbContextTransaction = await _db.Database.BeginTransactionAsync();

            try
            {
                userActivities = await _db.Activities.Where(x => x.UserId == userId).ToListAsync();

            }
            catch (Exception ex)
            {
                Log.Error($"Une erreur est survenue lors de la recupération des données en base de données" +
                   $"{ex.Message}, {ex.StackTrace}, {ex.InnerException}, {ex.Source}");

                await dbContextTransaction.RollbackAsync();
            }

            return userActivities;
        }
    }
}
