using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace LinnworksAPI.V2
{
    /// <summary>
    /// v2 transport: real HTTP verbs, JSON request/response bodies, query strings -
    /// unlike v1's Factory, which always form-encodes a single named parameter
    /// regardless of verb. Query string values are provided pre-formatted by the
    /// caller (Query dictionary), since not every field maps to ToString() cleanly
    /// (arrays, dates).
    /// </summary>
    public static class RestClient
    {
        public static string Send(ApiContextV2 context, string httpMethod, string path, Dictionary<string, string> query = null, object body = null)
        {
            string url = context.ApiServer.TrimEnd('/') + "/" + path.TrimStart('/');

            if (query != null && query.Count > 0)
            {
                var pairs = query
                    .Where(kv => kv.Value != null)
                    .Select(kv => WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value));
                var qs = string.Join("&", pairs);
                if (!string.IsNullOrEmpty(qs))
                    url += "?" + qs;
            }

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = httpMethod;
            req.Headers.Add(HttpRequestHeader.Authorization, context.SessionId.ToString());

            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body);
                req.ContentType = "application/json";
                var bytes = Encoding.UTF8.GetBytes(json);
                req.ContentLength = bytes.Length;
                using (var stream = req.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)req.GetResponse();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
            {
                using (var reader = new StreamReader(errorResponse.GetResponseStream()))
                {
                    throw new Exception($"v2 API error {(int)errorResponse.StatusCode}: {reader.ReadToEnd()}", ex);
                }
            }

            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        public static T Send<T>(ApiContextV2 context, string httpMethod, string path, Dictionary<string, string> query = null, object body = null)
        {
            var json = Send(context, httpMethod, path, query, body);
            if (string.IsNullOrWhiteSpace(json))
                return default(T);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
