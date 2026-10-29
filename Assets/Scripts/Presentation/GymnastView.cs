using GymnastGame.Simulation;
using UnityEngine;

/// <summary>
/// Draws the articulated figure from GymnastController.CurrentPose. Presentation only: it never changes the motion.
/// This object sits at the centre of mass and turns with the body; Hips is moved so the figure's own
/// centre of mass lands here. Limb bones point down their local -Y and bend about local Z, so a joint
/// angle is simply that bone's local Z rotation. The twist turns the whole body about its long axis
/// (local Y). Bringing the hands onto the chest turns each bent forearm about its upper arm (an inward
/// rotation of the shoulder). Bone names follow Unity's Humanoid names.
/// Runs in Update so the pose is in place before CameraFollow reads it in LateUpdate.
/// </summary>
public class GymnastView : MonoBehaviour
{
    [SerializeField] private GymnastController controller;
    [SerializeField] private Transform hips;
    [SerializeField] private Transform leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm;
    [SerializeField] private Transform leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg;

    private void Update()
    {
        if (!controller.IsReady) return;    // the controller has already reported what is missing

        GymnastPose pose = controller.CurrentPose;
        BodyPose joints = pose.Joints;

        // The simulation is planar (XY), so the body stays at z = 0, in line with the bar's centre.
        // Salto about the bar axis (Z), then twist about the body's own long axis (local Y).
        transform.SetPositionAndRotation(
            new Vector3(pose.CentreOfMass.x, pose.CentreOfMass.y, 0f),
            Quaternion.Euler(0f, 0f, pose.BodyAngleDegrees) * Quaternion.Euler(0f, pose.TwistDegrees, 0f));

        Vector2 centreOfMass = controller.Body.Evaluate(joints).CentreOfMass;   // hip → CoM, body frame
        hips.localPosition = new Vector3(-centreOfMass.x, -centreOfMass.y, 0f);

        SetBend(leftUpperArm, joints.Shoulder);
        SetBend(rightUpperArm, joints.Shoulder);
        float inward = joints.HandsIn * MaxInwardRotation();
        leftLowerArm.localRotation = Quaternion.Euler(0f, inward, 0f) * Quaternion.Euler(0f, 0f, joints.Elbow);
        rightLowerArm.localRotation = Quaternion.Euler(0f, -inward, 0f) * Quaternion.Euler(0f, 0f, joints.Elbow);
        SetBend(leftUpperLeg, joints.Hip);
        SetBend(rightUpperLeg, joints.Hip);
        SetBend(leftLowerLeg, -joints.Knee);    // knees bend backwards
        SetBend(rightLowerLeg, -joints.Knee);
    }

    private static void SetBend(Transform bone, float degrees) => bone.localRotation = Quaternion.Euler(0f, 0f, degrees);

    // Inward turn (degrees) that brings the hands from shoulder width to the midline.
    private float MaxInwardRotation()
    {
        var body = controller.Body;
        float reach = body.ForearmLength + body.HandLength * 0.5f;
        return Mathf.Asin(Mathf.Clamp01(body.ShoulderHalfWidth / reach)) * Mathf.Rad2Deg;
    }
}
