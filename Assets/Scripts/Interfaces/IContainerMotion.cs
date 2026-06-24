/// <summary>
/// Interface for container motion detection.
/// Used to trigger motion-based events like automatic paint dropping.
/// </summary>
public interface IContainerMotion
{
    /// <summary>
    /// Current speed magnitude of the container (m/s).
    /// </summary>
    float CurrentSpeed { get; }

    /// <summary>
    /// Current angular speed of the container (rad/s).
    /// For swinging buckets, this represents how fast the rope angle changes.
    /// </summary>
    float CurrentAngularSpeed { get; }

    /// <summary>
    /// True if the container is currently moving above a minimal threshold.
    /// </summary>
    bool IsMoving { get; }

    /// <summary>
    /// Checks if current speed exceeds a given threshold.
    /// </summary>
    bool IsSpeedAboveThreshold(float threshold);
}
