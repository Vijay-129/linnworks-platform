using System;

namespace LinnworksAPI
{
    /// <summary>
    /// Represents a Linnworks application subscription profile
    /// </summary>
    public class ApplicationProfileResponse : LinnObject
    {
        /// <summary>
        /// Plan Tag as defined in your Application Configuration
        /// </summary>
        public String PlanTag { get; set; }

        /// <summary>
        /// Plan Name as defined in your application Configuration
        /// </summary>
        public String PlanName { get; set; }

        /// <summary>
        /// Date when the profile was signed up for, or resubscribed
        /// </summary>
        public DateTime ActivationDate { get; set; }

        /// <summary>
        /// Last payment date
        /// </summary>
        public DateTime LastPaymentDate { get; set; }

        /// <summary>
        /// Next payment date
        /// </summary>
        public DateTime NextPaymentDate { get; set; }

        /// <summary>
        /// When the profile is due to expire
        /// </summary>
        public DateTime ProfileExpires { get; set; }

        /// <summary>
        /// Indicates whether the payment profile is active for the application. If false, the customer
        /// canceled the profile but it's still active due to the last payment made in the last month.
        /// </summary>
        public Boolean IsProfileActive { get; set; }
    }
}
