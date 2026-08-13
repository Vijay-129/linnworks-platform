using System;

namespace LinnworksAPI
{
    public interface IAuthController
    {
        BaseSession AuthorizeByApplication(AuthorizeByApplicationRequest request);
        ApplicationProfileResponse GetApplicationProfileBySecretKey(Guid applicationId, Guid applicationSecret, Guid userId);
        DateTime GetServerUTCTime();
    }
}
