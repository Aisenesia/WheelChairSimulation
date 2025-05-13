using System;
using UnityEngine;
using System.IO.Ports;
using System.Collections;

public class WheelChairController : MonoBehaviour
{
    private readonly SerialPort _serialPortNo = new("COM10", 9600);
    public Transform leftWheel;
    public Transform rightWheel;
    public float wheelRadius = 0.3f; // in meters
    public float ticksPerRevolution = 30f;
    public float timeSlice = 0.1f; // 100 ms

    private int _prevLeftTicks = 0;
    private int _prevRightTicks = 0;
    private Rigidbody _rb;

    private float _lastUpdateTime = 0f;
    private int _deltaLeft = 0;
    private int _deltaRight = 0;

    private Coroutine _movementCoroutine;

    void Start()
    {
        Console.WriteLine("Starting WheelChairController");
        _rb = GetComponent<Rigidbody>();
        _serialPortNo.Open();
        _serialPortNo.ReadTimeout = 50;
       
        Console.WriteLine("Serial Port Opened: " + _rb);
    }

    private void Update()
    {
        if (!_serialPortNo.IsOpen)
        {
            // Gather ticks from keyboard input if USB is not connected
            if (Input.GetKey(KeyCode.U))
            {
                _deltaLeft = 1;
            }
            if (Input.GetKey(KeyCode.J))
            {
                _deltaLeft = -1;
            }
            if (Input.GetKey(KeyCode.I))
            {
                _deltaRight = 1;
            }
            if (Input.GetKey(KeyCode.K))
            {
                _deltaRight = -1;
            }

            if (Time.time - _lastUpdateTime >= timeSlice)
            {
                var leftDistance = (2 * Mathf.PI * wheelRadius) * (_deltaLeft / ticksPerRevolution);
                var rightDistance = (2 * Mathf.PI * wheelRadius) * (_deltaRight / ticksPerRevolution);

                if (_movementCoroutine != null)
                {
                    StopCoroutine(_movementCoroutine);
                }

                _movementCoroutine = StartCoroutine(SmoothMovement(leftDistance, rightDistance));

                // Reset deltas and update time
                _deltaLeft = 0;
                _deltaRight = 0;
                _lastUpdateTime = Time.time;
            }

            return;
        }

        try
        {
            var data = _serialPortNo.ReadLine().Trim();
            Debug.Log("Raw Data: " + data);

            // Example: "Left : -1 , Right: -1"
            var parts = data.Split(',');

            var leftTicks = 0;
            var rightTicks = 0;

            foreach (var part in parts)
            {
                if (part.Contains("Left"))
                    leftTicks = int.Parse(part.Split(':')[1].Trim());
                else if (part.Contains("Right"))
                    rightTicks = int.Parse(part.Split(':')[1].Trim());
            }

            _deltaLeft += leftTicks - _prevLeftTicks;
            _deltaRight += rightTicks - _prevRightTicks;

            _prevLeftTicks = leftTicks;
            _prevRightTicks = rightTicks;

            if (Time.time - _lastUpdateTime >= timeSlice)
            {
                var leftDistance = (2 * Mathf.PI * wheelRadius) * (_deltaLeft / ticksPerRevolution);
                var rightDistance = (2 * Mathf.PI * wheelRadius) * (_deltaRight / ticksPerRevolution);

                if (_movementCoroutine != null)
                {
                    StopCoroutine(_movementCoroutine);
                }

                _movementCoroutine = StartCoroutine(SmoothMovement(leftDistance, rightDistance));

                // Reset deltas and update time
                _deltaLeft = 0;
                _deltaRight = 0;
                _lastUpdateTime = Time.time;
            }
        }
        catch (TimeoutException)
        {
            // Ignore timeout errors
        }
        catch (Exception e)
        {
            Debug.LogWarning("Data parse error: " + e.Message);
        }
    }

    private IEnumerator SmoothMovement(float leftDistance, float rightDistance)
    {
        float duration = 0.2f; // Duration to spread the movement over
        float elapsedTime = 0f;

        Vector3 initialPosition = _rb.position;
        Quaternion initialRotation = transform.rotation;

        Vector3 targetPosition = initialPosition;
        Quaternion targetRotation = initialRotation;

        if (Mathf.Abs(_deltaLeft) > 0 && Mathf.Abs(_deltaRight) > 0)
        {
            // Both wheels are moving
            var averageDistance = (leftDistance + rightDistance) / 2f;
            targetPosition += transform.forward * averageDistance;

            var rotationDiff = (rightDistance - leftDistance) / (2f * wheelRadius); // radians
            targetRotation *= Quaternion.Euler(0, rotationDiff * Mathf.Rad2Deg, 0);
        }
        else if (Mathf.Abs(_deltaLeft) > 0)
        {
            // Only left wheel is moving
            var rotationDiff = -leftDistance / wheelRadius; // Turn right
            targetRotation *= Quaternion.Euler(0, rotationDiff * Mathf.Rad2Deg, 0);

            // Move slightly forward while turning
            targetPosition += transform.forward * (Mathf.Abs(leftDistance) / 2f);
        }
        else if (Mathf.Abs(_deltaRight) > 0)
        {
            // Only right wheel is moving
            var rotationDiff = rightDistance / wheelRadius; // Turn left
            targetRotation *= Quaternion.Euler(0, rotationDiff * Mathf.Rad2Deg, 0);

            // Move slightly forward while turning
            targetPosition += transform.forward * (Mathf.Abs(rightDistance) / 2f);
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            _rb.MovePosition(Vector3.Lerp(initialPosition, targetPosition, t));
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, t);

            yield return null;
        }

        _rb.MovePosition(targetPosition);
        transform.rotation = targetRotation;
    }
}