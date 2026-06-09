using SyncBook.Server.Models;

namespace SyncBook.Server.Services;

public static class SubscriptionHelper
{
    public static bool IsSubscriptionActive(Business? business)
    {
        if (business is null)
        {
            return true;
        }

        if (string.IsNullOrEmpty(business.SubscriptionStatus))
        {
            return true;
        }

        return business.SubscriptionStatus == "active";
    }
}
