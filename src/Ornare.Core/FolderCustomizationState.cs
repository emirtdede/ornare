namespace Ornare.Core;

public enum FolderCustomizationState
{
    Healthy = 0,
    MissingOwnedIcon,
    ConfigMismatch,
    ExternalIconConflict,
    TrackingDetached,
    MissingTarget,
    AccessUnavailable,
    NeedsReconciliation,
    UnsupportedLocation
}
