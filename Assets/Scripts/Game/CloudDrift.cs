using UnityEngine;

/// <summary>让高空云层以很慢的速度掠过地图。</summary>
public class CloudDrift : MonoBehaviour
{
    public Vector3 direction = new Vector3(1f, 0f, 0.25f);
    public float speed = 0.35f;
    public float boundary = 26f;

    void Update()
    {
        transform.position += direction.normalized * speed * Time.deltaTime;

        if (Mathf.Abs(transform.position.x) > boundary || Mathf.Abs(transform.position.z) > boundary)
        {
            Vector3 position = transform.position;
            transform.position = new Vector3(-position.x, position.y, -position.z);
        }
    }
}
