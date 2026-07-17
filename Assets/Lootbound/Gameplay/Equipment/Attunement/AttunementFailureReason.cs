namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Reasons why an attunement attempt cannot proceed.
    /// Distinct from attempt failure (e.g., chance-based failure in future slices).
    /// </summary>
    public enum AttunementFailureReason
    {
        /// <summary>
        /// No failure - attempt can proceed or succeeded.
        /// </summary>
        None = 0,

        /// <summary>
        /// The equipment provided is null or invalid.
        /// </summary>
        InvalidEquipment,

        /// <summary>
        /// No inventory reference was provided.
        /// </summary>
        MissingInventory,

        /// <summary>
        /// The attunement stone item definition is not configured.
        /// </summary>
        MissingStoneDefinition,

        /// <summary>
        /// Player has no attunement stones in inventory.
        /// </summary>
        NoAttunementStones,

        /// <summary>
        /// Player has some stones but not enough for the attempt cost.
        /// </summary>
        InsufficientAttunementStones,

        /// <summary>
        /// Equipment is already at maximum attunement level.
        /// </summary>
        AlreadyAtMaximum,

        /// <summary>
        /// The attunement cost configuration is invalid or missing.
        /// </summary>
        InvalidConfiguration,

        /// <summary>
        /// The inventory transaction failed (stones could not be removed).
        /// </summary>
        TransactionFailed,

        /// <summary>
        /// The target item is not valid equipment (e.g., stackable item, stone itself).
        /// </summary>
        NotEquipment,

        /// <summary>
        /// An attunement attempt is already in progress.
        /// </summary>
        AttemptInProgress
    }
}
