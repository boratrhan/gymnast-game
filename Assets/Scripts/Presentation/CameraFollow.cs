using UnityEngine;

/// <summary>
/// Keeps the camera at a fixed offset from a point between the bar and the gymnast,
/// and moves it there smoothly. Runs in LateUpdate, after the gymnast has moved this frame.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;          // the gymnast
    [SerializeField] private Transform anchor;          // the bar; the view stays partly centred on it
    [SerializeField, Range(0f, 1f)] private float targetWeight = 0.6f;  // 0 = look at the bar, 1 = at the gymnast
    [SerializeField] private Vector3 offset = new Vector3(-3.8f, 0.4f, -8.2f);
    [SerializeField] private float smoothTime = 0.25f;  // seconds to roughly catch up
    [SerializeField] private float minFocusY = 1.2f;    // do not follow below this height (landing)

    private Vector3 focus;
    private Vector3 focusVelocity;

    private void Start()
    {
        focus = DesiredFocus();
        ApplyFocus();
    }

    private void LateUpdate()
    {
        focus = Vector3.SmoothDamp(focus, DesiredFocus(), ref focusVelocity, smoothTime);
        ApplyFocus();
    }

    private Vector3 DesiredFocus()
    {
        Vector3 desired = Vector3.Lerp(anchor.position, target.position, targetWeight);
        desired.y = Mathf.Max(desired.y, minFocusY);
        return desired;
    }

    private void ApplyFocus()
    {
        transform.position = focus + offset;
        transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
    }
}
