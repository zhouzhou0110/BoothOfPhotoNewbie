using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 5f;
    public float turnSpeed = 100f;
    public float jumpForce = 5f;

    [Header("控制开关")]          // ← 新增
    public bool canMove = true;   // ← 新增：扇形脚本会控制它

    private Rigidbody rb;
    private bool isGrounded;
    private bool jumpPressed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        // 隐藏并锁定鼠标（游戏中鼠标消失，视角由鼠标控制）
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!canMove) return;   // ← 新增：禁止移动时也不响应转向

        // 按 ESC 取消锁定（鼠标重新出现），点击鼠标重新锁定
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 鼠标左右转视角
        float mouseX = Input.GetAxis("Mouse X") * turnSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up * mouseX);

        if (Input.GetButtonDown("Jump") && isGrounded)
            jumpPressed = true;
    }

    void FixedUpdate()
    {
        if (!canMove)           // ← 新增：不能移动时清空水平速度并返回
        {
            Vector3 vel = rb.velocity;
            vel.x = 0f;
            vel.z = 0f;
            rb.velocity = vel;
            return;
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = (transform.right * h + transform.forward * v).normalized;

        Vector3 vel2 = rb.velocity;
        vel2.x = move.x * moveSpeed;
        vel2.z = move.z * moveSpeed;
        rb.velocity = vel2;

        if (jumpPressed)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpPressed = false;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.5f)
            {
                isGrounded = true;
                return;
            }
        }
        isGrounded = false;
    }

    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}
