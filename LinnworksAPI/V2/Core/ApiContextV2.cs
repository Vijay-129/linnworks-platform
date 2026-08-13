using System;

namespace LinnworksAPI.V2
{
    // LocalityType (EU/US/AS) is defined once in v1 (LinnworksAPI.LocalityType, under
    // Auth/Models) and reused here rather than redefined - it's the same concept
    // Linnworks' BaseSession.Locality already exposes.
    using LocalityType = LinnworksAPI.LocalityType;

    /// <summary>
    /// v2 is a distinct REST surface from v1 (real HTTP verbs, JSON bodies, path
    /// parameters) - kept in its own namespace so its types never collide with v1's
    /// flat LinnworksAPI namespace (e.g. both define FulfillmentStatus differently).
    /// </summary>
    public class ApiContextV2
    {
        public Guid SessionId { get; private set; }
        public string ApiServer { get; private set; }

        public ApiContextV2(Guid sessionId, LocalityType locality)
        {
            if (sessionId == Guid.Empty)
                throw new ArgumentNullException(nameof(sessionId), "SessionId is missing");

            SessionId = sessionId;
            ApiServer = ServerForLocality(locality);
        }

        // Servers list from PublicApiSpecs/2.0 - one per locality, same localities as
        // BaseSession.Locality (LocalityType) in v1.
        private static string ServerForLocality(LocalityType locality)
        {
            switch (locality)
            {
                case LocalityType.EU: return "https://eu-api.linnworks.net/v2/";
                case LocalityType.US: return "https://us-api.linnworks.net/v2/";
                case LocalityType.AS: return "https://as-api.linnworks.net/v2/";
                default: throw new ArgumentOutOfRangeException(nameof(locality));
            }
        }
    }
}
