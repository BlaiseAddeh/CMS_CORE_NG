using System;
using System.Collections.Generic;

namespace CookieService
{
    public interface ICookieSvc
    {
        string Get(string key);
        void SetCookie(string key, string value, int? expireTime, bool isSecure, bool isHttpOnly);
        void SetCookie(string key, string value, int? expireTime);
        public void DeleteCookie(string key);
        void DeleteAllCookies(IEnumerable<string> cookiesToDelete);
        string GetUserIP();
        string GetUserCountry();
        string GetUserOS();
    }
}
