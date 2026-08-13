using System;
using System.IO;
using System.Net;
using System.Text;

namespace LinnworksAPI
{
    /// <summary>
    /// Raw HTTP transport shared by every controller. All calls go through
    /// GetResponse - controllers only know their endpoint path and form body.
    /// </summary>
    public class Factory
    {
        public static string GetResponse(string extension, string body, ApiContext context, string httpMethod, int? timeout)
        {
            string url = context.ApiServer + extension;

            bool isGet = httpMethod == "GET";

            if (!string.IsNullOrWhiteSpace(body) && isGet)
            {
                url += "?" + body;
            }

            var req = HttpWebRequest.Create(url);
            req.Method = httpMethod;

            if (!isGet)
            {
                req.ContentType = "application/x-www-form-urlencoded";
            }

            req.Headers.Add("RecursionCount", context.RecursionCount.ToString());

            if (timeout.HasValue)
                req.Timeout = timeout.Value;

            if (context.SessionId != Guid.Empty)
                req.Headers.Add(HttpRequestHeader.Authorization, context.SessionId.ToString());

            if (!string.IsNullOrWhiteSpace(body) && !isGet)
                req.ContentLength = Encoding.UTF8.GetBytes(body).Length;

            if (!string.IsNullOrWhiteSpace(body) && !isGet)
            {
                using (Stream post = req.GetRequestStream())
                using (StreamWriter writer = new StreamWriter(post))
                {
                    writer.Write(body);
                }
            }

            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)req.GetResponse();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
            {
                // HttpWebRequest.GetResponse() throws for any non-2xx status - it never
                // returns normally with an error response, so the actual Linnworks error
                // body has to be read from the exception's Response, not from a post-hoc
                // status code check on a successful GetResponse() (that check below is
                // unreachable for genuine errors; kept only for whatever edge case a 2xx
                // response with a non-success body might represent).
                string errorBody;
                using (var sr = new StreamReader(errorResponse.GetResponseStream()))
                {
                    errorBody = sr.ReadToEnd();
                }

                string message;
                try
                {
                    var error = JsonFormatter.ConvertFromJson<ApiError>(errorBody);
                    message = error?.Message;
                }
                catch
                {
                    message = null;
                }

                throw new Exception(
                    $"Linnworks API error {(int)errorResponse.StatusCode} calling {extension}: " +
                    (string.IsNullOrWhiteSpace(message) ? errorBody : message),
                    ex);
            }

            string responseBody;
            using (StreamReader sr = new StreamReader(response.GetResponseStream()))
            {
                responseBody = sr.ReadToEnd();
            }

            if ((int)response.StatusCode < 200 || (int)response.StatusCode > 299)
            {
                var error = JsonFormatter.ConvertFromJson<ApiError>(responseBody);
                throw new Exception(error.Message);
            }

            return responseBody;
        }

        public class ApiError
        {
            public string Message { get; set; }
        }
    }
}
