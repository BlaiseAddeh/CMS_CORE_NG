using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ActivityService;
using CookieService;
using DataService;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelService;
using Serilog;

namespace AuthService
{
    public class AuthSvc : IAuthSvc
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppSettings _appSettings;
        private readonly ApplicationDbContext _db;
        private readonly ICookieSvc _cookieSvc;
        private readonly IServiceProvider _provider;
        private readonly DataProtectionKeys _dataProtectionKeys;
        private readonly IActivitySvc _activitySvc;

        private IDataProtector _protector;
        private string[] UserRoles = new[] { "Administrator", "Customer" };
        private TokenValidationParameters validationParameters;
        private JwtSecurityTokenHandler handler;
        private string unProtectedToken;
        private ClaimsPrincipal validateToken;



        public AuthSvc(UserManager<ApplicationUser> userManager,
        IOptions<AppSettings> appSettings, // Afin d'accéder au ppté de la class AppSettings qui ont leurs valeurs dans appsettings.json
        IOptions<DataProtectionKeys> dataProtectionKeys,
        ApplicationDbContext db, ICookieSvc cookieSvc,
        IServiceProvider provider, IActivitySvc activitySvc)
        {
            _userManager = userManager;
            _appSettings = appSettings.Value;
            _dataProtectionKeys = dataProtectionKeys.Value;
            _db = db;
            _cookieSvc = cookieSvc;
            _provider = provider;
            _activitySvc = activitySvc;

        }


        // Will be used for authenticating the Admin
        public async Task<TokenResponseModel> Auth(LoginViewModel model)
        {

            ActivityModel activityModel = new ActivityModel();
            activityModel.Date = DateTime.UtcNow;
            activityModel.IpAddress = _cookieSvc.GetUserIP();
            activityModel.Location = _cookieSvc.GetUserCountry();
            activityModel.OperatingSystem = _cookieSvc.GetUserOS();


            try
            {
                // Get the user from Database
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user == null) return CreateErrorResponseToken("Request Not Supported", HttpStatusCode.Unauthorized);

                // Get the role of the user - validate if he is admin - dont bother to go ahead if returned false
                var roles = await _userManager.GetRolesAsync(user);

                if (roles.FirstOrDefault() != "Administrator")
                {

                    activityModel.UserId = user.Id;
                    activityModel.Type = "Un-Authorized Login attempt";
                    activityModel.Icon = "fas fa-user-secret";
                    activityModel.Color = "danger";
                    await _activitySvc.AddUserActivity(activityModel);

                    Log.Error("Error: Role Not Admin");
                    return CreateErrorResponseToken("Request Not Supported", HttpStatusCode.Unauthorized);
                }

                // If user is Admin continue to execute the code
                if (!await _userManager.CheckPasswordAsync(user, model.Password))
                {

                    activityModel.UserId = user.Id;
                    activityModel.Type = "Login attempt failed";
                    activityModel.Icon = "fas fa-times-circle";
                    activityModel.Color = "danger";
                    await _activitySvc.AddUserActivity(activityModel);


                    Log.Error("Error: Invalid Password for Admin");
                    return CreateErrorResponseToken("Request Not Supported", HttpStatusCode.Unauthorized);
                }

                // The Check If Email Is confirmed
                if (!await _userManager.IsEmailConfirmedAsync(user))
                {
                    activityModel.UserId = user.Id;
                    activityModel.Type = "Login attempt success - Email Not Verified";
                    activityModel.Icon = "fas fa-envelope";
                    activityModel.Color = "wraning";
                    await _activitySvc.AddUserActivity(activityModel);

                    Log.Error($"Error: Email Not Confirmed for {user.UserName}");
                    return CreateErrorResponseToken("Email Not Confirmed", HttpStatusCode.Unauthorized);
                }



                var authToken = await GenerateNewToken(user, model);

                activityModel.UserId = user.Id;
                activityModel.Type = "Login attempt success";
                activityModel.Icon = "fas fa-thumbs-up";
                activityModel.Color = "success";
                await _activitySvc.AddUserActivity(activityModel);

                return authToken;

            }
            catch (Exception ex)
            {
                Log.Error($"Une erreur est survenue lors de l'authentification" +
                    $"{ex.Message}, {ex.StackTrace}, {ex.InnerException}, {ex.Source}");
            }

