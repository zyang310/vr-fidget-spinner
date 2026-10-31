using System.Collections.Generic;
using UnityEngine;

// Lives on the Spinner parent (the object with the Rigidbody).
// Tracked hand colliders are teleported each frame, so physics alone can't tell
// how fast your finger is moving. Instead, this script watches any hand collider
// inside the spinner's disc, measures how fast it sweeps around the hub, and
// drags the spinner up to that speed (or slows it if your finger is slower).
[RequireComponent(typeof(Rigidbody))]
public class FidgetSpinner : MonoBehaviour
{
    [Header("Spin")]
    [Tooltip("Unity caps angular velocity at 7 rad/s by default, which is far too slow for a spinner.")]
    public float maxSpinRadPerSec = 150f;
    [Tooltip("Lower = coasts longer.")]
    public float angularDamping = 0.08f;

    [Header("Finger flicks")]
    [Tooltip("Spinner radius where fingers count (m). ~0.105 for the generated spinner.")]
    public float outerRadius = 0.11f;
    [Tooltip("Fingers closer than this to the center (on the hub) are ignored.")]
    public float hubRadius = 0.025f;
    [Tooltip("How far above/below the disc a finger still counts (m).")]
    public float halfThickness = 0.025f;
    [Tooltip("Multiplier on finger speed. Real spinners spin faster than the flick that started them.")]
    public float flickBoost = 1.8f;
    [Range(0f, 1f), Tooltip("How strongly a fast finger grabs the spinner each physics step.")]
    public float grip = 0.6f;
    [Range(0f, 1f), Tooltip("How strongly a slower/still finger brakes it.")]
    public float brake = 0.08f;

    [Header("Optional whirr sound (looping AudioSource on this object)")]
    public AudioSource whirr;
    public float speedForFullPitch = 60f;

    Rigidbody rb;
    readonly Dictionary<Collider, (Vector3 pos, float time)> lastSeen = new();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = maxSpinRadPerSec;
        rb.angularDamping = angularDamping;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezePosition
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;

        // Trigger zone around the whole spinner so we can watch fingers approach.
        var zone = gameObject.AddComponent<SphereCollider>();
        zone.isTrigger = true;
        zone.radius = outerRadius + 0.02f;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.attachedRigidbody == rb) return; // our own parts

        Vector3 p = other.bounds.center;
        float now = Time.fixedTime;
        bool hadPrev = lastSeen.TryGetValue(other, out var prev);
        lastSeen[other] = (p, now);
        float dt = now - prev.time;
        if (!hadPrev || dt <= 0f || dt > 3f * Time.fixedDeltaTime) return;

        Vector3 axis = transform.up;
        Vector3 r = p - transform.position;
        float h = Vector3.Dot(r, axis);
        Vector3 radial = r - axis * h;
        float dist = radial.magnitude;
        if (Mathf.Abs(h) > halfThickness || dist < hubRadius || dist > outerRadius) return;

        Vector3 v = (p - prev.pos) / dt;
        // Angular speed the finger is sweeping around the spin axis.
        float fingerW = Vector3.Dot(Vector3.Cross(radial, v), axis) / (dist * dist);
        float w = Vector3.Dot(rb.angularVelocity, axis);

        bool pushingFaster = Mathf.Abs(fingerW) > Mathf.Abs(w) || Mathf.Sign(fingerW) != Mathf.Sign(w);
        if (pushingFaster && Mathf.Abs(fingerW) > 1f)
            w = Mathf.Lerp(w, fingerW * flickBoost, grip);
        else
            w = Mathf.Lerp(w, fingerW, brake); // finger resting in the blades slows it down

        rb.angularVelocity = axis * Mathf.Clamp(w, -maxSpinRadPerSec, maxSpinRadPerSec);
    }

    void OnTriggerExit(Collider other) => lastSeen.Remove(other);

    void Update()
    {
        if (whirr == null) return;
        float speed = Mathf.Abs(rb.angularVelocity.y);
        float t = Mathf.Clamp01(speed / speedForFullPitch);
        whirr.volume = t;
        whirr.pitch = Mathf.Lerp(0.5f, 2f, t);
        if (speed > 0.5f && !whirr.isPlaying) whirr.Play();
        else if (speed <= 0.5f && whirr.isPlaying) whirr.Stop();
    }
}
