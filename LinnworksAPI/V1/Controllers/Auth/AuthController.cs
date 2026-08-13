using System;
using System.Net;

namespace LinnworksAPI
{
    public class AuthController : BaseController, IAuthController
    {
        public AuthController(ApiContext apiContext) : base(apiContext)
        {
        }

        /// <summary>
        /// Generates a session and provides an Authorization Token and server in response.
        /// </summary>
        public BaseSession AuthorizeByApplication(AuthorizeByApplicationRequest request)
        {
            var response = GetResponse("Auth/AuthorizeByApplication", "request=" + WebUtility.UrlEncode(JsonFormatter.ConvertToJson(request)));
            return JsonFormatter.ConvertFromJson<BaseSession>(response);
        }

        /// <summary>
        /// Returns current application subscription profile information for a given application for a
        /// specific user. Use this after AuthorizeByApplication has returned a session - the session's
        /// Id is the userId to supply here. Returns null if there is no current subscription.
        /// </summary>
        /// <param name="applicationId">Your application Id</param>
        /// <param name="applicationSecret">Your application secret key</param>
        /// <param name="userId">User Id (Id field of the session)</param>
        public ApplicationProfileResponse GetApplicationProfileBySecretKey(Guid applicationId, Guid applicationSecret, Guid userId)
        {
            var response = GetResponse("Auth/GetApplicationProfileBySecretKey", "applicationId=" + applicationId + "&applicationSecret=" + applicationSecret + "&userId=" + userId);
            return JsonFormatter.ConvertFromJson<ApplicationProfileResponse>(response);
        }

        /// <summary>
        /// Not present in the current public API spec (references/api/v1/Auth.md only lists
        /// AuthorizeByApplication and GetApplicationProfileBySecretKey) - kept because it's a working
        /// endpoint in the existing SDK. Flag for confirmation with Linnworks before relying on it long-term.
        /// </summary>
        public DateTime GetServerUTCTime()
        {
            var response = GetResponse("Auth/GetServerUTCTime", "");
            return JsonFormatter.ConvertFromJson<DateTime>(response);
        }
    }
}