            return CreateErrorResponseToken("Request Not Supported", HttpStatusCode.Unauthorized);

        }

        // private Methods

        private async Task<TokenResponseModel> GenerateNewToken(ApplicationUser user, LoginViewModel model)
        {
            // Create a key to encrypt the Jwt
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_appSettings.Secret));

            // Get user role => check if user is admin
            var roles = await _userManager.GetRolesAsync(user);

            // Creating JWT token
            var tokenHandler = new JwtSecurityTokenHandler();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.UserName),  // Sub - Identifies principal taht issued the Jwt
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Jti - Unique identifier of the token
                    new Claim(ClaimTypes.NameIdentifier, user.Id), // Unique Identifier of the user
                    new Claim(ClaimTypes.Role, roles.FirstOrDefault()), // Role of the user
                    new Claim("LoggedOn", DateTime.Now.ToString(CultureInfo.InvariantCulture)), // Time when created
                }),

                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature), // Signature du token
                Issuer = _appSettings.Site, // Issuer - Identifies principal that issued the Jwt,
                Audience = _appSettings.Audience, // Audience - Identifies the recipients that the Jwt is idended for.
                Expires = (string.Equals(roles.FirstOrDefault(), "Administrator", StringComparison.CurrentCultureIgnoreCase)) ?
                          DateTime.UtcNow.AddMinutes(60) : DateTime.UtcNow.AddMinutes(Convert.ToDouble(_appSettings.ExpireTime))

            };

            /* Create the unique encryption key for token - 2nd layer protection */
            var encryptionKeyRt = Guid.NewGuid().ToString();
            var encryptionKeyJwt = Guid.NewGuid().ToString();

            /*Get the Data protection service instance*/
            var protectorProvider = _provider.GetService<IDataProtectionProvider>();  // Car nous utilisons: using Microsoft.Extensions.DependencyInjection;

            /* Create a protector instance */
            var protectorJwt = protectorProvider.CreateProtector(encryptionKeyJwt); // Car nous utilisons: using Microsoft.Extensions.DependencyInjection;

            /* Generate Token and Protect the user token */
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var encryptedToken = protectorJwt.Protect(tokenHandler.WriteToken(token));

            /* Create and update the token table */
            TokenModel newRtoken = new TokenModel();

            /* Create refresh token instance */
            newRtoken = CreateRefreshToken(_appSettings.ClientId, user.Id, Convert.ToInt32(_appSettings.RtExpireTime));

            /* assign the JWT encryption key */
            newRtoken.EncryptionKeyJwt = encryptionKeyJwt;

            newRtoken.EncryptionKeyRt = encryptionKeyRt;

            /* Add Refresh Token with encryption key for JWT to DB */
            try
            {
                // First, we need to check if the user has already logged in and has tokens in DB

                var rt = _db.Tokens.FirstOrDefault(t => t.UserId == user.Id);

                if (rt != null)
                {
                    // invalidate the old refresh token (by deleting it)
                    _db.Tokens.Remove(rt);

                    // Add the new refresh token
                    _db.Tokens.Add(newRtoken);
                }
                else
                {
                    await _db.Tokens.AddAsync(newRtoken);
                }

                // persist changes in the DB
                await _db.SaveChangesAsync();

            }
            catch (Exception ex)
            {
                Log.Error($"An error occured while seeding the database {ex.Message}," +
                    $"{ex.StackTrace}, {ex.InnerException}, {ex.Source}");
            }

            // Return Response containing encrypted token
            var protectorRt = protectorProvider.CreateProtector(encryptionKeyRt);
            var layerOneProtector = protectorProvider.CreateProtector(_dataProtectionKeys.ApplicationUserKey);

            var encAuthToken = new TokenResponseModel
            {
                Token = layerOneProtector.Protect(encryptedToken),
                Expiration = token.ValidTo,
                RefreshToken = protectorRt.Protect(newRtoken.Value),
                Role = roles.FirstOrDefault(),
                Username = user.UserName,
                UserId = layerOneProtector.Protect(user.Id),
                ResponseInfo = CreateResponse("Auth Token Created", HttpStatusCode.OK)
            };

            return encAuthToken;

        }

        private static TokenResponseModel CreateErrorResponseToken(string errorMessage, HttpStatusCode statusCode)
        {
            var errorToken = new TokenResponseModel
            {
                Token = null,
                Username = null,
                Role = null,
                RefreshTokenExpiration = DateTime.Now,
                RefreshToken = null,
                Expiration = DateTime.Now,
                ResponseInfo = CreateResponse(errorMessage, statusCode)
            };

            return errorToken;
        }

        private static ResponseStatusInfoModel CreateResponse(string errorMessage, HttpStatusCode statusCode)
        {
            var responseStatusInfo = new ResponseStatusInfoModel
            {
                Message = errorMessage,
                StatusCode = statusCode
            };
            return responseStatusInfo;
        }


        private static TokenModel CreateRefreshToken(string clientId, string userId, int expireTime)
        {
            return new TokenModel()
            {
                ClientId = clientId,
                UserId = userId,
                Value = Guid.NewGuid().ToString("N"),
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = "",
                ExpiryTime = DateTime.UtcNow.AddMinutes(expireTime),
                EncryptionKeyRt = "",
                EncryptionKeyJwt = ""
            };
        }
    }
}
