using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ModelService;
using Serilog;

namespace FunctionalService
{
    public class FunctionalSvc : IFunctionalSvc
    {
        private readonly AdminUserOptions _adminUserOptions;
        private readonly AppUserOptions _appUserOptions;
        private readonly UserManager<ApplicationUser> _userManager;

        public FunctionalSvc(IOptions<AppUserOptions> appUserOptions,
            IOptions<AdminUserOptions> adminUserOptions,
            UserManager<ApplicationUser> userManager)
        {
            _adminUserOptions = adminUserOptions.Value;
            _appUserOptions = appUserOptions.Value;
            _userManager = userManager;
        }


        public async Task CreateDefaultAdminUser()
        {
            try
            {
                var adminUser = new ApplicationUser
                {
                    Email = _adminUserOptions.Email,
                    UserName = _adminUserOptions.Username,
                    EmailConfirmed = true,
                    ProfilePic = GetDefaultProfilePic(),   // TODO - Upcoming video
                    PhoneNumber = "0555981945",
                    PhoneNumberConfirmed = true,
                    Firstname = _adminUserOptions.Firstname,
                    Lastname = _adminUserOptions.Lastname,
                    UserRole = "Administrator",
                    IsActive = true,
                    UserAddresses = new List<AddressModel>
                    {
                        new AddressModel { Country= _adminUserOptions.Country, Type="Billing"},
                        new AddressModel { Country= _adminUserOptions.Country, Type="Shipping"}
                    }
                };

                // Add this user to ApplicationUser Table
                var result = await _userManager.CreateAsync(adminUser, _adminUserOptions.Password);

                if (result.Succeeded)
                {
                    // Ajouter l'utilisateur au rôle Administrateur
                    await _userManager.AddToRoleAsync(adminUser, "Administrator");
                    Log.Information($"Admin User Created {adminUser.UserName}");
                }
                else
                {
                    var errorString = string.Join(",", result.Errors);
                    Log.Error($"Error while creating user { errorString }");
                }

            }
            catch (Exception ex)
            {
                Log.Error("Error while creating user {Error} {StackTrace} {InnerException}", ex.Message,
                                 ex.StackTrace, ex.InnerException, ex.Source);
            }
        }


        public async Task CreateDefaultUser()
        {
            try
            {
                var appUser = new ApplicationUser
                {
                    Email = _appUserOptions.Email,
                    UserName = _appUserOptions.Username,
                    EmailConfirmed = true,
                    ProfilePic = GetDefaultProfilePic(),   // TODO - Upcoming video
                    PhoneNumber = "0140670425",
                    PhoneNumberConfirmed = true,
                    Firstname = _appUserOptions.Firstname,
                    Lastname = _appUserOptions.Lastname,
                    UserRole = "Customer",
                    IsActive = true,
                    UserAddresses = new List<AddressModel>
                    {
                        new AddressModel { Country= _appUserOptions.Country, Type="Billing"},
                        new AddressModel { Country= _appUserOptions.Country, Type="Shipping"}
                    }
                };

                // Add this user to ApplicationUser Table
                var result = await _userManager.CreateAsync(appUser, _appUserOptions.Password);

                if (result.Succeeded)
                {
                    // Ajouter l'utilisateur au rôle Administrateur
                    await _userManager.AddToRoleAsync(appUser, "Customer");
                    Log.Information($"App User Created {appUser.UserName} ");
                }
                else
                {
                    var errorString = string.Join(",", result.Errors);
                    Log.Error($"Error while creating user { errorString }");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error while creating user {Error} {StackTrace} {InnerException}", ex.Message,
                                 ex.StackTrace, ex.InnerException, ex.Source);
            }
        }

        private string GetDefaultProfilePic()
        {
            return string.Empty;
        }

    }
}
