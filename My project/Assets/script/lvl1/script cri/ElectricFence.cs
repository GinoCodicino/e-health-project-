using UnityEngine;

public class ElectricFence : MonoBehaviour
{
    [SerializeField] CameraShake cameraShake;
    [SerializeField] AudioSource suonoScossa;   // opzionale
    [SerializeField] float cooldown = 1f;

    float prossimaScossa;

    void Awake()
    {
        if (cameraShake == null && Camera.main != null)
            cameraShake = Camera.main.GetComponent<CameraShake>();

        if (cameraShake == null)
            cameraShake = FindFirstObjectByType<CameraShake>();
    }

    void OnTriggerEnter(Collider other)
    {
        ProvaScossa(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        ProvaScossa(collision.collider);
    }

    void ProvaScossa(Collider other)
    {
        if (!EIlGiocatore(other)) return;
        if (Time.time < prossimaScossa) return;

        prossimaScossa = Time.time + cooldown;
        if (cameraShake != null)
            cameraShake.Shake();
        if (suonoScossa != null) suonoScossa.Play();
    }

    bool EIlGiocatore(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        if (other.transform.root.CompareTag("Player")) return true;
        return other.GetComponentInParent<playercontroller>() != null;
    }
}