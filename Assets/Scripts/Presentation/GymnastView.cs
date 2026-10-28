using GymnastGame.Simulation;
using UnityEngine;

/// <summary>
/// Draws the gymnast from GymnastController.CurrentPose. Presentation only: it never changes the motion.
/// The simulation is planar (XY), so the body is placed at z = 0, in line with the bar's centre.
/// Runs in Update so the pose is in place before CameraFollow reads it in LateUpdate.
/// </summary>
public class GymnastView : MonoBehaviour
{
    [SerializeField] private GymnastController controller;
    [SerializeField] private Transform body;

    private Vector3 standingScale;

    private void Awake()
    {
        standingScale = body.localScale;
    }

    private void Update()
    {
        GymnastPose pose = controller.CurrentPose;
        body.SetPositionAndRotation(
            new Vector3(pose.Position.x, pose.Position.y, 0f),
            Quaternion.Euler(0f, 0f, pose.BodyAngleDegrees));

        // The capsule has no knees yet, so a crouch shortens it.
        body.localScale = new Vector3(standingScale.x, standingScale.y * (1f - pose.Crouch), standingScale.z);
    }
}
