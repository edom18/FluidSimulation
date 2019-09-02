using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotater : MonoBehaviour
{
    [SerializeField]
    private float _speed = 1f;

    private void Update()
    {
        transform.Rotate(Vector3.one * _speed);
    }
}
