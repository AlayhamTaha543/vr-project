/// <summary>
/// Interface for tracking fluid mass within a container.
/// Used for dynamic particle count adjustments and mass depletion.
/// </summary>
public interface IFluidMassSource
{
    /// <summary>
    /// Initial fluid mass at start (kg).
    /// </summary>
    float InitialMass { get; }

    /// <summary>
    /// Current fluid mass remaining (kg).
    /// </summary>
    float CurrentMass { get; }

    /// <summary>
    /// Ratio of current mass to initial mass (0.0 to 1.0).
    /// Used to scale active particle count.
    /// </summary>
    float MassRatio { get; }
}
