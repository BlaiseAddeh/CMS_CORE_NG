using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelService;
using Serilog;
using DataService;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace FiltersService
{
    public class AdminAuthenticationHandler : AuthenticationHandler<AdminAuthenticationOptions>
    {

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IServiceProvider _provider;
        private readonly IdentityDefaultOptions _identityDefaultOptions;

        private readonly DataProtectionKeys _dataProtectionKeys;
        private readonly AppSettings _appSettings;


        private const string AccessToken = "access_token";

        private const string User_Id = "user_id";
        private const string Username = "username";
        private string[] UserRoles = new[] { "Administrator" };



        public AdminAuthenticationHandler(
            IOptionsMonitor<AdminAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            UserManager<ApplicationUser> userManager,
            IOptions<AppSettings> appSettings,
            IOptions<DataProtectionKeys> dataProtectionKeys,
            IServiceProvider provider,
            IOptions<IdentityDefaultOptions> identityDefaultOptions
            ) : base(options, logger, encoder, clock)
        {
            _userManager = userManager;
            _appSettings = appSettings.Value;
            _identityDefaultOptions = identityDefaultOptions.Value;
            _provider = provider;
            _dataProtectionKeys = dataProtectionKeys.Value;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {

            /*Logique validating the request*/

            /*1- Vérifier si la requete contient "access_token" ou "user_id" dans ses cookies*/
            if (!Request.Cookies.ContainsKey(AccessToken) || !Request.Cookies.ContainsKey(User_Id))
            {
                Log.Error("No Access Token or User Id found");
                return await Task.FromResult(AuthenticateResult.NoResult());
            }



            /*2- Verifier si le token de la requete se trouve bien dans l'entête d'authentification */
            if (!AuthenticationHeaderValue.TryParse($"{"Bearer " + Request.Cookies[AccessToken]}",
                out AuthenticationHeaderValue headerValue))
            {
                Log.Error("Could no Parse Token from Authentication Header");
                return await Task.FromResult(AuthenticateResult.NoResult());
            }


            /*3- Verifier si le userID de la requete se trouve bien dans l'entête authentification */
            if (!AuthenticationHeaderValue.TryParse($"{"Bearer " + Request.Cookies[User_Id]}",
               out AuthenticationHeaderValue headerValueUid))
            {
                Log.Error("Could no Parse User Id from Authentication Header");
                return await Task.FromResult(AuthenticateResult.NoResult());
            }

            /*4- Decripter le cookie */

            try
            {
                /* Logique de décryptage du cookie */
                var key = Encoding.ASCII.GetBytes(_appSettings.Secret); // Step1:  get Secret key in appsettings.json
                var handler = new JwtSecurityTokenHandler(); // Step2:  creation d'une instance de JwtSecurityTokenHandler
                TokenValidationParameters validationParameters = // Step3:  Retrouver les paramètres utiles à decrypter le token en créant une instance des paramètres de validation
                    new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidIssuer = _appSettings.Site,
                        ValidAudience = _appSettings.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };

                //Step4: using Microsoft.Extensions.DependencyInjection;

                // - Get the Data protection service instance
                var protectorProvider = _provider.GetService<IDataProtectionProvider>(); // Création d'une instance de DataProtection
                var protector = protectorProvider.CreateProtector(_dataProtectionKeys.ApplicationUserKey); //Capter depuis appsettins.json
                var decryptedUid = protector.Unprotect(headerValueUid.Parameter); // Decryptage de la vleur recu depuis l'Url
                var decryptedToken = protector.Unprotect(headerValue.Parameter); // Decryptage du token contenu dans le headerValue

                // Validation du Token decrypté

                //Step5:  Verifier la valeur que nous avons dans la base de données
                TokenModel tokenModel = new TokenModel();

                using (var scope = _provider.CreateScope())
                {
                    var dbContextService = scope.ServiceProvider.GetService<ApplicationDbContext>();
                    var userToken = dbContextService.Tokens.Include(x => x.User)
                        .FirstOrDefault(ut => ut.UserId == decryptedUid &&
                        ut.User.UserName == Request.Cookies[Username]
                        && ut.User.Id == decryptedUid
                        && ut.User.UserRole == "Administrator");

                    tokenModel = userToken;
                }

                // Step6: Verifier si le tokenmodel est null
                if (tokenModel == null)
                {
                    return await Task.FromResult(AuthenticateResult.Fail("You are not authorized to view this page"));
                }

                /*Step7: Apply second Layer of decryption using the key store in the token model*/
                /*Create protector instance for layer two using token model key */
                /*IMPORTANT : if no key exists or key is invalid, exception will be thrown */

                IDataProtector layerTwoProtector = protectorProvider.CreateProtector(tokenModel?.EncryptionKeyJwt);
                string decryptedTokenLayerTwo = layerTwoProtector.Unprotect(decryptedToken);


                /*Step8: Validate the token we received -using validation parameters set in setp 3*/
                /*IMPORTANT - if the validation fals - the method ValidateToken will thrown exception*/
                var validateToken = handler.ValidateToken(decryptedTokenLayerTwo,
                                            validationParameters, out var securityToken);


                //Step9:  check the signature of the token
                if (!(securityToken is JwtSecurityToken jwtSecurityToken) ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256,
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    return await Task.FromResult(AuthenticateResult.Fail("Your are not authorized"));
                }

                //Step10: Extrating username from Token
                var username = validateToken.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

                //Step11: Verifier si le username est celui contenu dans les cookies
                if (Request.Cookies[Username] != username)
                {
                    return await Task.FromResult(AuthenticateResult.Fail("Your are not authorized to view this page"));
                }

                // Step12: Get the user by their email
                var user = await _userManager.FindByNameAsync(username);

                // Step13: If user does not exist return authentication failed result

                if (user == null)
                {
                    return await Task.FromResult(AuthenticateResult.Fail("Your are not authorized to view this page"));
                }

                //Step14: We need to check if the user belongs to the group of user-roles
                if (!UserRoles.Contains(user.UserRole))
                {
                    return await Task.FromResult(AuthenticateResult.Fail("Your are not authorized to view this page"));
                }

                //Step15: Create an authentication ticket as tokken is valid
                var identity = new ClaimsIdentity(validateToken.Claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return await Task.FromResult(AuthenticateResult.Success(ticket));


            }
            catch (Exception ex)
            {
                /* Message d'erreur si on arrive pas à décrypter le cookie */
                Log.Error($"An error occured: Message::: {ex.Message} StackTrace::: {ex.StackTrace} Source::: {ex.Source}");
                return await Task.FromResult(AuthenticateResult.Fail("You are not authorized!"));
            }

        }


        /* Cette méthode pour gérer la procédure en cas d'echec de la tentative de connection */
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            /*Logique en cas d'echec de la tentative de connection*/

            //Delete any invalid cookie
            Response.Cookies.Delete("access_token");
            Response.Cookies.Delete("user_id");
            Response.Headers["WWW-Authenticate"] = $"Not Authorized";
            // AccessDeniedPath est definit dans le fichier appsettings.json
            // ==> le chemin /Account/AccessDenied   doit être créé
            Response.Redirect(_identityDefaultOptions.AccessDeniedPath);

            return Task.CompletedTask;
        }

    }
}
