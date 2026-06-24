using UnityEngine;

/// <summary>
/// Interface for any fluid container (bucket, bowl, cup, etc.).
/// Provides abstracted access to container geometry and motion.
/// </summary>
public interface IFluidContainer
{
    /// <summary>
    /// World position of the container's bottom-center point.
    /// For a frustum bucket, this is the base center.
    /// </summary>
    Vector3 GetContainerCenter();

    /// <summary>
    /// Container's local up axis in world space.
    /// Points from bottom to top of the container.
    /// </summary>
    Vector3 GetContainerUp();

    /// <summary>
    /// Current velocity of the container in world space.
    /// </summary>
    Vector3 GetContainerVelocity();

    /// <summary>
    /// Current acceleration of the container in world space.
    /// Useful for applying inertial forces to particles.
    /// </summary>
    Vector3 GetContainerAcceleration();

    /// <summary>
    /// Radius at the top opening of the container.
    /// </summary>
    float GetTopRadius();

    /// <summary>
    /// Radius at the bottom of the container.
    /// </summary>
    float GetBottomRadius();

    /// <summary>
    /// Height of the container from bottom to top.
    /// </summary>
    float GetHeight();

    /// <summary>
    /// Checks if a world position is inside the container volume.
    /// </summary>
    bool IsInsideContainer(Vector3 worldPosition);
}
