using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Serilog;

namespace CookieService
{
    public class CookieSvc : ICookieSvc
    {
        private readonly CookieOptions _cookieOptions;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CookieSvc(CookieOptions cookieOptions, IHttpContextAccessor httpContextAccessor)
        {
            _cookieOptions = cookieOptions;
            _httpContextAccessor = httpContextAccessor;
        }

        public string Get(string key) // Get the cookie by the name
        {
            return _httpContextAccessor.HttpContext.Request.Cookies[key];
        }

        public void SetCookie(string key, string value, int? expireTime, bool isSecure, bool isHttpOnly)
        {
            _cookieOptions.Expires = expireTime.HasValue ? DateTime.Now.AddMinutes(expireTime.Value) :
                                     DateTime.Now.AddMilliseconds(10);
            _cookieOptions.Secure = isSecure;
            _cookieOptions.HttpOnly = isHttpOnly;
            _httpContextAccessor.HttpContext.Response.Cookies.Append(key, value, _cookieOptions);
        }

        public void SetCookie(string key, string value, int? expireTime)
        {

            if (expireTime.HasValue)
            {
                _cookieOptions.Secure = true;
                _cookieOptions.Expires = DateTime.Now.AddMinutes(expireTime.Value);
            }
            else
            {
                _cookieOptions.Secure = true;
                _cookieOptions.Expires = DateTime.Now.AddMilliseconds(10);
            }

            _cookieOptions.Secure = true;
            _cookieOptions.SameSite = SameSiteMode.Strict;
            _httpContextAccessor.HttpContext.Response.Cookies.Append(key, value, _cookieOptions);

        }

        public void DeleteCookie(string key)
        {
            _httpContextAccessor.HttpContext.Response.Cookies.Delete(key);
        }

        public void DeleteAllCookies(IEnumerable<string> cookiesToDelete) // On a la liste des noms de cookies à supprimer
        {
            foreach (var key in cookiesToDelete)
            {
                _httpContextAccessor.HttpContext.Response.Cookies.Delete(key);
            }
        }


        // Cette methode est utilisée à des fin de demonstration.
        // Vous pouvez utiliser les sites: whatismyipaddress.com ou ipinfo.io pour la détection des ip
        // Mais ces sites sont payant.

        public string GetUserIP()
        {
            string userIp = "unknow";

            try
            {
                //_httpContextAccessor permet d'obtenir des informations à partir de l'url
                userIp = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();

            }
            catch (Exception ex)
            {
                Log.Error($"An error occured {ex.Message} , {ex.StackTrace} , {ex.InnerException}, {ex.Source}");
            }
            return userIp;
        }

        public string GetUserCountry()
        {
            try
            {
                string userIp = GetUserIP();
                // On va faire appel à l'api du site payant ipinfo.io car ce site donne la localisation à partir de l'ip
                string info = new WebClient().DownloadString("https://ipinfo.io/" + userIp);
                var ipInfo = JsonConvert.DeserializeObject<IpInfo>(info);
                RegionInfo regionInfo = new RegionInfo(ipInfo.Country);
                ipInfo.Country = regionInfo.EnglishName;

                if (!string.IsNullOrEmpty(userIp))
                {
                    return ipInfo.Country;
                }

            }
            catch (Exception ex)
            {
                Log.Error($"An error occured {ex.Message} , {ex.StackTrace} , {ex.InnerException}, {ex.Source}");
            }

            return "unknown";
        }

        public string GetUserOS()
        {
            string userOs = "unknown";

            try
            {
                userOs = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"].ToString();
            }
            catch (Exception ex)
            {
                Log.Error($"An error occured {ex.Message} , {ex.StackTrace} , {ex.InnerException}, {ex.Source}");
            }

            return userOs;
        }
    }
}
