using UnityEngine;

public class Rotation : MonoBehaviour
{
    [SerializeField] private Transform _transform;
    [SerializeField, Range(0, 1)] private float _speed;

    void Awake()
    {
        if (_transform == null)
            _transform = gameObject.GetComponent<Transform>();
    }

    void Update()
    {
        Vector3 rotation = _transform.eulerAngles;
        rotation += new Vector3(0, _speed, 0);
        _transform.eulerAngles = rotation;
    }
}