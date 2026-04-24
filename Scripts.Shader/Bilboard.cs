using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bilboard : MonoBehaviour
{
    [SerializeField] private bool onlyY = true;
    [SerializeField] private bool reverseForward = false;
    [SerializeField] private Camera targetCamera;



    private void LateUpdate()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        if (onlyY)
        {
            Vector3 lookDir = cam.transform.position - transform.position;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude < 0.0001f) return;

            if (reverseForward)
                lookDir = -lookDir;

            transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }
        else
        {
            Vector3 lookDir = cam.transform.position - transform.position;
            if (lookDir.sqrMagnitude < 0.0001f) return;

            if (reverseForward)
                lookDir = -lookDir;

            transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }
    }
}
