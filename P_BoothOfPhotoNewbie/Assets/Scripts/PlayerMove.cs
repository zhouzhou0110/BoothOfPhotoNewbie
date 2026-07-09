using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 5f;
    [Header("鼠标转向灵敏度")]
    public float mouseSensitivity = 100f;

    private Rigidbody rb;
    private float yRotate; // 存储水平旋转角度

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // 锁定鼠标到游戏窗口，隐藏光标
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // ========== 鼠标水平转向逻辑（Update执行输入检测） ==========
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        yRotate += mouseX;
        // 只修改物体Y轴旋转（左右转身），X/Z轴不动防止摔倒
        transform.localRotation = Quaternion.Euler(0, yRotate, 0);
    }

    void FixedUpdate()
    {
        // ========== WASD物理移动逻辑（不变） ==========
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 moveDir = transform.right * h + transform.forward * v;
        moveDir.Normalize();

        Vector3 vel = moveDir * moveSpeed;
        vel.y = rb.velocity.y; // 保留重力下落速度
        rb.velocity = vel;
    }

    // 可选：按ESC释放鼠标光标
    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.None;
            else
                Cursor.lockState = CursorLockMode.Locked;
        }
    }
}