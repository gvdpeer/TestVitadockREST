using System;
using System.Collections.Generic;
using System.Web;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Formatting;

namespace TestVitaDockREST
{
    public static class Extensions
    {
        public static string Encode(this string input)
        {
            var reg = new Regex(@"%[a-f0-9]{2}");
            var encoded = HttpUtility.UrlEncode(input);
            encoded = reg.Replace(encoded, m => m.Value.ToUpperInvariant());
            return encoded;
        }

        public static string HMAC(this string data, string key)
        {
            byte[] d = Encoding.ASCII.GetBytes(string.Concat(data));
            byte[] k = Encoding.ASCII.GetBytes(string.Concat(key));
            return Convert.ToBase64String(new HMACSHA256(k).ComputeHash(d));
        }
    }

    class Program
    {
        private static string access = "93hGU4YTZC85dVt40l85GJhqhtuXX78l0AoeJhU9XT9CE12K8mkutvrwtqr8SJTY";
        private static string secret = "9wJcwTYCrf9RVfdJrPyB510CxtoA7f7EF42dB8qsyddJqleGFPFW5eYLUn350t1p";
        private static string home = "https://test-cloud.vitadock.com";

        static void Main(string[] args)
        {
            Console.WriteLine("login");
            var tokens = Login();
            Console.WriteLine();
            Console.WriteLine("request activity tracker count");
            var result = Data(tokens["oauth_token"], tokens["oauth_token_secret"]);
            Console.WriteLine(result);
            Console.ReadLine();
        }

        private static Dictionary<string,string> Login()
        {
            //init
            var pars = new SortedDictionary<string, string>();
            pars["oauth_consumer_key"] = access;
            pars["oauth_signature_method"] = "HMAC-SHA256";
            pars["oauth_timestamp"] = Epoch();
            pars["oauth_nonce"] = Guid.NewGuid().ToString();
            pars["oauth_version"] = "1.0";
            var url = $"{home}/auth/unauthorizedaccesses";

            //basestring
            var baseString = "";
            foreach (var kv in pars)
                baseString += $"&{kv.Key}={kv.Value}";
            baseString = baseString.Trim('&');
            baseString = $"POST&{url.Encode()}&{baseString.Encode()}";

            //signed authorization header
            var sign = baseString.HMAC($"{secret}&").Encode();
            var authorization = "";
            pars.Add("oauth_signature", sign);
            foreach (var kv in pars)
                authorization += $",{kv.Key}=\"{kv.Value}\"";
            authorization = authorization.Trim(',');
            authorization = $"OAuth {authorization.Trim(',')}";

            //webapi call
            var headers = new Dictionary<string, string>();
            headers["Authorization"] = authorization;
            var parameters = new Dictionary<string, object>();
            var result = Post(url, headers).Result;

            //pass token dictionary
            var dic = new Dictionary<string, string>();
            var parts = result.Split('&');
            foreach(var part in parts)
            {
                var pp = part.Split('=');
                dic[pp[0]] = pp[1];
                Console.WriteLine($"{pp[0]} = {pp[1]}");
            }
            
            return dic;
        }

        private static string Data(string token,string tokenSecret)
        {
            //init
            var pars = new SortedDictionary<string, string>();
            pars["oauth_consumer_key"] = access;
            pars["oauth_signature_method"] = "HMAC-SHA256";
            pars["oauth_timestamp"] = Epoch();
            pars["oauth_nonce"] = Guid.NewGuid().ToString();
            pars["oauth_version"] = "1.0";
            pars["oauth_token"] = token;
            pars["date_since"] = "0";
            var url = $"{home}/data/tracker/activity/count";

            //basestring
            var baseString = "";
            foreach (var kv in pars)
                baseString += $"&{kv.Key}={kv.Value}";
            baseString = baseString.Trim('&');
            baseString = $"GET&{url.Encode()}&{baseString.Encode()}";

            //signed authorization header
            var sign = baseString.HMAC($"{secret}&{tokenSecret}").Encode();
            var authorization = "";
            pars.Remove("date_since");
            pars.Add("oauth_signature",sign);
            foreach (var kv in pars)
                authorization += $",{kv.Key}=\"{kv.Value}\"";
            authorization = authorization.Trim(',');
            authorization = $"OAuth {authorization}";

            //webapi call
            var headers = new Dictionary<string, string>();
            headers["Authorization"] = authorization;
            var parameters = new Dictionary<string, object>();
            var result = Get(url, "?date_since=0", headers).Result;
            return result;
        }

        public static async Task<string> Post(string url, Dictionary<string, string> headers)
        {
            if (headers == null)
                headers = new Dictionary<string, string>();
            using (var client = new HttpClient())
            {
                //prepare
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                //headers
                AddHeaders(client, headers);

                try
                {
                    HttpResponseMessage response = await client.PostAsync(url, "", new MediaTypeFormatterCollection().JsonFormatter);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch (Exception x)
                {

                }
                return "";

            }
        }

        public static async Task<string> Get(string url, string call, Dictionary<string, string> headers)
        {
            using (var client = new HttpClient())
            {
                //prepare
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                //headers
                AddHeaders(client, headers);

                //call
                HttpResponseMessage response = await client.GetAsync(call);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();
                else
                {
                    var error = response.Content.ReadAsStringAsync();
                    return response.ToString();
                }

                //result
                return default(string);
            }
        }

        private static void AddHeaders(HttpClient client, Dictionary<string, string> headers)
        {
            foreach (var header in headers)
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        private static string Epoch()
        {
            return Math.Round(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds).ToString();
        }
    }
}
