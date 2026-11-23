using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    public float velocidad = 6f;
    public float sensibilidadMouse = 2f;
    public float gravedad = -9.81f;
    public float alturaSalto = 1.5f;

    private CharacterController controller;
    private Transform camara;
    private float rotacionX = 0f;
    private Vector3 velocidadVertical;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        camara = GetComponentInChildren<Camera>().transform;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    void Update()
    {
        // Rotación de cámara
        float mouseX = Input.GetAxis("Mouse X") * sensibilidadMouse;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidadMouse;

        rotacionX -= mouseY;
        rotacionX = Mathf.Clamp(rotacionX, -80f, 80f);

        camara.localRotation = Quaternion.Euler(rotacionX, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // Comprobar contacto con el suelo
        bool enSuelo = controller.isGrounded;

        if (enSuelo && velocidadVertical.y < 0)
            velocidadVertical.y = -2f;

        // Saltar al presionar Espacio
        if (Input.GetButtonDown("Jump") && enSuelo)
            velocidadVertical.y = Mathf.Sqrt(alturaSalto * -2f * gravedad);

        // Aplicar gravedad
        velocidadVertical.y += gravedad * Time.deltaTime;

        // Movimiento con WASD
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 mover = transform.right * x + transform.forward * z;

        // Combinar desplazamiento horizontal y vertical
        Vector3 movimientoFinal = mover * velocidad + velocidadVertical;

        controller.Move(movimientoFinal * Time.deltaTime);

        // Liberar cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

}
