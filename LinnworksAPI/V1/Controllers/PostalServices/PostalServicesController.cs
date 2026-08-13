using System;
using System.Collections.Generic;
using System.Net;

namespace LinnworksAPI
{
    public class PostalServicesController : BaseController, IPostalServicesController
    {
        public PostalServicesController(ApiContext apiContext) : base(apiContext)
        {
        }

        /// <summary>
        /// Adds a new postal service to the database.
        /// </summary>
        /// <param name="postalServiceDetails">Information about the postal service</param>
        /// <returns>The data of the created service</returns>
        public PostalService CreatePostalService(PostalService_WithChannelAndShippingLinks postalServiceDetails)
        {
            var response = GetResponse("PostalServices/CreatePostalService", "PostalServiceDetails=" + WebUtility.UrlEncode(JsonFormatter.ConvertToJson(postalServiceDetails)));
            return JsonFormatter.ConvertFromJson<PostalService>(response);
        }

        /// <summary>
        /// Deletes an existing postal service from the database.
        /// </summary>
        /// <param name="idToDelete">Postal service ID to delete</param>
        public void DeletePostalService(Guid idToDelete)
        {
            GetResponse("PostalServices/DeletePostalService", "idToDelete=" + idToDelete);
        }

        /// <summary>
        /// Returns Channel Service Link information.
        /// </summary>
        /// <param name="postalServiceId">Postal service ID</param>
        /// <returns>The data used for showing associated Channel Services in Postal Services.</returns>
        public List<ChannelServiceLinks> GetChannelLinks(Guid postalServiceId)
        {
            var response = GetResponse("PostalServices/GetChannelLinks", "postalServiceId=" + postalServiceId);
            return JsonFormatter.ConvertFromJson<List<ChannelServiceLinks>>(response);
        }

        /// <summary>
        /// Gets a list of the user's postal services and information on channels and couriers linked
        /// to each service.
        /// </summary>
        public List<PostalService_WithChannelAndShippingLinks> GetPostalServices()
        {
            var response = GetResponse("PostalServices/GetPostalServices", "");
            return JsonFormatter.ConvertFromJson<List<PostalService_WithChannelAndShippingLinks>>(response);
        }

        /// <summary>
        /// Changes an existing postal service in the database.
        /// </summary>
        /// <param name="postalServiceDetails">Information about the postal service</param>
        public void UpdatePostalService(PostalService postalServiceDetails)
        {
            GetResponse("PostalServices/UpdatePostalService", "PostalServiceDetails=" + WebUtility.UrlEncode(JsonFormatter.ConvertToJson(postalServiceDetails)));
        }
    }
}
