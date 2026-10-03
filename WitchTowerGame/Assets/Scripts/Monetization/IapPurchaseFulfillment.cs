using System;
using WitchTower.Data;
using WitchTower.Save;

namespace WitchTower.Monetization
{
    public static class IapPurchaseFulfillment
    {
        // Persist a staged balance and transaction ID together before making the
        // currency spendable or acknowledging delivery to the store.
        public static bool TryFulfill(
            PlayerProfile profile,
            int currentFloor,
            string productId,
            string transactionId,
            Func<PlayerSaveData, bool> persist,
            Action confirm,
            out bool newlyGranted,
            out string error)
        {
            newlyGranted = false;
            error = string.Empty;
            if (profile == null || persist == null || confirm == null ||
                string.IsNullOrWhiteSpace(transactionId) ||
                !IapProductCatalog.TryGet(productId, out IapProductDefinition definition))
            {
                error = "Purchase data or persistence service is unavailable.";
                return false;
            }

            try
            {
                bool alreadyProcessed = profile.HasProcessedIapTransaction(transactionId);
                PlayerSaveData staged = profile.ToSaveData(currentFloor);
                staged.HasRemovedAds = true;
                if (!alreadyProcessed)
                {
                    staged.PaidGachaStones = checked(staged.PaidGachaStones + definition.PaidStoneAmount);
                    staged.ProcessedIapTransactionIds.Add(transactionId);
                }

                // Save even on redelivery: never treat an in-memory ID alone as
                // evidence that the current balance has been durably written.
                if (!persist(staged))
                {
                    error = "Purchase could not be saved. Delivery remains pending.";
                    return false;
                }

                if (!alreadyProcessed)
                {
                    profile.TryGrantPaidStonePurchase(productId, definition.PaidStoneAmount, transactionId);
                    newlyGranted = true;
                }

                AdRemovalEntitlementService.TryGrantVerifiedPurchase(profile, productId);
                confirm();
                return true;
            }
            catch (Exception exception)
            {
                // A failed confirmation can be retried using the persisted ID
                // without granting the currency a second time.
                error = exception.Message;
                return false;
            }
        }
    }
}
